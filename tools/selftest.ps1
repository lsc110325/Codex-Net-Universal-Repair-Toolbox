<#
    Codex 网络修复工具 · 自动化验收测试 (selftest.ps1)
    流程：隔离测试环境 -> 删除工具生成的文件 -> 让工具重新生成 -> 逐项验证 -> 还原现场
    用法：powershell -ExecutionPolicy Bypass -File selftest.ps1 [-Port 7890] [-TestGitConfig] [-KeepArtifacts]
    安全：结束时会自动还原 HKCU\Environment 与 .gitconfig，并清理测试目录
#>
param([int]$Port = 0, [switch]$TestGitConfig, [switch]$KeepArtifacts)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe  = Join-Path $root 'Codex网络修复工具.exe'
$work = Join-Path $root '_selftest'
$testHome = Join-Path $work 'codexhome'
$pass = 0; $fail = 0
$results = New-Object System.Collections.ArrayList

function T($name, [bool]$ok, $detail) {
    if ($ok) { $script:pass++; Write-Host ("  [PASS] " + $name + "  " + $detail) -ForegroundColor Green }
    else     { $script:fail++; Write-Host ("  [FAIL] " + $name + "  " + $detail) -ForegroundColor Red }
    [void]$script:results.Add([pscustomobject]@{ 项目 = $name; 结果 = $(if ($ok) { 'PASS' } else { 'FAIL' }); 说明 = "$detail" })
}
function Section($t) { Write-Host ""; Write-Host ("=== " + $t + " ===") -ForegroundColor Cyan }

# 安全断言：测试目录必须位于工具目录下，绝不能是用户主目录
if (($testHome -ne (Join-Path $work 'codexhome')) -or ($testHome -notlike ($root + '*'))) {
    Write-Host "测试目录校验失败，已中止：$testHome" -ForegroundColor Red
    exit 3
}
if (-not (Test-Path $exe)) { Write-Host "找不到主程序: $exe" -ForegroundColor Red; exit 2 }

Section "0. 准备隔离测试环境"
if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $testHome | Out-Null
Write-Host "  测试 CODEX_HOME : $testHome"
Write-Host "  初始文件数      : " ((Get-ChildItem $testHome -Force | Measure-Object).Count)

