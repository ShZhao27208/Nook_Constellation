$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'src\Nook.Constellation\bin\Release\net8.0-windows\Nook.Constellation.exe'
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 3
$process.Refresh()
if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)" }
$dir = Join-Path $root '.local'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save((Join-Path $dir 'desktop-smoke.png'))
} finally { $graphics.Dispose(); $bitmap.Dispose() }
Write-Output "RUNNING PID=$($process.Id)"
