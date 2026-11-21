# Build DeskSeasons.exe with the .NET Framework 4 compiler (present on every Windows 10/11)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$src  = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$out  = Join-Path $dist 'DeskSeasons.exe'
$ico  = Join-Path $dist 'DeskSeasons.ico'
$common = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001',
            "/win32manifest:$(Join-Path $root 'src\app.manifest')",
            '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')

# 1) first build without icon, so the app can draw its own .ico
& $csc @common "/out:$out" @src
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Start-Process -FilePath $out -ArgumentList '--export-icon', "`"$ico`"" -Wait

# 2) final build with icon
& $csc @common "/out:$out" "/win32icon:$ico" @src
if ($LASTEXITCODE -ne 0) { throw 'Compile failed (with icon)' }
Write-Host "OK: $out"
