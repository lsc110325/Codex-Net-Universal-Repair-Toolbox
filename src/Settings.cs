using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexNetFix
{
    // 版本与更新日志
    public class VersionLog
    {
        public string Version = "";
        public bool Bate = false;
        public string[] Items = new string[0];
    }

    public static class AppVersion
    {
        public const string Num = "1.0.100";
        public const string Current = "v1.0.100";
        public const string Last = "v26.9.26.300bate";
        public const string Older = "v26.9.26.285bate";
        public const string Legacy = "v26.9.26.001bate";
        public const string Previous = "v26.9.26.300bate";

        public static string[] CurrentChanges()
        {
            return new string[] {
                "修复「最近修复记录」被重复自检刷屏的问题",
                "历史记录支持连续合并计数（×N）与一键清空",
                "记录卡片加高、行宽自适应，长文本不再截断",
                "修复辅助色联动：顶部栏与导航跟随所选配色",
                "保留全部历史版本日志（200 以前标注 bate）"
            };
        }

        // 全部版本日志（新版本在前；200 以前标注为 bate 版）
        public static List<VersionLog> All()
        {
            List<VersionLog> list = new List<VersionLog>();
            string[] vers = new string[] { "v1.0.100", "v26.9.26.300bate", "v26.9.26.285bate", "v26.9.26.280bate", "v26.9.26.200bate", "v26.9.26.050bate", "v26.9.26.010bate", "v26.9.26.001bate" };
            string[][] items = new string[][] {

                new string[] {
                    "正式版发布：导航与更新提示改为完整圆弧圆角",
                    "版本号调整为 v1.0.100（首个正式版）",
                    "英文目录结构：src / gallery / tools，本体在根目录"
                },                new string[] {
                    "界面结构整理：根目录放本体，代码 / 图库 / 工具分文件夹",
                    "更新提示支持【不再提醒】，主界面保留红色胶囊提醒",
                    "全部版本统一标记为 bate 版并保留历史日志",
                    "细节打磨：胶囊、圆角、间距与字号统一"
                },
                new string[] {
                    "修复「最近修复记录」被重复自检刷屏",
                    "历史记录连续合并计数（×N）与一键清空",
                    "记录卡片加高、长文本不再截断"
                },
                new string[] {
                    "修复辅助色串色（顶部栏 / 导航未跟随配色）",
                    "导航改为胶囊样式，支持拖动窗口",
                    "界面细节打磨，更新说明精简"
                },
                new string[] {
                    "PCL 风格界面（顶部栏 + 导航 + 侧栏 + 内容区）",
                    "移除深色模式，彻底消除黑边",
                    "首次启动提示条、清理旧版本、覆盖升级"
                },
                new string[] {
                    "白色圆角现代界面，四种辅助色",
                    "修复卡片内控件黑色描边",
                    "主界面重排、独立文件夹交付"
                },
                new string[] {
                    "系统托盘常驻 + 代理健康监控",
                    "一键重启 Codex、启动时自检",
                    "修复历史、复制报告、命令行脚本化"
                },
                new string[] {
                    "一键修复部署（配置 / 环境变量 / git TLS）",
                    "代理端口自动探测 + 真实出网验证",
                    "11 项全面自检 + 备份还原"
                }
            };
            for (int i = 0; i < vers.Length; i++)
            {
                VersionLog v = new VersionLog();
                v.Version = vers[i];
                v.Bate = true;
                v.Items = items[i];
                list.Add(v);
            }
            return list;
        }
}

    // 用户设置（持久化到 %APPDATA%\CodexNetFix\settings.json）
    public class AppSettings
    {
        public string ThemeMode = "light";     // light / dark
        public string AccentKey = "blue";      // blue / purple / yellow / pink
        public bool CheckOnStart = true;
        public bool Monitor = false;
        public int MonitorInterval = 60;       // 秒
        public bool TrayResident = false;
        public int LastPort = 0;
        public bool OptCodex = true;
        public bool OptUserEnv = true;
        public bool OptGit = true;
        public bool OptGitExec = true;
        public string LastSeenVersion = "";
        public bool SuppressUpdateTip = false;

        public static string Dir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexNetFix");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        public static string FilePath() { return Path.Combine(Dir(), "settings.json"); }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                string f = FilePath();
                if (!File.Exists(f)) return s;
                string t = File.ReadAllText(f, Encoding.UTF8);
                s.ThemeMode = GetStr(t, "theme", s.ThemeMode);
                s.AccentKey = GetStr(t, "accent", s.AccentKey);
                s.CheckOnStart = GetBool(t, "checkOnStart", s.CheckOnStart);
                s.Monitor = GetBool(t, "monitor", s.Monitor);
                s.MonitorInterval = GetInt(t, "monitorInterval", s.MonitorInterval);
                s.TrayResident = GetBool(t, "tray", s.TrayResident);
                s.LastPort = GetInt(t, "lastPort", s.LastPort);
                s.OptCodex = GetBool(t, "optCodex", s.OptCodex);
                s.OptUserEnv = GetBool(t, "optUserEnv", s.OptUserEnv);
                s.OptGit = GetBool(t, "optGit", s.OptGit);
                s.OptGitExec = GetBool(t, "optGitExec", s.OptGitExec);
                s.LastSeenVersion = GetStr(t, "lastSeenVersion", s.LastSeenVersion);
                s.SuppressUpdateTip = GetBool(t, "suppressTip", s.SuppressUpdateTip);
            }
            catch { }
            if (s.MonitorInterval < 15) s.MonitorInterval = 15;
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"theme\": \"" + ThemeMode + "\",");
                sb.AppendLine("  \"accent\": \"" + AccentKey + "\",");
                sb.AppendLine("  \"checkOnStart\": " + (CheckOnStart ? "true" : "false") + ",");
                sb.AppendLine("  \"monitor\": " + (Monitor ? "true" : "false") + ",");
                sb.AppendLine("  \"monitorInterval\": " + MonitorInterval + ",");
                sb.AppendLine("  \"tray\": " + (TrayResident ? "true" : "false") + ",");
                sb.AppendLine("  \"lastPort\": " + LastPort + ",");
                sb.AppendLine("  \"optCodex\": " + (OptCodex ? "true" : "false") + ",");
                sb.AppendLine("  \"optUserEnv\": " + (OptUserEnv ? "true" : "false") + ",");
                sb.AppendLine("  \"optGit\": " + (OptGit ? "true" : "false") + ",");
                sb.AppendLine("  \"optGitExec\": " + (OptGitExec ? "true" : "false") + ",");
                sb.AppendLine("  \"lastSeenVersion\": \"" + LastSeenVersion + "\",");
                sb.AppendLine("  \"suppressTip\": " + (SuppressUpdateTip ? "true" : "false"));
                sb.AppendLine("}");
                File.WriteAllText(FilePath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static string GetStr(string json, string key, string def)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : def;
        }
        static bool GetBool(string json, string key, bool def)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)");
            return m.Success ? (m.Groups[1].Value == "true") : def;
        }
        static int GetInt(string json, string key, int def)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            int v;
            return (m.Success && int.TryParse(m.Groups[1].Value, out v)) ? v : def;
        }
    }

    // 修复历史
    public static class History
    {
        public static string FilePath() { return Path.Combine(AppSettings.Dir(), "history.log"); }

        // 只记录有效变化：动作+结果+说明 与上一条完全相同则跳过，避免被自检刷屏
        public static void Add(string action, string result, string detail)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + action + " | " + result + " | " + detail;
                string f = FilePath();
                if (File.Exists(f))
                {
                    string last = LastLine(f);
                    if (last.Length > 0)
                    {
                        string[] p = last.Split('|');
                        if (p.Length >= 3 && p[1].Trim() == action && p[2].Trim() == result)
                        {
                            if (p.Length < 4 || p[3].Trim() == detail) return;
                        }
                    }
                }
                File.AppendAllText(f, line + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        static string LastLine(string f)
        {
            try
            {
                string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                for (int i = lines.Length - 1; i >= 0; i--) if (lines[i].Trim().Length > 0) return lines[i];
            }
            catch { }
            return "";
        }

        // 把连续重复项压缩为一条（带 ×N 次数），减少冗余
        public static void Compact()
        {
            try
            {
                string f = FilePath();
                if (!File.Exists(f)) return;
                string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                List<string> outp = new List<string>();
                string lastKey = ""; int count = 1; string lastBase = "";
                foreach (string ln in lines)
                {
                    string t = ln.Trim();
                    if (t.Length == 0) continue;
                    string[] p = t.Split('|');
                    string key = p.Length >= 3 ? (p[1].Trim() + "|" + p[2].Trim() + "|" + (p.Length >= 4 ? p[3].Trim() : "")) : t;
                    if (key == lastKey) { count++; continue; }
                    if (lastKey.Length > 0) outp.Add(lastBase + (count > 1 ? ("  ×" + count) : ""));
                    lastKey = key; lastBase = t; count = 1;
                }
                if (lastKey.Length > 0) outp.Add(lastBase + (count > 1 ? ("  ×" + count) : ""));
                File.WriteAllLines(f, outp.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        public static void Clear()
        {
            try { File.WriteAllText(FilePath(), "", new UTF8Encoding(false)); } catch { }
        }

        public static List<string> Recent(int count)
        {
            List<string> list = new List<string>();
            try
            {
                string f = FilePath();
                if (!File.Exists(f)) return list;
                string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                for (int i = lines.Length - 1; i >= 0 && list.Count < count; i--)
                {
                    string t = lines[i].Trim();
                    if (t.Length == 0) continue;
                    // 展示格式：时间 · 动作 · 结果 · 说明
                    string[] p = t.Split('|');
                    if (p.Length >= 3)
                        list.Add(p[0].Trim() + "   " + p[1].Trim() + "   " + p[2].Trim() + (p.Length >= 4 && p[3].Trim().Length > 0 ? ("   " + p[3].Trim()) : ""));
                    else list.Add(t);
                }
            }
            catch { }
            return list;
        }
    }
}
