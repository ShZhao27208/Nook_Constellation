$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$testRoot=Join-Path $env:TEMP ('NookNotification-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$exe=Join-Path $root 'artifacts\dev\Nook.Constellation.exe'
$p=Start-Process -FilePath $exe -ArgumentList @('--data-dir',('"'+$testRoot+'"'),'--notification-test') -PassThru
Start-Sleep -Seconds 5
$p.Refresh()
if ($p.HasExited) { throw 'Notification test exited unexpectedly' }
@{processId=$p.Id; dataDirectory=$testRoot} | ConvertTo-Json | Set-Content (Join-Path $root '.local\notification-test-process.json')
Get-Content (Join-Path $root '.local\notification-test-process.json')
if (Test-Path (Join-Path $testRoot 'errors.log')) { Get-Content (Join-Path $testRoot 'errors.log'); exit 1 }
