param()
$ErrorActionPreference = 'Stop'
# 仓库结构：<repo>/src（源码） · <repo>/assets（图标） · 根目录输出 exe
$src  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $src
$exe  = Join-Path $repo 'Codex网络修复工具.exe'
$ico  = Join-Path $repo 'assets\app.ico'
$png  = Join-Path $repo 'assets\icon-preview.png'
$csc  = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$refs = @('/r:System.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }

Write-Host '== 第 1 遍：编译（CLI 版） =='
& $csc /nologo /target:exe /platform:anycpu /langversion:5 /codepage:65001 /utf8output @refs /out:$exe @files
if ($LASTEXITCODE -ne 0) { throw "编译失败: $LASTEXITCODE" }

Write-Host '== 生成图标 =='
Start-Process -FilePath $exe -ArgumentList @('--cli','icon',$ico) -Wait -NoNewWindow

Write-Host '== 第 2 遍：带图标编译 =='
& $csc /nologo /target:exe /platform:anycpu /langversion:5 /codepage:65001 /utf8output /win32icon:$ico @refs /out:$exe @files
if ($LASTEXITCODE -ne 0) { throw "最终编译失败: $LASTEXITCODE" }

Write-Host '== 生成图标预览 =='
Start-Process -FilePath $exe -ArgumentList @('--cli','preview',$png) -Wait -NoNewWindow

Get-Item $exe, $ico, $png | Select-Object Name, @{n='KB';e={[math]::Round($_.Length/1KB,1)}} | Format-Table -AutoSize
Write-Host ('BUILD OK -> ' + $exe)