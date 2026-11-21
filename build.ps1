$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
foreach ($need in 'Program.cs','panel.html','defaults','icon.ico') { if (-not (Test-Path (Join-Path $root $need))) { Write-Host "Missing $need next to build.ps1. Extract the whole zip and run build.bat from that folder." -ForegroundColor Red; exit 1 } }
$work = Join-Path $root 'work'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $work, $dist | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$pkgs = @(@('Microsoft.Web.WebView2', '1.0.2792.45'), @('NAudio.Core', '2.2.1'), @('NAudio.Wasapi', '2.2.1'), @('MoonSharp', '2.0.0.0'))
foreach ($p in $pkgs) {
    $dir = Join-Path $work $p[0]
    if (-not (Test-Path $dir)) {
        $zip = Join-Path $work ($p[0] + '.zip')
        Invoke-WebRequest ("https://www.nuget.org/api/v2/package/" + $p[0] + "/" + $p[1]) -OutFile $zip -UseBasicParsing
        Expand-Archive $zip $dir -Force
    }
}

function Pick($pkg, $name) {
    $c = Get-ChildItem (Join-Path $work $pkg) -Recurse -Filter $name | Where-Object { $_.FullName -match '\\lib\\' }
    foreach ($t in 'net472', 'net48', 'net462', 'netstandard2.0', 'net40-client', 'net40', 'net35-client') {
        $m = $c | Where-Object { $_.FullName -match ('\\' + [regex]::Escape($t) + '\\') } | Select-Object -First 1
        if ($m) { return $m.FullName }
    }
    throw "Missing $name"
}

Copy-Item (Pick 'Microsoft.Web.WebView2' 'Microsoft.Web.WebView2.Core.dll') $dist -Force
Copy-Item (Pick 'Microsoft.Web.WebView2' 'Microsoft.Web.WebView2.WinForms.dll') $dist -Force
Copy-Item (Pick 'NAudio.Core' 'NAudio.Core.dll') $dist -Force
Copy-Item (Pick 'NAudio.Wasapi' 'NAudio.Wasapi.dll') $dist -Force
Copy-Item (Pick 'MoonSharp' 'MoonSharp.Interpreter.dll') $dist -Force
$loader = Get-ChildItem (Join-Path $work 'Microsoft.Web.WebView2') -Recurse -Filter 'WebView2Loader.dll' | Where-Object { $_.FullName -match 'win-x64' } | Select-Object -First 1
Copy-Item $loader.FullName $dist -Force

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'NAudio.Core.dll', 'NAudio.Wasapi.dll', 'MoonSharp.Interpreter.dll') | ForEach-Object { '/r:' + (Join-Path $dist $_) }
$roots = @((Join-Path $env:WINDIR 'Microsoft.NET'), (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework'))
foreach ($n in 'netstandard.dll', 'System.Runtime.dll') {
    $f = $null
    foreach ($r in $roots) {
        if (-not $f -and (Test-Path $r)) {
            $f = Get-ChildItem $r -Recurse -Filter $n -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match 'GAC_MSIL|Facades' } | Sort-Object FullName -Descending | Select-Object -First 1
        }
    }
    if ($f) { $refs += '/r:' + $f.FullName }
}
Copy-Item "$root\defaults" $dist -Recurse -Force
$a = @('/nologo', "/win32icon:$root\icon.ico", '/target:winexe', '/platform:x64', "/out:$dist\lua_walpapers.exe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:Microsoft.CSharp.dll', "/resource:$root\panel.html,panel.html") + $refs + @("$root\Program.cs")
& $csc $a 2>&1 | Tee-Object -FilePath (Join-Path $root 'build-log.txt') | Write-Host
if ($LASTEXITCODE -ne 0) { Write-Host ''; Write-Host 'Compile failed. Send me the lines with "error CS" (also saved in build-log.txt).' -ForegroundColor Red; exit 1 }
Write-Host ''
Write-Host "Done: $dist\lua_walpapers.exe"
