using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;

namespace CodexNetFix
{
    public class CheckItem
    {
        public string Name = "";
        public string Status = "INFO";   // OK / WARN / FAIL / INFO
        public string Detail = "";
        public string Hint = "";
        public static CheckItem Make(string name, string status, string detail, string hint)
        {
            CheckItem c = new CheckItem();
            c.Name = name; c.Status = status; c.Detail = detail; c.Hint = hint;
            return c;
        }
    }

    public class RepairOptions
    {
        public int Port = 0;                 // 0 = 自动探测
        public bool WriteUserEnvVars = true; // 写入 HKCU\Environment
        public bool PatchCodexHome = true;   // config.toml + .env
        public bool PatchGitTls = true;      // git http.sslBackend=openssl
        public bool PatchGitExecPath = true; // config.toml 写 GIT_EXEC_PATH
    }

    public static class Core
    {
        public const string Tag = "codexfix";
        public static readonly int[] CommonPorts = new int[] { 7890, 7897, 7891, 10809, 10808, 1080, 8889, 8080, 8118, 20171, 33210, 2080 };

        // ---------- 基础 ----------
        public static string Timestamp()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        public static string CodexHome()
        {
            string fromEnv = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrEmpty(fromEnv))
            {
                string f = fromEnv.Trim();
                if (f.Length > 0) return f.TrimEnd(new char[] { '\\', '/' });
            }
            string up = Environment.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(up) && Directory.Exists(up)) return Path.Combine(up, ".codex");
            string sf = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(sf) && Directory.Exists(sf)) return Path.Combine(sf, ".codex");
            string hd = Environment.GetEnvironmentVariable("HOMEDRIVE");
            string hp = Environment.GetEnvironmentVariable("HOMEPATH");
            if (!string.IsNullOrEmpty(hd) && !string.IsNullOrEmpty(hp)) return Path.Combine(hd + hp, ".codex");
            string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(la)) return Path.Combine(la, ".codex");
            return Path.Combine(Environment.CurrentDirectory, ".codex");
        }
        public static string RunProcess(string exe, string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return "[超时]"; }
                    return (outp + err).Trim();
                }
            }
            catch (Exception ex) { return "[失败] " + ex.Message; }
        }

        // ---------- 代理探测 ----------
        public static bool TcpOpen(string host, int port, int timeoutMs)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        // 真实 HTTP 代理握手：CONNECT 到 example.com:443，期望 2xx
        public static bool ProbeHttpProxy(int port, out string detail)
        {
            detail = "";
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(2000)) { detail = "连接超时"; return false; }
                    c.EndConnect(ar);
                    c.ReceiveTimeout = 6000;
                    using (NetworkStream s = c.GetStream())
                    {
                        byte[] req = Encoding.ASCII.GetBytes("CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nProxy-Connection: keep-alive\r\n\r\n");
                        s.Write(req, 0, req.Length);
                        byte[] buf = new byte[256];
                        int n = s.Read(buf, 0, buf.Length);
                        if (n <= 0) { detail = "无响应"; return false; }
                        string head = Encoding.ASCII.GetString(buf, 0, n);
                        string first = head.Split('\n')[0].Trim();
                        detail = first;
                        return first.IndexOf(" 2", StringComparison.Ordinal) > 0 || first.IndexOf("200", StringComparison.Ordinal) >= 0;
                    }
                }
            }
            catch (Exception ex) { detail = ex.Message; return false; }
        }

        public static List<int> CandidatePorts()
        {
            List<int> list = new List<int>();
            // 1) 系统代理设置
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        object en = k.GetValue("ProxyEnable");
                        object ps = k.GetValue("ProxyServer");
                        if (en != null && ps != null && Convert.ToInt32(en) == 1)
                        {
                            string s = ps.ToString();
                            int idx = s.LastIndexOf(':');
                            if (idx > 0)
                            {
                                int p;
                                if (int.TryParse(s.Substring(idx + 1).TrimEnd('/'), out p) && !list.Contains(p)) list.Add(p);
                            }
                        }
                    }
                }
            }
            catch { }
            // 2) 常见端口
            foreach (int p in CommonPorts) if (!list.Contains(p)) list.Add(p);
            // 3) netstat 中由代理类进程监听的端口（优先）
            try
            {
                string[] patterns = new string[] { "clash", "v2ray", "xray", "sing-box", "mihomo", "verge", "nekoray", "ikuuu", "surge", "shadowsocks", "trojan", "hysteria", "warp", "proxy" };
                Dictionary<int, string> pidName = new Dictionary<int, string>();
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        string nm = p.ProcessName.ToLower();
                        pidName[p.Id] = nm;
                    }
                    catch { }
                }
                string outText = RunProcess("netstat", "-ano", 8000);
                foreach (string raw in outText.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5) continue;
                    string local = parts[1];
                    int pid;
                    if (!int.TryParse(parts[parts.Length - 1], out pid)) continue;
                    string name = pidName.ContainsKey(pid) ? pidName[pid] : "";
                    bool looksProxy = false;
                    foreach (string pat in patterns) if (name.IndexOf(pat, StringComparison.OrdinalIgnoreCase) >= 0) { looksProxy = true; break; }
                    if (!looksProxy) continue;
                    if (name.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    int c = local.LastIndexOf(':');
                    if (c <= 0) continue;
                    int port;
                    if (!int.TryParse(local.Substring(c + 1), out port)) continue;
                    if (!list.Contains(port)) list.Add(port);
                }
            }
            catch { }
            return list;
        }

        public static int DetectProxyPort(List<string> log)
        {
            List<int> all = CandidatePorts();
            List<int> tier1 = new List<int>();
            foreach (int p in CommonPorts) if (all.Contains(p) && !tier1.Contains(p)) tier1.Add(p);
            List<int> tier2 = new List<int>();
            foreach (int p in all) if (!tier1.Contains(p)) tier2.Add(p);
            int firstHandshake = 0;
            foreach (int port in tier1) { int r = TryPort(port, log, ref firstHandshake); if (r > 0) return r; }
            foreach (int port in tier2) { int r = TryPort(port, log, ref firstHandshake); if (r > 0) return r; }
            return firstHandshake;
        }

        static int TryPort(int port, List<string> log, ref int firstHandshake)
        {
            if (!TcpOpen("127.0.0.1", port, 400)) return 0;
            string detail;
            if (!ProbeHttpProxy(port, out detail))
            {
                if (log != null) log.Add("  端口 " + port + " → 监听但代理握手失败 (" + detail + ")");
                return 0;
            }
            if (firstHandshake == 0) firstHandshake = port;
            string egress = "";
            bool net = false;
            for (int i = 0; i < 2 && !net; i++) net = TestHttpEgressThroughProxy(port, out egress);
            if (log != null) log.Add("  端口 " + port + " → 代理握手正常" + (net ? "，出网验证成功 (" + egress + ")" : "，出网验证失败 (" + egress + ")"));
            return net ? port : 0;
        }
        // ---------- git 探测 ----------
        public static string FindGitExe()
        {
            try
            {
                string p = RunProcess("where", "git.exe", 5000);
                if (!string.IsNullOrEmpty(p) && !p.StartsWith("["))
                {
                    foreach (string line in p.Split('\n'))
                    {
                        string f = line.Trim();
                        if (f.Length > 0 && File.Exists(f)) return f;
                    }
                }
            }
            catch { }
            string[] guesses = new string[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\cmd\git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"OpenAI\Codex\runtimes")
            };
            if (File.Exists(guesses[0])) return guesses[0];
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"OpenAI\Codex\runtimes\cua_node");
                if (Directory.Exists(baseDir))
                {
                    foreach (string d in Directory.GetDirectories(baseDir, "*", SearchOption.AllDirectories))
                    {
                        string cand = Path.Combine(d, @"bin\node_modules");
                        if (Directory.Exists(cand)) continue;
                    }
                }
            }
            catch { }
            return "";
        }

        // 从 git.exe 推导 GIT_EXEC_PATH（git-core + mingw64\bin，双目录）
        public static string GitExecPathValue(string gitExe)
        {
            if (string.IsNullOrEmpty(gitExe)) return "";
            try
            {
                DirectoryInfo cmd = new DirectoryInfo(Path.GetDirectoryName(gitExe));   // ...\cmd
                DirectoryInfo root = cmd.Parent;                                        // ...\git
                string bin = Path.Combine(root.FullName, @"mingw64\bin");
                string core = Path.Combine(root.FullName, @"mingw64\libexec\git-core");
                List<string> parts = new List<string>();
                if (Directory.Exists(core)) parts.Add(core);
                if (Directory.Exists(bin)) parts.Add(bin);
                if (parts.Count == 0 && Directory.Exists(bin)) parts.Add(bin);
                return string.Join(";", parts.ToArray());
            }
            catch { return ""; }
        }

        // ---------- 备份 ----------
        public static string BackupFile(string path, List<string> log)
        {
            if (!File.Exists(path)) return "";
            string bak = path + ".bak-" + Tag + "-" + Timestamp();
            try
            {
                File.Copy(path, bak, true);
                if (log != null) log.Add("  已备份 " + Path.GetFileName(path) + " → " + Path.GetFileName(bak));
                return bak;
            }
            catch (Exception ex) { if (log != null) log.Add("  备份失败: " + ex.Message); return ""; }
        }

        // ---------- TOML 简单读写 ----------
        static List<string> ReadLines(string file)
        {
            if (!File.Exists(file)) return new List<string>();
            return new List<string>(File.ReadAllLines(file, Encoding.UTF8));
        }

        static int FindSection(List<string> lines, string section)
        {
            string want = "[" + section + "]";
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Trim() == want) return i;
            return -1;
        }

        public static bool TomlSetKeys(string file, string section, Dictionary<string, string> values, List<string> log, bool doBackup)
        {
            try
            {
                bool created = !File.Exists(file);
                List<string> lines = ReadLines(file);
                int idx = FindSection(lines, section);
                if (idx < 0)
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length != 0) lines.Add("");
                    lines.Add("[" + section + "]");
                    foreach (KeyValuePair<string, string> kv in values) lines.Add(kv.Key + " = " + kv.Value);
                }
                else
                {
                    int end = lines.Count;
                    for (int i = idx + 1; i < lines.Count; i++)
                    {
                        string t = lines[i].Trim();
                        if (t.StartsWith("[") && t.EndsWith("]")) { end = i; break; }
                    }
                    foreach (KeyValuePair<string, string> kv in values)
                    {
                        bool replaced = false;
                        for (int i = idx + 1; i < end; i++)
                        {
                            string t = lines[i].TrimStart();
                            if (t.StartsWith(kv.Key))
                            {
                                string rest = t.Substring(kv.Key.Length).TrimStart();
                                if (rest.StartsWith("="))
                                {
                                    lines[i] = kv.Key + " = " + kv.Value;
                                    replaced = true;
                                    break;
                                }
                            }
                        }
                        if (!replaced)
                        {
                            lines.Insert(end, kv.Key + " = " + kv.Value);
                            end++;
                        }
                    }
                }
                if (!created && doBackup) BackupFile(file, log);
                File.WriteAllText(file, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { if (log != null) log.Add("  写 " + file + " 失败: " + ex.Message); return false; }
        }

        public static string TomlGetKey(string file, string section, string key)
        {
            List<string> lines = ReadLines(file);
            int idx = FindSection(lines, section);
            if (idx < 0) return "";
            for (int i = idx + 1; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]")) break;
                if (t.StartsWith(key))
                {
                    string rest = t.Substring(key.Length).TrimStart();
                    if (rest.StartsWith("=")) return rest.Substring(1).Trim().Trim('"').Trim('\'');
                }
            }
            return "";
        }

        // ---------- 修复 ----------
        public static Dictionary<string, string> ProxyEnvMap(int port)
        {
            string url = "http://127.0.0.1:" + port;
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["HTTP_PROXY"] = url;
            d["HTTPS_PROXY"] = url;
            d["ALL_PROXY"] = url;
            d["NO_PROXY"] = "localhost,127.0.0.1,::1";
            d["http_proxy"] = url;
            d["https_proxy"] = url;
            d["all_proxy"] = url;
            d["no_proxy"] = "localhost,127.0.0.1,::1";
            d["NODE_USE_ENV_PROXY"] = "1";
            return d;
        }

        public static int Repair(RepairOptions opt, List<string> log)
        {
            int port = opt.Port;
            if (port <= 0)
            {
                log.Add("[1/6] 自动探测本机代理端口…");
                port = DetectProxyPort(log);
                if (port == 0)
                {
                    log.Add("  ✗ 未找到可用代理端口。请先启动你的代理软件(Clash/v2ray 等)并在界面手动填写端口。");
                    return 0;
                }
                log.Add("  ✓ 选用代理端口: 127.0.0.1:" + port);
            }
            else
            {
                string detail;
                bool ok = ProbeHttpProxy(port, out detail);
                log.Add("[1/6] 校验指定端口 " + port + " → " + (ok ? "✓ 可用 (" + detail + ")" : "✗ 不可用 (" + detail + ")"));
                if (!ok) return 0;
            }

            string home = CodexHome();
            try { Directory.CreateDirectory(home); }
            catch (Exception ex) { log.Add("  ✗ 无法创建 CODEX_HOME (" + home + "): " + ex.Message); return 0; }
            string cfg = Path.Combine(home, "config.toml");
            string envFile = Path.Combine(home, ".env");
            string gitExe = FindGitExe();
            string gitExec = GitExecPathValue(gitExe);

            if (opt.PatchCodexHome)
            {
                log.Add("[2/6] 写入 " + cfg);
                Dictionary<string, string> envKeys = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kv in ProxyEnvMap(port)) envKeys[kv.Key] = "\"" + kv.Value + "\"";
                if (opt.PatchGitExecPath && gitExec.Length > 0) envKeys["GIT_EXEC_PATH"] = "'" + gitExec + "'";
                TomlSetKeys(cfg, "shell_environment_policy.set", envKeys, log, true);
                Dictionary<string, string> sandboxKeys = new Dictionary<string, string>();
                sandboxKeys["network_access"] = "true";
                TomlSetKeys(cfg, "sandbox_workspace_write", sandboxKeys, log, false);
                log.Add("  ✓ config.toml 已更新 ([shell_environment_policy.set] + [sandbox_workspace_write])");

                log.Add("[3/6] 写入 " + envFile);
                BackupFile(envFile, log);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Codex 网络修复工具自动生成 - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("# 修改后请重启 Codex 使其生效");
                foreach (KeyValuePair<string, string> kv in ProxyEnvMap(port)) sb.AppendLine(kv.Key + "=" + kv.Value);
                if (opt.PatchGitExecPath && gitExec.Length > 0) sb.AppendLine("GIT_EXEC_PATH=" + gitExec);
                try
                {
                    File.WriteAllText(envFile, sb.ToString(), new UTF8Encoding(false));
                    log.Add("  ✓ .env 已写入 (" + port + " 端口)");
                }
                catch (Exception ex) { log.Add("  ✗ 写 .env 失败: " + ex.Message); }
            }
            else log.Add("[2/6][3/6] 跳过 CODEX_HOME 文件修复（未勾选）");

            if (opt.WriteUserEnvVars)
            {
                log.Add("[4/6] 写入用户级环境变量 HKCU\\Environment");
                bool envOk = true;
                envOk &= SetUserEnv("HTTP_PROXY", "http://127.0.0.1:" + port);
                envOk &= SetUserEnv("HTTPS_PROXY", "http://127.0.0.1:" + port);
                envOk &= SetUserEnv("ALL_PROXY", "http://127.0.0.1:" + port);
                envOk &= SetUserEnv("NO_PROXY", "localhost,127.0.0.1,::1");
                envOk &= SetUserEnv("NODE_USE_ENV_PROXY", "1");
                if (opt.PatchGitExecPath && gitExec.Length > 0) SetUserEnv("GIT_EXEC_PATH", gitExec);
                BroadcastChange();
                if (envOk) log.Add("  ✓ 已写入并广播设置变更");
                else log.Add("  ✗ 写用户级环境变量失败：当前很可能运行在 Codex 沙箱内。请关闭本工具，在【资源管理器】里直接双击运行（不要从 Codex 终端启动）。");
            }
            else log.Add("[4/6] 跳过用户级环境变量（未勾选）");

            if (opt.PatchGitTls && gitExe.Length > 0)
            {
                log.Add("[5/6] 修复 git 的 TLS 后端（schannel → openssl）");
                string cur = RunProcess(gitExe, "config --global --get http.sslBackend", 8000);
                if (cur.Trim().ToLower() == "openssl") log.Add("  ✓ 已是 openssl，无需修改");
                else
                {
                    string bakGit = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig");
                    try { }
                    catch { }
                    BackupFile(bakGit, log);
                    string r = RunProcess(gitExe, "config --global http.sslBackend openssl", 10000);
                    string now = RunProcess(gitExe, "config --global --get http.sslBackend", 8000);
                    log.Add(now.Trim().ToLower() == "openssl" ? "  ✓ 已设为 openssl" : "  ✗ 设置失败: " + r);
                }
            }
            else log.Add("[5/6] 跳过 git TLS 修复");

            log.Add("[6/6] 完成。请【完全退出并重启 Codex】后回到本工具点【全面自检】验证。");
            return port;
        }

        public static bool SetUserEnv(string name, string value)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k == null) return false;
                    k.SetValue(name, value, RegistryValueKind.String);
                }
                using (RegistryKey k2 = Registry.CurrentUser.OpenSubKey("Environment"))
                {
                    object v = k2 == null ? null : k2.GetValue(name);
                    return v != null && v.ToString() == value;
                }
            }
            catch { return false; }
        }
        public static void DeleteUserEnv(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k != null) k.DeleteValue(name, false);
                }
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        public static void BroadcastChange()
        {
            try
            {
                UIntPtr res;
                SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment", 2, 3000, out res);
            }
            catch { }
        }

        // ---------- 旧版本查找与清理 ----------
        public class OldCopy
        {
            public string Path = "";
            public bool IsDir = false;
            public long Size = 0;
        }

        public static List<OldCopy> FindOldCopies()
        {
            List<OldCopy> list = new List<OldCopy>();
            try
            {
                string curExe = Path.GetFullPath(Application_ExecutablePath());
                string curDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
                DirectoryInfo di = new DirectoryInfo(curDir);
                DirectoryInfo parent = di.Parent;
                string scanDir = parent != null ? parent.FullName : curDir;
                foreach (string entry in Directory.GetFileSystemEntries(scanDir))
                {
                    string full = Path.GetFullPath(entry);
                    if (full.TrimEnd('\\').Equals(curDir, StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Path.GetFileName(entry);
                    bool looksOurs = name.IndexOf("网络修复工具") >= 0
                                  || name.IndexOf("Network Repair Tool", StringComparison.OrdinalIgnoreCase) >= 0
                                  || name.IndexOf("CodexNetFix", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!looksOurs) continue;
                    OldCopy o = new OldCopy();
                    o.Path = full;
                    o.IsDir = Directory.Exists(full);
                    if (!o.IsDir) { try { o.Size = new FileInfo(full).Length; } catch { } }
                    list.Add(o);
                }
                // 同目录下的其它同名 exe（例如旧版残留）
                foreach (string f in Directory.GetFiles(curDir, "*.exe"))
                {
                    string full = Path.GetFullPath(f);
                    if (full.Equals(curExe, StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Path.GetFileName(f);
                    if (name.IndexOf("网络修复工具") >= 0 || name.IndexOf("Network Repair Tool", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        OldCopy o = new OldCopy();
                        o.Path = full;
                        try { o.Size = new FileInfo(full).Length; } catch { }
                        list.Add(o);
                    }
                }
            }
            catch { }
            return list;
        }

        static string Application_ExecutablePath()
        {
            try { return System.Windows.Forms.Application.ExecutablePath; }
            catch { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static int DeleteOldCopies(List<OldCopy> items, List<string> log)
        {
            int n = 0;
            string curDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
            foreach (OldCopy o in items)
            {
                try
                {
                    string full = Path.GetFullPath(o.Path);
                    if (full.TrimEnd('\\').Equals(curDir, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!full.StartsWith(Path.GetDirectoryName(curDir) ?? "", StringComparison.OrdinalIgnoreCase)) continue;
                    if (o.IsDir)
                    {
                        Directory.Delete(full, true);
                        if (log != null) log.Add("  ✓ 已删除旧版本文件夹: " + full);
                    }
                    else
                    {
                        File.Delete(full);
                        if (log != null) log.Add("  ✓ 已删除旧版本文件: " + full);
                    }
                    n++;
                }
                catch (Exception ex)
                {
                    if (log != null) log.Add("  ✗ 删除失败 " + o.Path + " : " + ex.Message);
                }
            }
            return n;
        }
        // 运行环境探针：检测是否在受限沙箱内运行（沙箱会隔离 CODEX_HOME 写权限与用户环境变量注册表）
        public static string ProbeEnvironment()
        {
            string home = CodexHome();
            try
            {
                Directory.CreateDirectory(home);
                string p = Path.Combine(home, ".codexfix-probe.tmp");
                File.WriteAllText(p, "probe");
                File.Delete(p);
            }
            catch
            {
                return "无法写入 CODEX_HOME（" + home + "）。当前很可能运行在 Codex 沙箱内 —— 请在【资源管理器】中直接双击运行本工具。";
            }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k == null) return "无法打开用户环境变量注册表键，请直接双击运行本工具。";
                    k.SetValue("CodexNetFixProbe", "1", RegistryValueKind.String);
                }
                bool seen = false;
                using (RegistryKey k2 = Registry.CurrentUser.OpenSubKey("Environment"))
                {
                    object v = k2 == null ? null : k2.GetValue("CodexNetFixProbe");
                    seen = v != null;
                }
                using (RegistryKey k3 = Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k3 != null) k3.DeleteValue("CodexNetFixProbe", false);
                }
                if (!seen) return "用户环境变量注册表被隔离（写入不可见）。当前很可能运行在 Codex 沙箱内 —— 请在【资源管理器】中直接双击运行本工具。";
            }
            catch { return "无法写入用户环境变量，请在【资源管理器】中直接双击运行本工具。"; }
            return "";
        }
        // ---------- 回滚 ----------
        public static List<string> NewestBackups(string originalPath)
        {
            List<string> found = new List<string>();
            try
            {
                string dir = Path.GetDirectoryName(originalPath);
                string name = Path.GetFileName(originalPath);
                if (!Directory.Exists(dir)) return found;
                foreach (string f in Directory.GetFiles(dir, name + ".bak-" + Tag + "-*"))
                    found.Add(f);
                found.Sort();
                found.Reverse();
            }
            catch { }
            return found;
        }

        public static bool Rollback(List<string> log)
        {
            string home = CodexHome();
            string[] targets = new string[] { Path.Combine(home, "config.toml"), Path.Combine(home, ".env"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig") };
            bool any = false;
            foreach (string t in targets)
            {
                List<string> baks = NewestBackups(t);
                if (baks.Count == 0)
                {
                    string fn = Path.GetFileName(t);
                    bool removed = false;
                    try
                    {
                        if (File.Exists(t))
                        {
                            string txt = File.ReadAllText(t);
                            if (fn == ".env" && txt.IndexOf("Codex 网络修复工具自动生成") >= 0) { File.Delete(t); removed = true; log.Add("  ✓ 已删除本工具生成的 .env（修复前不存在该文件）"); }
                            else if (fn == ".gitconfig" && txt.Trim().Replace("\t", "").Replace(" ", "").Contains("[http]sslBackend=openssl") && txt.Length < 80) { File.Delete(t); removed = true; log.Add("  ✓ 已删除本工具新建的 .gitconfig"); }
                            else if (fn == "config.toml" && txt.IndexOf("[shell_environment_policy.set]") >= 0 && txt.IndexOf("[sandbox_workspace_write]") >= 0)
                            {
                                int headers = 0;
                                bool foreign = false;
                                foreach (string raw in txt.Split('\n'))
                                {
                                    string s = raw.Trim();
                                    if (s.Length == 0 || s.StartsWith("#")) continue;
                                    if (s.StartsWith("[") && s.EndsWith("]"))
                                    {
                                        headers++;
                                        if (s != "[shell_environment_policy.set]" && s != "[sandbox_workspace_write]") foreign = true;
                                    }
                                    else if (s.IndexOf("=") < 0) foreign = true;
                                }
                                if (!foreign && headers == 2) { File.Delete(t); removed = true; log.Add("  ✓ 已删除本工具新建的 config.toml（修复前不存在该文件）"); }
                            }
                        }
                    }
                    catch (Exception ex) { log.Add("  ✗ 清理 " + fn + " 失败: " + ex.Message); }
                    if (removed) any = true;
                    else log.Add("  - " + fn + "：没有本工具的备份，跳过");
                    continue;
                }
                try
                {
                    File.Copy(baks[0], t, true);
                    log.Add("  ✓ " + Path.GetFileName(t) + " 已还原自 " + Path.GetFileName(baks[0]));
                    any = true;
                }
                catch (Exception ex) { log.Add("  ✗ " + Path.GetFileName(t) + " 还原失败: " + ex.Message); }
            }
            return any;
        }

        public static void RollbackUserEnv(List<string> log)
        {
            string[] names = new string[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "NODE_USE_ENV_PROXY", "http_proxy", "https_proxy", "all_proxy", "no_proxy" };
            foreach (string n in names) DeleteUserEnv(n);
            BroadcastChange();
            log.Add("  ✓ 已清除本工具写入的用户级代理变量（GIT_EXEC_PATH 保留，避免影响其他工具）");
        }

        // ---------- 自检 ----------
        public static int ExtractPort(string url)
        {
            if (string.IsNullOrEmpty(url)) return 0;
            int i = url.IndexOf("://");
            if (i >= 0) url = url.Substring(i + 3);
            int j = url.LastIndexOf(':');
            if (j < 0) return 0;
            string tail = url.Substring(j + 1);
            int k = tail.IndexOfAny(new char[] { '/', ' ', '"' });
            if (k >= 0) tail = tail.Substring(0, k);
            int p;
            if (int.TryParse(tail, out p)) return p;
            return 0;
        }
        public static List<CheckItem> SelfCheck(int port, bool deep, List<string> log)
        {
            List<CheckItem> list = new List<CheckItem>();
            string home = CodexHome();
            string cfg = Path.Combine(home, "config.toml");
            string envFile = Path.Combine(home, ".env");

            if (port <= 0)
            {
                port = DetectProxyPort(log);
            }

            // 1 代理端口
            if (port > 0 && TcpOpen("127.0.0.1", port, 800))
            {
                string detail;
                bool ok = ProbeHttpProxy(port, out detail);
                list.Add(CheckItem.Make("本机代理端口", ok ? "OK" : "WARN",
                    "127.0.0.1:" + port + " " + (ok ? "HTTP 代理握手正常" : "已监听但握手异常(" + detail + ")"),
                    ok ? "" : "该端口可能不是 HTTP/SOCKS 混合代理端口，请确认代理软件配置"));
            }
            else
            {
                list.Add(CheckItem.Make("本机代理端口", "FAIL", "未发现可用代理端口",
                    "请先启动代理软件(Clash/v2ray 等)，或在界面手动填写端口后重试"));
            }

            // 2 深度出网
            if (deep && port > 0)
            {
                string d = "";
                bool ok = TestEgressThroughProxy(port, out d);
                list.Add(CheckItem.Make("经代理访问外网(HTTPS)", ok ? "OK" : "FAIL", d,
                    ok ? "" : "代理端口可用但出网失败：检查代理节点是否已选择/订阅是否过期"));
            }

            // 3 CODEX_HOME
            list.Add(CheckItem.Make("CODEX_HOME", Directory.Exists(home) ? "OK" : "WARN", home,
                Directory.Exists(home) ? "" : "目录不存在，点【一键修复】会自动创建"));

            // 4 config.toml
            if (File.Exists(cfg))
            {
                string hp = TomlGetKey(cfg, "shell_environment_policy.set", "HTTP_PROXY");
                string np = TomlGetKey(cfg, "shell_environment_policy.set", "NO_PROXY");
                bool ok = false;
                string cfgNote = "";
                int cfgPort = ExtractPort(hp);
                if (cfgPort > 0)
                {
                    string hd;
                    ok = ProbeHttpProxy(cfgPort, out hd);
                    if (!ok) cfgNote = "配置的端口 " + cfgPort + " 无法作为代理使用 (" + hd + ")";
                }
                list.Add(CheckItem.Make("config.toml 代理解析", ok ? "OK" : "FAIL",
                    hp.Length > 0 ? hp + " / NO_PROXY=" + np : "未找到 [shell_environment_policy.set] 配置",
                    ok ? "" : "点【一键修复】重新写入"));

                string na = TomlGetKey(cfg, "sandbox_workspace_write", "network_access");
                list.Add(CheckItem.Make("沙箱 network_access", na == "true" ? "OK" : "WARN", "network_access = " + (na.Length == 0 ? "(未设置)" : na),
                    na == "true" ? "" : "宽松放行直连（部分机型无效，主要靠代理通道）"));

                string gp = TomlGetKey(cfg, "shell_environment_policy.set", "GIT_EXEC_PATH");
                list.Add(CheckItem.Make("GIT_EXEC_PATH 注入", gp.Length > 0 ? "OK" : "WARN", gp.Length > 0 ? "已注入" : "未设置",
                    gp.Length > 0 ? "" : "沙箱内 git clone 会报 'remote-https is not a git command'，建议修复"));
            }
            else list.Add(CheckItem.Make("config.toml", "FAIL", "文件不存在: " + cfg, "点【一键修复】自动创建"));

            // 5 .env
            if (File.Exists(envFile))
            {
                string content = File.ReadAllText(envFile);
                int envPort = 0;
                int at = content.IndexOf("127.0.0.1:");
                if (at >= 0)
                {
                    string tail = content.Substring(at + 10);
                    string digits = "";
                    foreach (char ch in tail)
                    {
                        if (ch >= '0' && ch <= '9') digits += ch;
                        else break;
                    }
                    int.TryParse(digits, out envPort);
                }
                bool ok = content.IndexOf("HTTP_PROXY") >= 0 && envPort > 0;
                string envNote = ok ? ("代理端口 " + envPort) : "内容缺少代理变量";
                if (ok && envPort != port)
                {
                    string hd2;
                    if (ProbeHttpProxy(envPort, out hd2)) envNote = "代理端口 " + envPort + "（与探测值不同，但可用）";
                }
                list.Add(CheckItem.Make("CODEX_HOME/.env", ok ? "OK" : "WARN", envNote,
                    ok ? "" : "点【一键修复】重新生成"));
            }
            else list.Add(CheckItem.Make("CODEX_HOME/.env", "WARN", "不存在", "点【一键修复】自动生成（Codex 会读取该文件）"));

            // 6 用户级环境变量
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Environment"))
                {
                    string v = k == null ? null : (string)k.GetValue("HTTP_PROXY");
                    bool ok = v != null && v.IndexOf(port.ToString()) >= 0;
                    string st = ok ? "OK" : "WARN";
                    string dt = v == null ? "未设置 HTTP_PROXY" : v;
                    string ht = ok ? "" : "勾选“写入用户环境变量”后点【一键修复】";
                    if (!ok && ProbeEnvironment().Length > 0)
                    {
                        st = "INFO";
                        dt = "受限环境（沙箱）隔离了注册表读取，无法判定";
                        ht = "请在资源管理器中双击本工具重新自检";
                    }
                    list.Add(CheckItem.Make("用户级环境变量", st, dt, ht));
                }
            }
            catch { list.Add(CheckItem.Make("用户级环境变量", "WARN", "读取失败", "")); }

            // 7 git TLS 后端
            string gitExe = FindGitExe();
            if (gitExe.Length > 0)
            {
                string be = RunProcess(gitExe, "config --global --get http.sslBackend", 8000).Trim();
                bool ok = be.ToLower() == "openssl";
                list.Add(CheckItem.Make("git TLS 后端", ok ? "OK" : "WARN", be.Length == 0 ? "(默认 schannel)" : be,
                    ok ? "" : "沙箱内 git/curl 会报 SEC_E_NO_CREDENTIALS，建议改为 openssl"));
            }
            else list.Add(CheckItem.Make("git", "INFO", "未检测到 git，跳过", ""));

            // 8 本进程环境是否已生效
            string cur = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            bool live = cur != null && cur.Length > 0;
            string curNote = live ? cur : "当前进程未加载代理变量";
            if (live && cur.IndexOf(port.ToString()) < 0)
            {
                int cp = ExtractPort(cur);
                string hd;
                if (cp > 0 && ProbeHttpProxy(cp, out hd)) curNote = cur + "（端口与探测值不同，但可用）";
            }
            list.Add(CheckItem.Make("当前进程代理环境", live ? "OK" : "WARN", curNote,
                live ? "" : "这是正常的：需要完全退出并重启 Codex（以及终端）后新进程才会继承"));

            // 9 NO_PROXY 回环保护
            string noProxy = Environment.GetEnvironmentVariable("NO_PROXY");
            bool loopOk = noProxy != null && noProxy.IndexOf("127.0.0.1") >= 0;
            list.Add(CheckItem.Make("回环地址保护", loopOk ? "OK" : "WARN",
                noProxy == null ? "(未设置)" : noProxy,
                loopOk ? "" : "缺少 127.0.0.1 保护，本地服务(如模型中转)可能被误走代理"));

            return list;
        }

        // 纯 HTTP 出网验证：不依赖 Windows schannel，沙箱内也能用
        public static bool TestHttpEgressThroughProxy(int port, out string detail)
        {
            detail = "";
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(3000)) { detail = "代理连接超时"; return false; }
                    c.EndConnect(ar);
                    c.ReceiveTimeout = 12000;
                    using (NetworkStream s = c.GetStream())
                    {
                        string req = "GET http://www.gstatic.com/generate_204 HTTP/1.1\r\nHost: www.gstatic.com\r\nUser-Agent: CodexNetFix/1.0\r\nConnection: close\r\n\r\n";
                        byte[] b = Encoding.ASCII.GetBytes(req);
                        s.Write(b, 0, b.Length);
                        byte[] buf = new byte[512];
                        int n = s.Read(buf, 0, buf.Length);
                        if (n <= 0) { detail = "无响应"; return false; }
                        string head = Encoding.ASCII.GetString(buf, 0, n);
                        string first = head.Split('\n')[0].Trim();
                        detail = first;
                        return first.IndexOf(" 204", StringComparison.Ordinal) > 0 || first.IndexOf(" 200", StringComparison.Ordinal) > 0
                            || first.IndexOf(" 301", StringComparison.Ordinal) > 0 || first.IndexOf(" 302", StringComparison.Ordinal) > 0;
                    }
                }
            }
            catch (Exception ex) { detail = ex.Message; return false; }
        }
        public static bool TestEgressThroughProxy(int port, out string detail)
        {
            detail = "";
            string httpDetail;
            if (TestHttpEgressThroughProxy(port, out httpDetail)) { detail = httpDetail; return true; }
            string[] urls = new string[] { "https://www.gstatic.com/generate_204", "https://developers.openai.com/codex/" };
            foreach (string u in urls)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(u);
                    req.Proxy = new WebProxy("http://127.0.0.1:" + port);
                    req.Timeout = 20000;
                    req.UserAgent = "CodexNetFix/1.0";
                    req.AllowAutoRedirect = true;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        int code = (int)resp.StatusCode;
                        detail = u + " → HTTP " + code;
                        if (code >= 200 && code < 400) return true;
                    }
                }
                catch (Exception ex)
                {
                    detail = u + " → " + ex.Message;
                }
            }
            return false;
        }

        // 快速组合探针：端口监听 -> 代理解析 -> 真实出网（供后台监控使用）
        public static bool QuickProbe(int port, out string detail)
        {
            detail = "";
            if (port <= 0) { detail = "未指定端口"; return false; }
            if (!TcpOpen("127.0.0.1", port, 800)) { detail = "端口未监听"; return false; }
            string hd;
            if (!ProbeHttpProxy(port, out hd)) { detail = "代理握手失败: " + hd; return false; }
            string eg;
            if (!TestHttpEgressThroughProxy(port, out eg)) { detail = "出网失败: " + eg; return false; }
            detail = eg;
            return true;
        }

        public class ProcInfo
        {
            public int Id;
            public string Name = "";
            public string Path = "";
        }

        // 发现 Codex 相关进程（桌面应用 / CLI / 命令执行器 / 沙箱服务）
        public static List<ProcInfo> CodexProcesses()
        {
            List<ProcInfo> list = new List<ProcInfo>();
            foreach (Process p in Process.GetProcesses())
            {
                string n = "";
                try { n = p.ProcessName; } catch { continue; }
                string ln = n.ToLower();
                bool hit = ln == "chatgpt" || ln.StartsWith("codex") || ln.IndexOf("codex-") >= 0;
                if (!hit) continue;
                ProcInfo i = new ProcInfo();
                i.Id = p.Id;
                i.Name = n;
                try { i.Path = p.MainModule.FileName; } catch { }
                list.Add(i);
            }
            return list;
        }

        // 一键重启 Codex：关闭相关进程后重新拉起桌面应用
        public static string RestartCodex(List<string> log, bool dryRun)
        {
            List<ProcInfo> procs = CodexProcesses();
            string appExe = "";
            foreach (ProcInfo p in procs)
                if (p.Path.Length > 0 && p.Name.ToLower().IndexOf("chatgpt") >= 0) appExe = p.Path;
            if (log != null)
            {
                log.Add("  检测到 Codex 相关进程 " + procs.Count + " 个");
                foreach (ProcInfo p in procs) log.Add("    - " + p.Name + " (PID " + p.Id + ")");
            }
            if (dryRun) return "DRYRUN:" + procs.Count;
            int closed = 0;
            foreach (ProcInfo p in procs)
            {
                try
                {
                    Process proc = Process.GetProcessById(p.Id);
                    if (proc.CloseMainWindow()) proc.WaitForExit(3000);
                    if (!proc.HasExited) proc.Kill();
                    closed++;
                }
                catch { }
            }
            System.Threading.Thread.Sleep(1500);
            bool launched = false;
            if (appExe.Length > 0 && File.Exists(appExe))
            {
                try { Process.Start(appExe); launched = true; } catch { }
            }
            if (!launched)
            {
                string cli = FindCodexExe();
                if (cli.Length > 0)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(cli, "app");
                        psi.UseShellExecute = false;
                        Process.Start(psi);
                        launched = true;
                    }
                    catch { }
                }
            }
            if (log != null) log.Add(launched ? "  ✓ 已重新启动 Codex" : "  ⚠ 已关闭 Codex，但未能自动启动，请手动打开");
            return launched ? "OK" : "PARTIAL";
        }
        // 查找 Codex CLI 可执行文件
        public static string FindCodexExe()
        {
            try
            {
                string p = RunProcess("where", "codex.exe", 5000);
                if (!string.IsNullOrEmpty(p) && !p.StartsWith("["))
                    foreach (string ln in p.Split('\n'))
                    {
                        string f = ln.Trim();
                        if (f.Length > 0 && File.Exists(f)) return f;
                    }
            }
            catch { }
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"OpenAI\Codex\bin");
                if (Directory.Exists(baseDir))
                {
                    string best = "";
                    DateTime bestT = DateTime.MinValue;
                    foreach (string f in Directory.GetFiles(baseDir, "codex.exe", SearchOption.AllDirectories))
                    {
                        DateTime t = File.GetLastWriteTimeUtc(f);
                        if (t > bestT) { bestT = t; best = f; }
                    }
                    return best;
                }
            }
            catch { }
            return "";
        }

        // 端到端验证：用当前生成的配置，在 Codex 沙箱里真实访问网络
        public static string RunSandboxProbe(string codexExe, List<string> log)
        {
            string probePath = Path.Combine(Path.GetTempPath(), "codexfix-probe-" + Timestamp() + ".ps1");
            string probe = @"$out = @()
$out += ""HTTP_PROXY="" + [Environment]::GetEnvironmentVariable(""HTTP_PROXY"")
$g = (Get-Command git.exe -ErrorAction SilentlyContinue).Source
if ($g) {
  $r = (& $g ls-remote https://github.com/octocat/Hello-World refs/heads/master 2>&1 | Select-Object -First 1)
  $rs = ""$r""
  if ($rs -match ""^[0-9a-f]{40}"") { $out += ""GIT=OK"" }
  else { $out += ""GIT=FAIL:"" + $rs.Substring(0, [Math]::Min(80, $rs.Length)) }
} else { $out += ""GIT=NA"" }
$n = (Get-Command node.exe -ErrorAction SilentlyContinue).Source
if ($n) {
  $js = ""const us=['https://developers.openai.com/codex/','https://github.com','https://www.baidu.com'];(async()=>{for(const u of us){try{const r=await fetch(u,{signal:AbortSignal.timeout(20000)});console.log('NODE='+r.status);return}catch(e){}}console.log('NODE=FAIL')})()""
  $nodeOut = (& $n -e $js 2>&1 | Select-Object -Last 1)
if (""$nodeOut"" -match ""FAIL"") { $out += ""NODE=FAIL:"" + $nodeOut.Substring(0, [Math]::Min(80, $nodeOut.Length)) } else { $out += ""$nodeOut"" }
} else { $out += ""NODE=NA"" }
try { $x = Invoke-WebRequest -UseBasicParsing -TimeoutSec 8 'http://127.0.0.1:15721/v1/models'; $out += ""RELAY="" + $x.StatusCode } catch { $out += ""RELAY=NA"" }
$out -join ""`n""
";
            File.WriteAllText(probePath, probe, new UTF8Encoding(true));
            string outp = RunProcess(codexExe, "sandbox -- powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + probePath + "\"", 180000);
            try { File.Delete(probePath); } catch { }
            if (log != null) log.Add("  沙箱探针输出: " + outp.Replace("\r\n", " | ").Replace("\n", " | "));
            return outp;
        }
        public static string ReportText(int port, List<CheckItem> items, List<string> log)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Codex 网络修复工具 · 诊断报告");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("计算机: " + Environment.MachineName + "   用户: " + Environment.UserName);
            sb.AppendLine("CODEX_HOME: " + CodexHome());
            sb.AppendLine("代理端口: " + (port > 0 ? port.ToString() : "(未探测到)"));
            sb.AppendLine(new string('-', 60));
            foreach (CheckItem c in items) sb.AppendLine("[" + c.Status + "] " + c.Name + " : " + c.Detail + (c.Hint.Length > 0 ? "  ⇒ " + c.Hint : ""));
            sb.AppendLine(new string('-', 60));
            foreach (string l in log) sb.AppendLine(l);
            return sb.ToString();
        }
    }
}