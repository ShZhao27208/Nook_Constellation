$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$oldPath=Join-Path $root 'src\Nook.Constellation\bin\Release\net8.0-windows\Nook.Constellation.exe'
# Only stop the original empty preview started during development, never arbitrary user applications.
Get-Process -Name Nook.Constellation -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $oldPath } | Stop-Process
$exe=Join-Path $env:LOCALAPPDATA 'Programs\NookConstellation\Nook.Constellation.exe'
$p=Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 3
$p.Refresh()
if ($p.HasExited) { throw 'Installed application exited unexpectedly' }
$startup=(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').NookConstellation
if ($startup -ne ('"'+$exe+'"')) { throw 'Startup registry path is not quoted correctly' }
$desktopShortcut=Join-Path ([Environment]::GetFolderPath('Desktop')) '隅星集.lnk'
if (!(Test-Path $desktopShortcut)) { throw 'Desktop shortcut missing' }
@{passed=$true;exe=$exe;pid=$p.Id;startup=$startup;shortcut=$desktopShortcut} | ConvertTo-Json | Set-Content (Join-Path $root '.local\install-test-results.json') -Encoding UTF8
Get-Content (Join-Path $root '.local\install-test-results.json') -Encoding UTF8
