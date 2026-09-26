# GitHub 发布信息 / Publish Kit

本文件提供直接可用的仓库信息，复制粘贴即可发布。

---

## 1. 仓库名称建议

| 类型 | 建议 |
|---|---|
| 首选 | `Codex-Net-Universal-Repair-Toolbox` |
| 备选 | `CodexNetworkFixTool` / `codex-net-fix` / `codex-windows-network-fix` |

## 2. About（一句话简介，256 字符内）

```
一键修复 Codex / ChatGPT 桌面版的网络连接问题：自动探测本机代理、覆盖沙箱黑洞代理、修复 git，并提供全面自检与一键回滚。Windows 单文件绿色工具，零依赖。
```

英文版：

```
One-click network repair for Codex / ChatGPT desktop on Windows: detects your local proxy, overrides the sandbox black-hole proxy, fixes git, with full self-check and one-click rollback. Single-file, zero dependencies.
```

## 3. Topics（标签）

```
codex  chatgpt  openai  proxy  clash  windows  sandbox  network-troubleshooting  git  csharp  winforms  dotnet-framework  china-network  developer-tools
```

## 4. 详细描述（可直接作为 README 顶部或 Release 说明）

**痛点**：在 Windows 上，Codex 沙箱默认禁止非回环网络访问，并会注入黑洞代理 `HTTP_PROXY=http://127.0.0.1:9`，导致 Codex 内执行命令时全部断网；同时 `.env` 里的代理不会覆盖已存在的环境变量，手工排查非常耗时。

**方案**：本工具自动探测本机可用代理端口（TCP + `CONNECT` 握手 + 真实出网验证），写入 `[shell_environment_policy.set]` 覆盖黑洞代理，注入 `GIT_EXEC_PATH` 并把 git TLS 切到 openssl，最后提供 11 项自检、一键回滚，以及代理启动、测速、快照和反馈包工具。

**特点**：
- 零依赖单文件 EXE，免安装、免管理员
- 所有改动前自动备份，可完整还原
- PCL 风格现代界面、四种主题色、托盘代理监控、修复历史
- 更多工具：启动代理软件、一键全流程、代理测速、配置快照、反馈包、端口与时间诊断
- 完整 CLI，可脚本化批量部署

## 5. 首次发布步骤

```bash
# 在“开源文件”目录下
git init
git add .
git commit -m "feat: release v1.0.225"
git branch -M main
git remote add origin https://github.com/<你的用户名>/Codex-Net-Universal-Repair-Toolbox.git
git push -u origin main
```

发布 Release：

1. 本地构建：`powershell -ExecutionPolicy Bypass -File src\build.ps1`
2. 在 GitHub → Releases → Draft a new release
3. Tag 填 `v1.0.225`，标题填 `v1.0.225`
4. 把构建出的 `Codex网络修复工具.exe` 作为附件上传
5. 说明可直接复制下方模板

> 也可以直接推一个 tag，仓库内置的 GitHub Actions（`.github/workflows/build.yml`）会自动构建并把 EXE 附加到 Release。

## 6. Release 说明模板

```markdown
## v1.0.225

### 本次更新
- 新增「更多」分区：启动代理软件、一键全流程、代理测速、配置快照、反馈包、端口与时间诊断
- 新增 Ctrl+F / Ctrl+T / Ctrl+R / F5 / Ctrl+M 快捷键
- 新增命令行诊断入口
- 修复窗口标题拖动和系统时间检查兼容性

### 下载
- `Codex网络修复工具.exe`（单文件，免安装）
- 校验：Windows 10/11 + .NET Framework 4.8

### 快速开始
1. 启动代理软件，确认节点可用
2. 双击运行本工具 → 点【一键修复并部署】
3. 点【重启 Codex】→ 点【全面自检】，全部 OK 即完成

> 完整日志见 CHANGELOG.md
```

## 7. 仓库设置建议

| 项目 | 建议 |
|---|---|
| Social preview（社交预览图）| 用 `assets/screenshots/ui-repair.png` |
| Description | 用第 2 节文案 |
| Topics | 用第 3 节标签 |
| License | MIT |
| Default branch | `main` |
| Releases | 每个正式版上传 EXE，并附第 6 节模板 |

## 8. 发布前检查清单

- [ ] 截图与日志中已脱敏（用户名、代理地址、订阅链接、端口等）
- [ ] `settings.json` / `history.log` / `*.exe` 未被提交（已在 `.gitignore` 中排除）
- [ ] `src/build.ps1` 在干净环境可构建成功
- [ ] `README.md` 中的截图路径与实际文件一致
- [ ] `CHANGELOG.md` 已更新
- [ ] `LICENSE` 中的版权人已改为你的名字或组织名

## 9. 免责声明（可放在 README 末尾或 Release 说明）

本工具会修改当前用户的配置文件（`~\.codex\config.toml`、`~\.codex\.env`、`~\.gitconfig`、`HKCU\Environment`），
所有改动前均会生成备份。请在使用前确认你了解上述改动；因代理软件、网络环境差异导致的连接问题与本工具无关。
