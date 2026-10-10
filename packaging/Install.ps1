param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$destination = 'E:\NookConstellation'
$source = Join-Path $PSScriptRoot 'app'
if (!(Test-Path (Join-Path $source 'Nook.Constellation.exe'))) { throw 'The app folder is missing. Extract the complete ZIP before installing.' }
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class NookInstallerNative {
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
}
'@
$processes = @(Get-Process -Name Nook.Constellation -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $destination 'Nook.Constellation.exe') })
if ($processes.Count -gt 0) {
 $h = [NookInstallerNative]::FindWindow($null, '隅星集 · Nook Constellation')
 if ($h -ne [IntPtr]::Zero) { [NookInstallerNative]::PostMessage($h, 0x8002, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
 foreach ($p in $processes) { if (!$p.WaitForExit(10000)) { throw 'Please exit Nook Constellation from its tray menu, then run the installer again.' } }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination $destination -Force
$exe = Join-Path $destination 'Nook.Constellation.exe'
$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
 $shortcut = $shell.CreateShortcut((Join-Path $folder '隅星集.lnk'))
 $shortcut.TargetPath = $exe; $shortcut.WorkingDirectory = $destination; $shortcut.IconLocation = "$exe,0"; $shortcut.Description = '隅星集 · Nook Constellation'; $shortcut.Save()
}
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NookConstellation'
New-Item -Path $key -Force | Out-Null
$values = @{ DisplayName='隅星集 · Nook Constellation'; DisplayVersion='1.0.3'; Publisher='Nook Constellation'; InstallLocation=$destination; DisplayIcon=$exe; UninstallString=('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $destination 'Uninstall.ps1') + '"') }
foreach ($name in $values.Keys) { New-ItemProperty -Path $key -Name $name -Value $values[$name] -PropertyType String -Force | Out-Null }
Write-Output "Installed: $exe"
if (!$NoLaunch) { Start-Process -FilePath $exe }
