$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$outDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$avatar = Join-Path $root "assets\avatar-round.png"
if (-not (Test-Path $avatar)) { $avatar = Join-Path $root "assets\avatar.png" }

$refs = @(
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Net.Http.dll"
)

$sources = @(
  "Program.cs",
  "Theme.cs",
  "DisclaimerForm.cs",
  "MainForm.cs",
  "InstallerForm.cs",
  "BridgeHost.cs",
  "AdbRunner.cs"
)

function Build-Exe([string]$outName, [string[]]$extraDefine) {
  $outExe = Join-Path $outDir $outName
  $args = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/utf8output",
    "/win32icon:app.ico",
    "/resource:app.ico,GjBridge.app.ico",
    "/out:$outExe"
  ) + $refs
  if (Test-Path $avatar) { $args += "/resource:$avatar,GjBridge.avatar.png" }
  $args += $sources
  Write-Host "Compiling $outName ..."
  # csc 警告会混进函数返回值，必须丢掉流水线输出
  $cscOut = & $csc @args 2>&1
  foreach ($line in $cscOut) { Write-Host $line }
  if ($LASTEXITCODE -ne 0) { throw "csc failed for $outName : $LASTEXITCODE" }
  if (-not (Test-Path $outExe)) { throw "missing output $outExe" }
  return $outExe
}

# 1) 便携版（双击即用）
$portable = [string](Build-Exe "搞机工具箱-本机桥接.exe" | Select-Object -Last 1)
if (-not (Test-Path $portable)) { throw "portable build missing: $portable" }
Copy-Item -LiteralPath $portable -Destination (Join-Path $outDir "gj-adb-bridge.exe") -Force

# 2) 安装包：同二进制，靠文件名触发安装向导；旁边必须有便携 exe 供复制
$setup = Join-Path $outDir "搞机工具箱-本机桥接-安装程序.exe"
Copy-Item -LiteralPath $portable -Destination $setup -Force

Copy-Item "app.ico" (Join-Path $outDir "app.ico") -Force
if (Test-Path $avatar) { Copy-Item $avatar (Join-Path $outDir "avatar-round.png") -Force }

# 简易说明
@"
便携版：搞机工具箱-本机桥接.exe（或 gj-adb-bridge.exe）
  - 双击即可，无需安装
  - 首次需「同意」免责声明，不同意会退出

安装版：搞机工具箱-本机桥接-安装程序.exe
  - 安装到本机目录，创建开始菜单/桌面快捷方式
  - 安装完成后可从开始菜单随时打开（会启动桥接并打开网站）

网站：https://sj.aikex.ink/
开源：https://github.com/aiyangdie/gj-adb-bridge
"@ | Set-Content (Join-Path $outDir "使用说明.txt") -Encoding UTF8

Get-ChildItem $outDir | Format-Table Name, Length, LastWriteTime
Write-Host "BUILD_OK"
