param()
$ErrorActionPreference = 'Stop'
# 目录结构：<主文件夹>/src（代码） · /gallery（图库） · /tools（脚本） · 根目录放本体 exe
$src  = Split-Path -Parent $MyInvocation.MyCommand.Path
$main = Split-Path -Parent $src
$exe  = Join-Path $main 'Codex网络修复工具.exe'
$ico  = Join-Path $main 'gallery\app.ico'
$png  = Join-Path $main 'gallery\icon-preview.png'
if (-not (Test-Path $ico)) {
    $ico = Join-Path $main 'assets\app.ico'
    $png = Join-Path $main 'assets\icon-preview.png'
}
$csc  = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/r:System.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }
$buildTemp = $src
$env:TEMP = $buildTemp
$env:TMP  = $buildTemp

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

Get-Item $exe, $ico, $png | Select-Object Name, @{n='KB';e={[math]::Round($_.Length/1KB,1)}}, LastWriteTime | Format-Table -AutoSize
Write-Host ('BUILD OK -> ' + $exe)
