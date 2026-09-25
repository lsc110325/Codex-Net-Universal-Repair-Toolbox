# 仓库描述 / Repository Description

可直接复制粘贴到 GitHub。GitHub 的 Description 字段上限为 350 字符。

---

## 1. About（一句话简介，推荐）

**中文（N 字符）**

```
一键修复 Codex / ChatGPT 桌面版在 Windows 上的网络连接问题：自动探测本机代理、覆盖沙箱黑洞代理 127.0.0.1:9、修复 git TLS 与 GIT_EXEC_PATH，并提供 11 项全面自检、一键回滚与诊断报告。单文件绿色工具，零依赖、免安装、免管理员。
```

**English**

```
One-click network repair for Codex / ChatGPT desktop on Windows: detects your local proxy, overrides the sandbox black-hole proxy, fixes git TLS and GIT_EXEC_PATH - plus an 11-item self-check, one-click rollback and diagnostics. Single-file, portable, zero dependencies.
```

## 2. Topics（标签，逐条添加）

```
codex
chatgpt
openai
proxy
clash
windows
sandbox
network-troubleshooting
git
csharp
winforms
dotnet-framework
china-network
developer-tools
```

## 3. 详细描述（用于 README 顶部、社交预览说明或 Release 介绍）

```
在 Windows 上，Codex 沙箱默认禁止非回环网络访问，并会注入黑洞代理 HTTP_PROXY=http://127.0.0.1:9，
导致 Codex 内执行命令时全部断网；同时 .env 中的代理不会覆盖已存在的环境变量，手工排查非常耗时。

本工具把整套修复自动化：

• 自动探测本机可用代理端口（TCP + CONNECT 握手 + 真实出网验证）
• 写入 [shell_environment_policy.set] 覆盖黑洞代理，并同步写入 .env
• 写入用户级代理环境变量（HKCU\Environment，仅当前用户）
• 注入 GIT_EXEC_PATH 修复沙箱内 git clone，git TLS 切换为 openssl
• 11 项全面自检 + 诊断报告导出
• 所有改动前自动备份，支持一键完整回滚
• PCL 风格界面、四种辅助色、托盘常驻、代理健康监控、修复历史
• 完整 CLI 子命令，便于批量部署

单文件 EXE，使用系统自带 .NET Framework 4.8 编译与运行，无需安装、无需管理员权限。
```

## 4. 一句话卖点（用于分享/群介绍）

```
Codex 又连不上网？双击一下，自动把 Codex 的网络修好（代理注入 / 配置修复 / 一键回滚）。
```

## 5. 社交预览图（Social preview）

上传 `assets/screenshots/ui-repair.png`（1120×780，主界面截图）。

## 6. 发布前替换清单

- `LICENSE` 中的版权人改成你的名字或组织名
- 仓库名如与 `codex-network-repair-tool` 不同，同步更新 README 中的链接与徽章