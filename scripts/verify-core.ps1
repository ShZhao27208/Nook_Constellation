$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $env:TEMP ('NookTests-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$exe = Join-Path $root 'artifacts\dev\Nook.Constellation.exe'
$p = Start-Process -FilePath $exe -ArgumentList @('--self-test', '--data-dir', ('"' + $testRoot + '"')) -Wait -PassThru
New-Item -ItemType Directory -Force (Join-Path $root '.local') | Out-Null
Copy-Item (Join-Path $testRoot 'test-results.json') (Join-Path $root '.local\core-test-results.json')
Get-Content (Join-Path $testRoot 'test-results.json') -Encoding UTF8
exit $p.ExitCode
