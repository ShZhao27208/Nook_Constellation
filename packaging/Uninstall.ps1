$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\NookConstellation'
if ($PSScriptRoot -ne $destination) { throw 'Run the installed uninstaller from Windows Settings.' }
$running = @(Get-Process -Name Nook.Constellation -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $destination 'Nook.Constellation.exe') })
if ($running.Count -gt 0) { Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('请先通过托盘退出隅星集，再卸载。', '隅星集') | Out-Null; exit 1 }
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name NookConstellation -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NookConstellation' -Recurse -ErrorAction SilentlyContinue
foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
 Remove-Item -LiteralPath (Join-Path $folder '隅星集.lnk') -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $destination -Recurse -Force
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.MessageBox]::Show('程序已卸载。任务数据和备份仍保存在 LocalAppData\NookConstellation。', '隅星集') | Out-Null
