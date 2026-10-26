$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $root 'src/DamageMeter.Sniffing/Transport.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Transport build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.Sniffing/bin-transport/net7.0-windows/DamageMeter.Sniffing.dll') -Destination $root
dotnet build (Join-Path $root 'src/DamageMeter.UI/ModernMeter.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Interface build failed.' }
dotnet build (Join-Path $root 'src/Lang/Ru.Local.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Russian resource build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.UI/bin-modern/net7.0-windows/ShinraMeter.dll') -Destination $root
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.UI/bin-modern/net7.0-windows/ShinraMeter.exe') -Destination $root
Copy-Item -LiteralPath (Join-Path $root 'src/Lang/bin-ru/net7.0/ru/Lang.resources.dll') -Destination (Join-Path $root 'ru/Lang.resources.dll')