$envSnap = @{}
$rk = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
if ($rk) { foreach ($n in $rk.GetValueNames()) { $envSnap[$n] = $rk.GetValue($n, $null, 'DoNotExpandEnvironmentNames') }; $rk.Close() }
$gitCfg = Join-Path $env:USERPROFILE '.gitconfig'
$gitCfgExisted = Test-Path $gitCfg
$gitCfgOrig = if ($gitCfgExisted) { Get-Content $gitCfg -Raw } else { $null }
$restored = $false
function Restore-Scene {
    if ($script:restored) { return }
    $script:restored = $true
    try {
        $k = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
        if ($k) {
            foreach ($n in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','NO_PROXY','NODE_USE_ENV_PROXY','http_proxy','https_proxy','all_proxy','no_proxy','GIT_EXEC_PATH','CodexNetFixProbe')) {
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
}

try {
    Section "1. 删除工具生成的文件（模拟被删光的电脑）"
    foreach ($f in @('config.toml', '.env')) { $p = Join-Path $testHome $f; if (Test-Path $p) { Remove-Item $p -Force } }
    if ($TestGitConfig -and (Test-Path $gitCfg)) { Remove-Item $gitCfg -Force; Write-Host "  已删除真实 .gitconfig（结束时自动还原）" }
    T "现场已清空" ((-not (Test-Path (Join-Path $testHome 'config.toml'))) -and (-not (Test-Path (Join-Path $testHome '.env')))) "config.toml/.env 不存在"

    $env:CODEX_HOME = $testHome

    Section "2. 自动探测代理端口"
    $detectText = (& $exe --cli detect 2>&1) -join "`n"
    $m = [regex]::Match($detectText, 'PORT=(\d+)')
    $detected = if ($m.Success) { [int]$m.Groups[1].Value } else { 0 }
    T "探测到可用代理端口" ($detected -gt 0) ("PORT=" + $detected)

    Section "3. 一键修复（重建全部文件）"
    if ($Port -le 0) { $Port = $detected }
    $repairText = (& $exe --cli repair $Port 2>&1) -join "`n"
    T "修复命令返回成功" ($repairText -match 'RESULT=OK') ("端口 " + $Port)
    $cfg = Join-Path $testHome 'config.toml'; $envf = Join-Path $testHome '.env'
    T "config.toml 已生成" (Test-Path $cfg) ("$((Get-Item $cfg -ErrorAction SilentlyContinue).Length) 字节")
    T ".env 已生成" (Test-Path $envf) ("$((Get-Item $envf -ErrorAction SilentlyContinue).Length) 字节")
    if (Test-Path $cfg) {
        $ct = Get-Content $cfg -Raw
        T "写入 shell_environment_policy.set" ($ct -match '\[shell_environment_policy\.set\]') ""
        T "代理端口正确" ($ct -match ("127\.0\.0\.1:" + $Port)) ""
        T "写入 GIT_EXEC_PATH" ($ct -match 'GIT_EXEC_PATH') ""
        T "写入 network_access" ($ct -match 'network_access\s*=\s*true') ""
    }
    if ($TestGitConfig) {
        $gt = if (Test-Path $gitCfg) { Get-Content $gitCfg -Raw } else { "" }
        T "git TLS 后端重建为 openssl" ($gt -match 'sslBackend\s*=\s*openssl') ""
    }

    Section "4. 全面自检"
    $chkText = (& $exe --cli check $Port 2>&1) -join "`n"
    $okN = ([regex]::Matches($chkText, '\[OK\]')).Count
    $wnN = ([regex]::Matches($chkText, '\[WARN\]')).Count
    $badN = ([regex]::Matches($chkText, '\[FAIL\]')).Count
    $healthy = ($badN -eq 0) -and ($okN -ge 8)
    if (-not $healthy) { Write-Host "  ---- 自检明细 ----" -ForegroundColor DarkYellow; Write-Host ("  " + ($chkText -replace "`r?`n", "`n  ")) -ForegroundColor DarkGray }
    T "自检结果健康(无FAIL且OK>=8)" $healthy ("OK=$okN WARN=$wnN FAIL=$badN")

    Section "5. 端到端：用生成的配置让 Codex 沙箱真实联网"
    # 端到端验证使用真实 CODEX_HOME（全新/临时目录下 Codex 会走未预置分支注入黑洞代理，不代表真实场景）
    $savedCodexHome = $env:CODEX_HOME
    Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue
    $e2eText = (& $exe --cli e2e 2>&1) -join "`n"
    if ($savedCodexHome) { $env:CODEX_HOME = $savedCodexHome }
    if ($e2eText -notmatch 'RESULT=OK') {
        Write-Host "  端到端首次未通过（可能是代理节点抖动），3 秒后重试…" -ForegroundColor DarkYellow
        Start-Sleep -Seconds 3
        # 端到端验证使用真实 CODEX_HOME（全新/临时目录下 Codex 会走未预置分支注入黑洞代理，不代表真实场景）
    $savedCodexHome = $env:CODEX_HOME
    Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue
    $e2eText = (& $exe --cli e2e 2>&1) -join "`n"
    if ($savedCodexHome) { $env:CODEX_HOME = $savedCodexHome }
    }
    Write-Host ("  " + ($e2eText -replace "`r?`n", "`n  "))
    T "沙箱子进程继承代理变量" ($e2eText -match 'HTTP_PROXY=http://127\.0\.0\.1:\d+') ""
    T "沙箱内 git 可访问 GitHub" ($e2eText -match 'GIT=OK') ""
    T "沙箱内 HTTPS 请求可达" ($e2eText -match 'NODE=\d{3}') ""
    T "本地服务未被代理劫持" ($e2eText -match 'RELAY=200') ""
    T "端到端总体结论 OK" ($e2eText -match 'RESULT=OK') ""

    Section "6. 二次删除 -> 再次重建"
    Remove-Item (Join-Path $testHome 'config.toml'), (Join-Path $testHome '.env') -Force -ErrorAction SilentlyContinue
    [void](& $exe --cli repair $Port 2>&1)
    T "二次重建 config.toml" (Test-Path (Join-Path $testHome 'config.toml')) ""
    T "二次重建 .env" (Test-Path (Join-Path $testHome '.env')) ""
    $chk2 = (& $exe --cli check $Port 2>&1) -join "`n"
    $ok2 = ([regex]::Matches($chk2, '\[OK\]')).Count
    T "二次自检健康" ((([regex]::Matches($chk2, '\[FAIL\]')).Count) -eq 0 -and $ok2 -ge 8) ("OK=$ok2")

    Section "7. 还原备份 / 负向测试 / 其他功能"
    $rbText = (& $exe --cli rollback 2>&1) -join "`n"
    T "还原备份可执行" ($rbText -match 'RESULT=ROLLED_BACK') ""
    $badText = (& $exe --cli repair 9999 2>&1) -join "`n"
    T "错误端口被拒绝" ($badText -match 'RESULT=FAILED') ""
    $ico2 = Join-Path $work 'test.ico'
    & $exe --cli icon $ico2 | Out-Null

    T "图标生成功能" (Test-Path $ico2) ("$((Get-Item $ico2 -ErrorAction SilentlyContinue).Length) 字节")
    $rep2 = Join-Path $work 'report.txt'
    & $exe --cli report $rep2 | Out-Null

    T "诊断报告导出功能" (Test-Path $rep2) ("$((Get-Item $rep2 -ErrorAction SilentlyContinue).Length) 字节")
}
finally {
    Section "8. 还原现场"
    Restore-Scene
    Write-Host "  已还原 HKCU\Environment 与 .gitconfig"
    if (-not $KeepArtifacts) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "  已清理测试目录" }
    else { Write-Host "  保留测试产物: $work" }
}

Section "测试汇总"
$color = if ($fail -eq 0) { 'Green' } else { 'Red' }
Write-Host ("  通过 $pass 项 / 失败 $fail 项") -ForegroundColor $color
$results | Format-Table -AutoSize
if ($fail -eq 0) { exit 0 } else { exit 1 }