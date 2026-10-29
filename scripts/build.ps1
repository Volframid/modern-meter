$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'scripts/NotificationBridge/NotificationBridge.csproj') -c Release -- (Join-Path $root 'DamageMeter.dll')
if ($LASTEXITCODE -ne 0) { throw 'Notification compatibility bridge failed.' }
dotnet build (Join-Path $root 'src/DamageMeter.Sniffing/Transport.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Transport build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.Sniffing/bin-transport/net7.0-windows/DamageMeter.Sniffing.dll') -Destination $root
# Refresh native EXE icon resources even when only the ICO asset changed.
dotnet build (Join-Path $root 'src/DamageMeter.UI/ModernMeter.csproj') -t:Rebuild -c Release
if ($LASTEXITCODE -ne 0) { throw 'Interface build failed.' }
dotnet build (Join-Path $root 'src/Lang/Ru.Local.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Russian resource build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.UI/bin-modern/net7.0-windows/ModernMeter.dll') -Destination $root
Copy-Item -LiteralPath (Join-Path $root 'src/DamageMeter.UI/bin-modern/net7.0-windows/ModernMeter.exe') -Destination $root
Copy-Item -LiteralPath (Join-Path $root 'src/Lang/bin-ru/net7.0/ru/Lang.resources.dll') -Destination (Join-Path $root 'ru/Lang.resources.dll')
