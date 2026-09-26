using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

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
        public const string Num = "1.1.150";
        public const string Current = "v1.1.150";
        public const string Last = "v1.1.125";
        public const string Older = "v26.9.26.285bate";
        public const string Legacy = "v26.9.26.001bate";
        public const string Previous = "v1.1.125";

        // 作者有话说（显示在更新日志页顶部）
        public static string AuthorNote()
        {
            return "这是一名 15 岁高中生使用 DeepSeek V4.1 Flash 制作的一个小工具，会持续更新该工具，"
                 + "可能有些地方 bug 很多，但我会努力学习完善的，未来还会制作更多的工具，感谢大家的支持！"
                 + "由于高中学业紧张、住宿半月放假，来不及回复消息，"
                 + "大家可以加入技术反馈 QQ 群：783904560 共同探讨与反馈问题。";
        }
        public static string[] CurrentChanges()
        {
            return new string[] {
                "新增独立 Token 优化导航分区，移除“更多”页里的旧入口",
                "新增今日常用 Token 统计、7 日柱状图和今日 Token 预警阈值",
                "Token 分区整合 Ponytail、省 Token 提示词、代理测速和网络监测",
                "新增 Token 预警开关和 10M 步进阈值调整",
                "版本更新为 v1.1.150"
            };
        }

        // 全部版本日志（新版本在前；200 以前标注为 bate 版）
        public static List<VersionLog> All()
        {
            List<VersionLog> list = new List<VersionLog>();
            string[] vers = new string[] { "v1.1.150", "v1.1.125", "v1.1.100", "v1.0.350", "v1.0.325", "v1.0.300", "v1.0.275", "v1.0.250", "v1.0.225", "v1.0.200", "v1.0.125", "v1.0.100", "v26.9.27.100bate", "v26.9.26.300bate", "v26.9.26.285bate", "v26.9.26.280bate", "v26.9.26.200bate", "v26.9.26.050bate", "v26.9.26.010bate", "v26.9.26.001bate" };
            string[][] items = new string[][] {

                new string[] {
                    "新增独立 Token 优化导航分区，移除“更多”页旧入口",
                    "新增今日 Token 统计、7 日柱状图和 Token 预警",
                    "整合 Ponytail、省 Token 提示词、代理测速和网络监测",
                    "新增预警开关与阈值调整",
                    "版本更新为 v1.1.150"
                },
                new string[] {
                    "重做星空黑主题：深空渐变、星云、多层星点和十字星光",
                    "优化星空黑整体配色，卡片与背景改为深蓝黑层次",
                    "保留圆角与导航布局，避免黑色边框",
                    "版本更新为 v1.1.125"
                },
                new string[] {
                    "新增 Token 优化：Ponytail 安装/更新、省 Token 提示词和代理监测",
                    "新增今日 Token 统计与近 7 日消耗柱状图",
                    "新增淡绿色主题和三种隐藏彩蛋主题：彩蛋蓝、无限粉、星空黑",
                    "新增一次性调试码，可解锁全部彩蛋颜色",
                    "新增液态玻璃模式占位（敬请期待）",
                    "修复重复打开工具出现多个窗口",
                    "版本更新为 v1.1.100"
                },
                new string[] {
                    "尝试开发项目官网：功能介绍、开发建议投稿、GitHub 与下载入口",
                    "由于开发和维护成本过高，官网方案停止，作为弃案保留",
                    "网站地址（弃案存档）：https://lsc110325.github.io/Codex-Net-Universal-Repair-Toolbox/",
                    "界面截图已保留：gallery/website-preview.png",
                    "后续不再继续开发该网站"
                },
                new string[] {
                    "更新提示弹窗缩小为轻量卡片，只显示当前版本内容",
                    "更新日志页拆分为“正式版更新”和“测试版更新”",
                    "两个区域独立滚动，互不影响",
                    "版本更新为 v1.0.350"
                },
                new string[] {
                    "移除后台命令提示符宿主，关闭提示符不再导致软件退出",
                    "新增打开 / 最小化 / 恢复 / 关闭淡入淡出动画",
                    "修复最小化偶发误关闭问题",
                    "动画保持圆角窗口区域，无黑色边框",
                    "版本更新为 v1.0.325"
                },
                new string[] {
                    "导航栏启用双缓冲绘制，消除切换时闪烁",
                    "动画刷新频率提高，并自动取消重叠动画",
                    "移除导航点击时误触发的窗口拖动",
                    "减少页面切换时的重复控件样式遍历",
                    "版本更新为 v1.0.300"
                },
                new string[] {
                    "修复重启 Codex / 一键全流程会误关工具自身",
                    "修复代理软件选择列表无法手动切换 exe",
                    "代理软件启动时使用自身目录作为工作目录",
                    "完善重启进程排除规则",
                    "版本更新为 v1.0.275"
                },
                new string[] {
                    "新增“快捷键”设置卡片和独立编辑窗口，可自定义五个功能按键",
                    "新增快捷键总开关，关闭后立即停用且保留设置",
                    "支持恢复默认、重复快捷键检测、系统保留组合拦截",
                    "新增快捷键命令行管理入口",
                    "版本更新为 v1.0.250"
                },
                new string[] {
                    "新增「更多」分区：代理启动 / 一键全流程 / 测速 / 快照 / 反馈包 / 诊断",
                    "支持自动发现并启动 FlClash、iKuuu、v2rayN、Hiddify 等代理软件",
                    "新增配置快照、端口占用排查、系统时间检查和命令行诊断入口",
                    "新增窗口标题拖动修复与 Ctrl+F / Ctrl+T / Ctrl+R / F5 / Ctrl+M 快捷键",
                    "版本更新为 v1.0.225"
                },
                new string[] {
                    "界面动画：开关滑块 / 按钮过渡 / 导航滑块（圆角矩形 + ease-in-out）",
                    "导航改为父级统一绘制 + 命中测试，修复文字与滑块消失",
                    "最小化 / 关闭改为纯图标，悬停半透明高亮（不再刺眼）",
                    "更新日志新增「作者有话说」并固定置顶",
                    "版本号升级为 v1.0.200"
                },
                new string[] {
                    "更新日志新增「作者有话说」分区",
                    "版本号升级为 v1.0.125"
                },
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
                    "升级全新 UI 与界面风格",
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
                v.Bate = vers[i].StartsWith("v26.9.");
                v.Items = items[i];
                list.Add(v);
            }
            return list;
        }
}

    // 用户设置（持久化到 %APPDATA%\CodexNetFix\settings.json）
    public static class HotkeyUtil
    {
        const Keys ModsMask = Keys.Control | Keys.Alt | Keys.Shift;

        public static bool TryParse(string text, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                KeysConverter conv = new KeysConverter();
                object obj = conv.ConvertFromString(text.Trim());
                if (obj == null) return false;
                key = (Keys)obj;
                return key != Keys.None;
            }
            catch { return false; }
        }

        public static string Format(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            Keys mods = key & ModsMask;
            List<string> parts = new List<string>();
            if ((mods & Keys.Control) != 0) parts.Add("Ctrl");
            if ((mods & Keys.Alt) != 0) parts.Add("Alt");
            if ((mods & Keys.Shift) != 0) parts.Add("Shift");
            try { parts.Add(new KeysConverter().ConvertToString(code)); }
            catch { parts.Add(code.ToString()); }
            return string.Join("+", parts.ToArray());
        }

        public static bool IsValid(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            Keys mods = key & ModsMask;
            if (code == Keys.None) return false;
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu ||
                code == Keys.LWin || code == Keys.RWin) return false;
            if ((key & Keys.LWin) != 0 || (key & Keys.RWin) != 0) return false;
            bool fn = code >= Keys.F1 && code <= Keys.F12;
            if (mods == Keys.None) return fn;
            return true;
        }

        public static bool IsReserved(Keys key)
        {
            Keys k = key & (Keys.KeyCode | ModsMask);
            if (k == (Keys.Alt | Keys.F4)) return true;
            if (k == (Keys.Alt | Keys.Tab)) return true;
            if (k == (Keys.Control | Keys.Escape)) return true;
            return false;
        }

        public static bool IsMatch(Keys pressed, string configured)
        {
            Keys want;
            if (!TryParse(configured, out want)) return false;
            return (pressed & (Keys.KeyCode | ModsMask)) == (want & (Keys.KeyCode | ModsMask));
        }

        public static bool Same(string a, string b)
        {
            Keys ka, kb;
            return TryParse(a, out ka) && TryParse(b, out kb) && IsMatch(ka, b);
        }
    }

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
        public bool HotkeysEnabled = true;
        public string HotkeyRepair = "Ctrl+F";
        public string HotkeyCheck = "Ctrl+T";
        public string HotkeyRestart = "Ctrl+R";
        public string HotkeyRefresh = "F5";
        public string HotkeyMore = "Ctrl+M";
        public bool SuppressUpdateTip = false;
        public bool EnableAnim = true;   // 界面动画（滑块/胶囊滑动/悬停过渡）
        public bool DomesticDirect = false;   // 国内网络直连（国内站点不走代理）
        public string ProxyPath = "";        // 代理软件可执行文件路径（记住用户选择）
        public bool EasterBlueUnlocked = false;
        public bool InfinitePinkUnlocked = false;
        public bool StarryUnlocked = false;
        public bool DebugKeyUsed = false;
        public int RepairCount = 0;
        public bool TokenWarnEnabled = true;
        public int TokenWarnMillions = 100;

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
                s.EnableAnim = GetBool(t, "enableAnim", s.EnableAnim);
                s.DomesticDirect = GetBool(t, "domesticDirect", s.DomesticDirect);
                s.ProxyPath = GetStr(t, "proxyPath", s.ProxyPath);
                s.EasterBlueUnlocked = GetBool(t, "easterBlue", s.EasterBlueUnlocked);
                s.InfinitePinkUnlocked = GetBool(t, "infinitePink", s.InfinitePinkUnlocked);
                s.StarryUnlocked = GetBool(t, "starry", s.StarryUnlocked);
                s.DebugKeyUsed = GetBool(t, "debugKeyUsed", s.DebugKeyUsed);
                s.RepairCount = GetInt(t, "repairCount", s.RepairCount);
                s.TokenWarnEnabled = GetBool(t, "tokenWarnEnabled", s.TokenWarnEnabled);
                s.TokenWarnMillions = GetInt(t, "tokenWarnMillions", s.TokenWarnMillions);
                s.HotkeysEnabled = GetBool(t, "hotkeysEnabled", s.HotkeysEnabled);
                s.HotkeyRepair = GetStr(t, "hotkeyRepair", s.HotkeyRepair);
                s.HotkeyCheck = GetStr(t, "hotkeyCheck", s.HotkeyCheck);
                s.HotkeyRestart = GetStr(t, "hotkeyRestart", s.HotkeyRestart);
                s.HotkeyRefresh = GetStr(t, "hotkeyRefresh", s.HotkeyRefresh);
                s.HotkeyMore = GetStr(t, "hotkeyMore", s.HotkeyMore);
                s.HotkeyRepair = SafeHotkey(s.HotkeyRepair, "Ctrl+F");
                s.HotkeyCheck = SafeHotkey(s.HotkeyCheck, "Ctrl+T");
                s.HotkeyRestart = SafeHotkey(s.HotkeyRestart, "Ctrl+R");
                s.HotkeyRefresh = SafeHotkey(s.HotkeyRefresh, "F5");
                s.HotkeyMore = SafeHotkey(s.HotkeyMore, "Ctrl+M");
            }
            catch { }
            if (s.MonitorInterval < 15) s.MonitorInterval = 15;
            if (s.AccentKey == "easterblue" && !s.EasterBlueUnlocked) s.AccentKey = "blue";
            if (s.AccentKey == "infinitepink" && !s.InfinitePinkUnlocked) s.AccentKey = "blue";
            if (s.AccentKey == "starry" && !s.StarryUnlocked) s.AccentKey = "blue";
            if (s.TokenWarnMillions < 1) s.TokenWarnMillions = 1;
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
                sb.AppendLine("  \"suppressTip\": " + (SuppressUpdateTip ? "true" : "false") + ",");
                sb.AppendLine("  \"enableAnim\": " + (EnableAnim ? "true" : "false") + ",");
                sb.AppendLine("  \"domesticDirect\": " + (DomesticDirect ? "true" : "false") + ",");
                sb.AppendLine("  \"proxyPath\": \"" + ProxyPath.Replace("\\", "\\\\") + "\",");
                sb.AppendLine("  \"easterBlue\": " + (EasterBlueUnlocked ? "true" : "false") + ",");
                sb.AppendLine("  \"infinitePink\": " + (InfinitePinkUnlocked ? "true" : "false") + ",");
                sb.AppendLine("  \"starry\": " + (StarryUnlocked ? "true" : "false") + ",");
                sb.AppendLine("  \"debugKeyUsed\": " + (DebugKeyUsed ? "true" : "false") + ",");
                sb.AppendLine("  \"repairCount\": " + RepairCount + ",");
                sb.AppendLine("  \"tokenWarnEnabled\": " + (TokenWarnEnabled ? "true" : "false") + ",");
                sb.AppendLine("  \"tokenWarnMillions\": " + TokenWarnMillions + ",");
                sb.AppendLine("  \"hotkeysEnabled\": " + (HotkeysEnabled ? "true" : "false") + ",");
                sb.AppendLine("  \"hotkeyRepair\": \"" + HotkeyRepair + "\",");
                sb.AppendLine("  \"hotkeyCheck\": \"" + HotkeyCheck + "\",");
                sb.AppendLine("  \"hotkeyRestart\": \"" + HotkeyRestart + "\",");
                sb.AppendLine("  \"hotkeyRefresh\": \"" + HotkeyRefresh + "\",");
                sb.AppendLine("  \"hotkeyMore\": \"" + HotkeyMore + "\"");
                sb.AppendLine("}");
                File.WriteAllText(FilePath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static string SafeHotkey(string value, string fallback)
        {
            Keys k;
            if (HotkeyUtil.TryParse(value, out k) && HotkeyUtil.IsValid(k) && !HotkeyUtil.IsReserved(k))
                return HotkeyUtil.Format(k);
            return fallback;
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
                File.AppendAllText(f, line + "\r\n", new UTF8Encoding(true));
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
                File.WriteAllLines(f, outp.ToArray(), new UTF8Encoding(true));
            }
            catch { }
        }

        public static void Clear()
        {
            try { File.WriteAllText(FilePath(), "", new UTF8Encoding(true)); } catch { }
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
