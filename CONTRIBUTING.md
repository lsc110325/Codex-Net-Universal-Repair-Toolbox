# 贡献指南 / Contributing

感谢你愿意为本项目做出贡献！

## 提交 Issue

请尽量附上：

1. 操作系统版本（`winver`）与 Codex 版本
2. **诊断报告**（工具内【导出诊断报告】或 `--cli report out.txt`）
3. 复现步骤与期望结果
4. 相关错误信息（如 `502` / `TLS EOF` / `SEC_E_NO_CREDENTIALS` 等）

> 提示：自检失败时，探针输出里的 `502`、`TLS EOF` 通常是**代理节点掉线**，不是工具问题，请先更换节点再复测。

## 提交 Pull Request

1. Fork 本仓库并创建分支：`git checkout -b feature/your-feature`
2. 修改源码（`src/`），保持现有代码风格（C# 5 语法，兼容 .NET Framework 4.8）
3. 本地构建：
   ```powershell
   powershell -ExecutionPolicy Bypass -File src\build.ps1
   ```
4. 运行自动化验收（可选，会临时修改并自动还原 .gitconfig / 环境变量）：
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\selftest.ps1 -TestGitConfig
   ```
5. 提交并推送，然后在 GitHub 上发起 PR，说明改动动机与验证方式

## 代码约定

- **兼容性**：只能使用 C# 5 语法（无字符串插值、无 `?.`、无表达式主体成员），目标 .NET Framework 4.8
- **界面**：自绘控件请使用 `Draw.EffectiveBack()` 取父级不透明背景，避免出现黑色描边
- **文件修改**：任何对用户文件的修改都必须先备份（`*.bak-codexfix-时间戳`）
- **日志**：新增操作请通过 `History.Add()` 记录（重复项会自动合并）

## 安全与隐私

- 本工具只修改**当前用户**的配置文件，不修改系统目录、不写 `HKLM`、不安装服务
- 提交日志或截图前，请先脱敏（用户名、代理地址、订阅链接等）