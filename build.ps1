$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$outDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$exeName = "搞机工具箱-本机桥接.exe"
$outExe = Join-Path $outDir $exeName

$avatar = Join-Path $root "assets\avatar-round.png"
if (-not (Test-Path $avatar)) { $avatar = Join-Path $root "assets\avatar.png" }

$refs = @(
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Net.Http.dll"
)

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

if (Test-Path $avatar) {
  $args += "/resource:$avatar,GjBridge.avatar.png"
}

$args += @(
  "Program.cs",
  "MainForm.cs",
  "BridgeHost.cs",
  "AdbRunner.cs"
)

Write-Host "Compiling..."
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }

Copy-Item "app.ico" (Join-Path $outDir "app.ico") -Force
if (Test-Path $avatar) { Copy-Item $avatar (Join-Path $outDir "avatar-round.png") -Force }
Copy-Item $outExe (Join-Path $outDir "gj-adb-bridge.exe") -Force

Get-Item $outExe, (Join-Path $outDir "gj-adb-bridge.exe") | Format-Table Name, Length, LastWriteTime
Write-Host "BUILD_OK"
