$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class NookNative {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
'@
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'artifacts\dev\Nook.Constellation.exe'
$testRoot = Join-Path $env:TEMP ('NookUI-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$p = Start-Process -FilePath $exe -ArgumentList @('--data-dir', ('"' + $testRoot + '"')) -PassThru
Start-Sleep -Seconds 3
$p.Refresh()
if ($p.HasExited) { throw 'App did not start' }
$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$p.Id)
$main = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
if ($null -eq $main) { Stop-Process -Id $p.Id; throw 'No visible test window' }
$handle = [IntPtr]$main.Current.NativeWindowHandle
function Find-Element($parent, $name) {
 $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
 $el = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
 if ($null -eq $el) { throw "Element not found: $name" }
 return $el
}
function Click-Element($el) {
 $b = $el.Current.BoundingRectangle
 [NookNative]::SetCursorPos([int]($b.X+$b.Width/2),[int]($b.Y+$b.Height/2)) | Out-Null
 [NookNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
 [NookNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 400
}
function Screenshot($name) {
 Start-Sleep -Milliseconds 1400
 $b = $main.Current.BoundingRectangle
 $bitmap = New-Object System.Drawing.Bitmap([int]$b.Width,[int]$b.Height)
 $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
 try { $graphics.CopyFromScreen([int]$b.X,[int]$b.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $root ('.local\' + $name + '.png'))) } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
$checks = @()
try {
 [NookNative]::SetForegroundWindow($handle) | Out-Null
 Click-Element (Find-Element $main '新增任务')
 $desktop = [System.Windows.Automation.AutomationElement]::RootElement
 $editor = Find-Element $desktop '隅星集 · 新任务'
 $title = Find-Element $editor '任务标题'
 $title.SetFocus()
 [System.Windows.Forms.Clipboard]::SetText('Nook UI verified task')
 [System.Windows.Forms.SendKeys]::SendWait('^v')
 Start-Sleep -Milliseconds 500
 $pattern = $title.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
 if ($pattern.DocumentRange.GetText(-1).Trim() -ne 'Nook UI verified task') { throw 'Title text was not entered correctly' }
 [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
 Start-Sleep -Milliseconds 500
 $checks += 'Keyboard add task'
 Screenshot 'ui-small'
 Click-Element (Find-Element $main '放大或还原')
 Screenshot 'ui-expanded'
 $checks += 'Expand anchored window'
 Click-Element (Find-Element $main '开始做')
 Find-Element $main '暂停' | Out-Null
 $checks += 'Start task'
 Click-Element (Find-Element $main '完成')
 $checks += 'Complete task'
 Click-Element (Find-Element $main '设置')
 $settings = Find-Element $desktop '隅星集 · 设置与数据'
 Find-Element $settings '开机启动' | Out-Null
 Find-Element $settings '导出 JSON' | Out-Null
 $checks += 'Settings and data controls'
 [System.Windows.Forms.SendKeys]::SendWait('%{F4}')
 Start-Sleep -Milliseconds 300
 Screenshot 'ui-final'
 @{ passed=$true; checks=$checks; dataDirectory=$testRoot; processId=$p.Id } | ConvertTo-Json | Set-Content (Join-Path $root '.local\ui-test-results.json') -Encoding UTF8
} catch {
 @{ passed=$false; checks=$checks; error=$_.ToString(); dataDirectory=$testRoot; processId=$p.Id } | ConvertTo-Json | Set-Content (Join-Path $root '.local\ui-test-results.json') -Encoding UTF8
 throw
} finally {
 if (!$p.HasExited) { Stop-Process -Id $p.Id -ErrorAction SilentlyContinue }
}
Get-Content (Join-Path $root '.local\ui-test-results.json') -Encoding UTF8
