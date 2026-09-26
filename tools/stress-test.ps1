<#
    Codex 网络修复工具 · 终极/极端场景测试 (stress-test.ps1)
    覆盖：正常流程、异常输入、权限问题、损坏配置、无 git、路径含空格中文、
          幂等性、备份回滚、历史损坏、无备份回滚、报告/图标等
    安全：结束自动还原 HKCU\Environment、.gitconfig、history.log，并清理测试目录
#>
param([switch]$KeepArtifacts)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe  = Join-Path $root 'Codex网络修复工具.exe'
$work = Join-Path $root '_stress'
$pass = 0; $fail = 0
$results = New-Object System.Collections.ArrayList

function T($name, [bool]$ok, $detail) {
    if ($ok) { $script:pass++; Write-Host ("  [PASS] " + $name + "  " + $detail) -ForegroundColor Green }
    else     { $script:fail++; Write-Host ("  [FAIL] " + $name + "  " + $detail) -ForegroundColor Red }
    [void]$results.Add([pscustomobject]@{ 用例 = $name; 结果 = $(if ($ok) { 'PASS' } else { 'FAIL' }); 说明 = "$detail" })
}
function Sec($t) { Write-Host ""; Write-Host ("=== " + $t + " ===") -ForegroundColor Cyan }
function Run($homeDir, [string[]]$cliArgs) {
    $old = $env:CODEX_HOME
    if ($homeDir) { $env:CODEX_HOME = $homeDir } 
    $out = (& $exe @cliArgs 2>&1) -join "`n"
    if ($homeDir) { if ($old) { $env:CODEX_HOME = $old } else { Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue } }
    return $out
}

if (-not (Test-Path $exe)) { Write-Host "找不到主程序: $exe" -ForegroundColor Red; exit 2 }
if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $work | Out-Null
Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue

# ---- 现场快照 ----
$envSnap = @{}
$rk = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
if ($rk) { foreach ($n in $rk.GetValueNames()) { $envSnap[$n] = $rk.GetValue($n, $null, 'DoNotExpandEnvironmentNames') }; $rk.Close() }
$gitCfg = Join-Path $env:USERPROFILE '.gitconfig'
$gitCfgExisted = Test-Path $gitCfg
$gitCfgOrig = if ($gitCfgExisted) { Get-Content $gitCfg -Raw } else { $null }
$histFile = Join-Path $env:APPDATA 'CodexNetFix\history.log'
$histExisted = Test-Path $histFile
$histOrig = if ($histExisted) { Get-Content $histFile -Raw } else { $null }
$restored = $false
function Restore-All {
    if ($script:restored) { return }
    $script:restored = $true
    try {
        $k = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
        if ($k) {
            foreach ($n in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','NO_PROXY','NODE_USE_ENV_PROXY','GIT_EXEC_PATH','CodexNetFixProbe')) {
                if (-not $script:envSnap.ContainsKey($n)) { $k.DeleteValue($n, $false) }
            }
            foreach ($kv in $script:envSnap.GetEnumerator()) { $k.SetValue($kv.Key, $kv.Value, 'String') }
            $k.Close()
        }
    } catch { }
    try {
        if ($script:gitCfgExisted) { Set-Content -Path $script:gitCfg -Value $script:gitCfgOrig -Encoding UTF8 -NoNewline }
        elseif (Test-Path $script:gitCfg) { Remove-Item $script:gitCfg -Force }
    } catch { }
    try {
        if ($script:histExisted) { Set-Content -Path $script:histFile -Value $script:histOrig -Encoding UTF8 -NoNewline }
        elseif (Test-Path $script:histFile) { Remove-Item $script:histFile -Force }
    } catch { }
}

