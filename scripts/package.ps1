$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$package = Join-Path $root ('artifacts\Nook-Constellation-' + $version + '-win-x64')
$app = Join-Path $package 'app'
dotnet publish (Join-Path $root 'src\Nook.Constellation\Nook.Constellation.csproj') -c Release -r win-x64 --self-contained true -o $app
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item (Join-Path $root 'packaging\*') -Destination $package -Force
Copy-Item (Join-Path $root 'docs\USER-GUIDE.md') -Destination (Join-Path $package '使用说明.md') -Force
$zip = $package + '.zip'
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
(Get-FileHash $zip -Algorithm SHA256).Hash | Set-Content ($zip + '.sha256')
Write-Output $zip
