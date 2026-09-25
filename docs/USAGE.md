# 使用说明 / Usage

## 1. 准备

- 系统：Windows 10 / 11（.NET Framework 4.8 为系统自带）
- 代理软件：Clash / Clash Verge / v2ray / iKuuu 等，确认**节点已连接**且允许本机回环访问

## 2. 一键修复（推荐）

1. **在资源管理器中双击** `Codex网络修复工具.exe`
2. 左侧点 **【一键修复并部署】**，工具会依次：
   - 探测可用代理端口（TCP → `CONNECT` 握手 → 真实出网验证）
   - 备份并修补 `~\.codex\config.toml`、写入 `~\.codex\.env`
   - 写入用户级环境变量（`HKCU\Environment`）
   - 修复 git：`GIT_EXEC_PATH` + `http.sslBackend=openssl`
3. 点 **【重启 Codex】**（会自动关闭 Codex 相关进程并重新打开）
4. 点 **【全面自检】**，确认全部 `OK`

## 3. 自检项说明

| 自检项 | 通过标准 |
|---|---|
| 本机代理端口 | 端口监听且 HTTP 代理握手成功 |
| 经代理访问外网 | 通过代理请求外网返回 2xx / 3xx |
| CODEX_HOME | 目录存在且可写 |
| config.toml 代理解析 | 配置中的代理端口可握手 |
| 沙箱 network_access | 存在 `network_access = true` |
| GIT_EXEC_PATH 注入 | 已写入双目录值 |
| CODEX_HOME/.env | 含当前端口代理变量 |
| 用户级环境变量 | `HKCU\Environment` 中代理指向当前端口 |
| git TLS 后端 | `http.sslBackend = openssl` |
| 当前进程代理环境 | 当前进程已加载代理变量（未加载会提示需重启） |
| 回环地址保护 | `NO_PROXY` 含 `127.0.0.1` |

## 4. 回滚

- 点 **【还原备份】**：把 `config.toml` / `.env` / `.gitconfig` 恢复为最近一次修复前的状态；工具新建的文件会被删除
- 点 **【清除变量】**：删除用户级代理环境变量
- 备份文件命名：`*.bak-codexfix-YYYYMMDD-HHMMSS`

## 5. 目录与配置

| 内容 | 路径 |
|---|---|
| 设置 | `%APPDATA%\CodexNetFix\settings.json` |
| 修复历史 | `%APPDATA%\CodexNetFix\history.log` |
| 异常日志 | `%APPDATA%\CodexNetFix\error.log` |
| Codex 配置 | `%USERPROFILE%\.codex\config.toml`、`.env` |
| git 配置 | `%USERPROFILE%\.gitconfig` |

## 6. 命令行

```powershell
--cli version / detect / repair [端口] / check [端口] / e2e / rollback
--cli restart-list / restart --yes / report [文件] / icon [文件] / preview [文件]
```

## 7. 自动化验收

```powershell
powershell -ExecutionPolicy Bypass -File tools\selftest.ps1 -TestGitConfig
```

流程：隔离测试环境 → 删除工具生成的文件 → 自动探测 → 一键修复重建 → 全面自检 →
**端到端真实联网**（沙箱内 git / HTTPS / 本地服务）→ 二次重建 → 还原 / 负向测试 / 图标 / 报告。
结束自动还原 `HKCU\Environment` 与 `.gitconfig` 并清理测试目录。

退出码：`0` 全部通过 · `1` 有失败项 · `2` 找不到主程序 · `3` 测试目录校验失败