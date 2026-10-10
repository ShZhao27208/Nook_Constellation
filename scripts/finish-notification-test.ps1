$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type @'
using System; using System.Runtime.InteropServices;
public class FinishNookTest { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l); }
'@
$root=Split-Path $PSScriptRoot -Parent
$test=Get-Content (Join-Path $root '.local\notification-test-process.json') -Raw | ConvertFrom-Json
$p=Get-Process -Id $test.processId
if ($p.Path -ne (Join-Path $root 'artifacts\dev\Nook.Constellation.exe')) { throw 'Test process path mismatch' }
$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,[int]$p.Id)
$window=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
[FinishNookTest]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,0x8002,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
if (!$p.WaitForExit(10000)) { throw 'Graceful exit failed' }
Copy-Item -LiteralPath (Join-Path $test.dataDirectory 'tasks.db') -Destination (Join-Path $root '.local\notification-test.db')
Write-Output 'Notification test exited cleanly.'