try {
    Sec "1. 基础功能"
    $v = Run $null @('--cli','version')
    T "版本号输出" ($v -match 'VERSION=v1\.1\.125') ($v.Trim())

    $d = Run $null @('--cli','detect')
    $port = 0
    $m = [regex]::Match($d, 'PORT=(\d+)'); if ($m.Success) { $port = [int]$m.Groups[1].Value }
    T "自动探测代理端口" ($port -gt 0) ("PORT=" + $port)

    $rl = Run $null @('--cli','restart-list')
    T "进程枚举(restart-list)" ($rl -match 'COUNT=\d+') (($rl -split "`n" | Select-Object -Last 1))

    $nr = Run $null @('--cli','restart')
    T "restart 未加 --yes 时被拒绝" ($nr -match '需要 --yes') ""

    Sec "2. 全新机器：修复应创建全部文件"
    $h1 = "$work\fresh"
    New-Item -ItemType Directory -Force -Path $h1 | Out-Null
    $r1 = Run $h1 @('--cli','repair',"$port")
    T "修复成功" ($r1 -match 'RESULT=OK') (($r1 -split "`n" | Select-Object -Last 1))
    $cfg1 = Join-Path $h1 'config.toml'; $env1 = Join-Path $h1 '.env'
    T "config.toml 已创建" (Test-Path $cfg1) ("$((Get-Item $cfg1 -ErrorAction SilentlyContinue).Length) 字节")
    T ".env 已创建" (Test-Path $env1) ""
    $c1 = if (Test-Path $cfg1) { Get-Content $cfg1 -Raw } else { '' }
    T "含 shell_environment_policy.set" ($c1 -match '\[shell_environment_policy\.set\]') ""
    T "含网络开关 network_access" ($c1 -match 'network_access\s*=\s*true') ""
    T "含 GIT_EXEC_PATH" ($c1 -match 'GIT_EXEC_PATH') ""

    Sec "3. 幂等性：连续修复不应产生重复段落"
    $before = if (Test-Path $cfg1) { (Get-FileHash $cfg1 -Algorithm SHA256).Hash } else { '' }
    $r2 = Run $h1 @('--cli','repair',"$port")
    $after = (Get-FileHash $cfg1 -Algorithm SHA256).Hash
    T "二次修复配置内容一致" ($before -eq $after -and $before -ne '') ""
    $sections = ([regex]::Matches((Get-Content $cfg1 -Raw), '\[shell_environment_policy\.set\]')).Count
    T "代理段落未重复" ($sections -eq 1) ("出现 " + $sections + " 次")

    Sec "4. 已有用户配置必须保留"
    $h2 = "$work\existing"
    New-Item -ItemType Directory -Force -Path $h2 | Out-Null
    Set-Content (Join-Path $h2 'config.toml') -Encoding UTF8 -Value "model = `"gpt-5-codex`"`r`n`r`n[projects.'D:\work']`r`ntrust_level = `"trusted`"`r`n"
    $null = Run $h2 @('--cli','repair',"$port")
    $c2 = Get-Content (Join-Path $h2 'config.toml') -Raw
    T "保留 model 配置" ($c2 -match 'gpt-5-codex') ""
    T "保留 projects 配置" ($c2 -match "\[projects\.'D:\\work'\]") ""
    T "同时写入代理段落" ($c2 -match 'shell_environment_policy\.set') ""

    Sec "5. 异常输入与错误处理"
    $bad = "$work\badport"
    New-Item -ItemType Directory -Force -Path $bad | Out-Null
    $rb = Run $bad @('--cli','repair','9')
    T "无效端口被拒绝" ($rb -match 'RESULT=FAILED') ""
    $rb2 = Run $bad @('--cli','repair','abc')
    T "非数字端口不崩溃" ($rb2 -match 'RESULT=') (($rb2 -split "`n" | Select-Object -Last 1))
    $rb3 = Run $bad @('--cli','repair','70000')
    T "超范围端口不崩溃" ($rb3 -match 'RESULT=') ""

    Sec "6. 只读配置：应报错但不崩溃"
    $ro = "$work\readonly"
    New-Item -ItemType Directory -Force -Path $ro | Out-Null
    $roc = Join-Path $ro 'config.toml'
    Set-Content $roc -Encoding UTF8 -Value "model = `"x`""
    Set-ItemProperty $roc -Name IsReadOnly -Value $true
    $rro = Run $ro @('--cli','repair',"$port")
    $roOk = ($rro -match 'RESULT=') -and ($rro -match '失败|OK')
    T "只读文件时优雅处理" $roOk ""
    Set-ItemProperty $roc -Name IsReadOnly -Value $false

    Sec "7. 损坏的 TOML：不应崩溃"
    $cr = "$work\corrupt"
    New-Item -ItemType Directory -Force -Path $cr | Out-Null
    Set-Content (Join-Path $cr 'config.toml') -Encoding UTF8 -Value "[[[broken`n= = =`n这不是合法 TOML`n"
    $rc = Run $cr @('--cli','repair',"$port")
    T "损坏配置仍能完成修复" ($rc -match 'RESULT=OK') ""
    T "已追加代理段落" ((Get-Content (Join-Path $cr 'config.toml') -Raw) -match 'shell_environment_policy\.set') ""

    Sec "8. 路径含空格与中文"
    $uni = "$work\测试 目录 Chinese Path"
    New-Item -ItemType Directory -Force -Path $uni | Out-Null
    $ru = Run $uni @('--cli','repair',"$port")
    T "中文/空格路径修复成功" ($ru -match 'RESULT=OK') ""
    T "文件生成正确" (Test-Path (Join-Path $uni 'config.toml')) ""

    Sec "9. 备份与回滚"
    $h3 = "$work\rollback"
    New-Item -ItemType Directory -Force -Path $h3 | Out-Null
    Set-Content (Join-Path $h3 'config.toml') -Encoding UTF8 -Value "model = `"keepme`""
    Set-Content (Join-Path $h3 '.env') -Encoding UTF8 -Value "MY_OWN_VAR=1"
    $null = Run $h3 @('--cli','repair',"$port")
    $baks = Get-ChildItem $h3 -Filter '*.bak-codexfix-*'
    T "生成了备份文件" ($baks.Count -ge 2) ("" + $baks.Count + " 个")
    $null = Run $h3 @('--cli','rollback')
    $c3 = Get-Content (Join-Path $h3 'config.toml') -Raw
    $e3 = Get-Content (Join-Path $h3 '.env') -Raw
    T "config.toml 已还原" ($c3 -match 'keepme') ""
    T ".env 已还原" ($e3 -match 'MY_OWN_VAR') ""
    $h4 = "$work\nobackup"
    New-Item -ItemType Directory -Force -Path $h4 | Out-Null
    $r4 = Run $h4 @('--cli','rollback')
    T "无备份时回滚不崩溃" ($r4 -match 'RESULT=ROLLED_BACK') ""

    Sec "10. 无 git 环境"
    $h5 = "$work\nogit"
    New-Item -ItemType Directory -Force -Path $h5 | Out-Null
    $savedPath = $env:PATH
    $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notmatch 'git' }) -join ';'
    $r5 = Run $h5 @('--cli','repair',"$port")
    $env:PATH = $savedPath
    T "无 git 时仍完成修复" ($r5 -match 'RESULT=OK') ""
    T "配置已生成" (Test-Path (Join-Path $h5 'config.toml')) ""

    Sec "11. 历史记录损坏"
    New-Item -ItemType Directory -Force -Path (Split-Path $histFile) | Out-Null
    Add-Content $histFile -Encoding UTF8 -Value "garbage line without pipes`n|||`n2026-01-01 00:00:00 | 测试 | 通过 | 说明"
    $rd = Run $null @('--cli','check')
    T "历史损坏不影响自检" ($rd -match '\[OK\]') ""
    T "历史文件仍可读" (Test-Path $histFile) ""

    Sec "12. 报告 / 图标 / 预览"
    $rep = "$work\out-report.txt"
    $rr = Run $null @('--cli','report',$rep)
    T "导出诊断报告" (Test-Path $rep) ("$((Get-Item $rep -ErrorAction SilentlyContinue).Length) 字节")
    $ico = "$work\out.ico"
    Start-Process -FilePath $exe -ArgumentList @('--cli','icon',$ico) -Wait -NoNewWindow
    T "生成图标" (Test-Path $ico) ("$((Get-Item $ico -ErrorAction SilentlyContinue).Length) 字节")
    $png = "$work\out.png"
    Start-Process -FilePath $exe -ArgumentList @('--cli','preview',$png) -Wait -NoNewWindow
    T "生成图标预览" (Test-Path $png) ""

    Sec "13. 端到端真实联网（真实 CODEX_HOME）"
    $savedHome = $env:CODEX_HOME
    Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue
    $e2e = (& $exe --cli e2e 2>&1) -join "`n"
    if ($savedHome) { $env:CODEX_HOME = $savedHome }
    Write-Host ("  " + ($e2e -replace "`r?`n", "`n  ")) -ForegroundColor DarkGray
    T "沙箱继承代理变量" ($e2e -match 'HTTP_PROXY=http://127\.0\.0\.1:\d+') ""
    T "沙箱内 git 可用" ($e2e -match 'GIT=OK') ""
    T "沙箱内 HTTPS 可达" ($e2e -match 'NODE=\d{3}') ""
    T "本地服务未被劫持" ($e2e -match 'RELAY=200') ""
}
finally {
    Restore-All
    if (-not $KeepArtifacts) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}
Sec "测试汇总"
$color = if ($fail -eq 0) { 'Green' } else { 'Red' }
Write-Host ("  通过 $pass 项 / 失败 $fail 项") -ForegroundColor $color
$results | Format-Table -AutoSize
if ($fail -eq 0) { exit 0 } else { exit 1 }
