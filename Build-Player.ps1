param(
    [string]$VideoPath = (Join-Path $PSScriptRoot 'assets\compatible-clip.mp4'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'dist\OLEDVideoPlayer-Fixed.exe')
)

$ErrorActionPreference = 'Stop'
$video = (Resolve-Path -LiteralPath $VideoPath).Path
$source = Join-Path $PSScriptRoot 'player\OLEDVideoPlayer.cs'
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework 4.x C# compiler was not found.'
}

$references = @(
    'WPF\PresentationFramework.dll'
    'WPF\PresentationCore.dll'
    'WPF\WindowsBase.dll'
    'System.Xaml.dll'
) | ForEach-Object {
    '/reference:' + (Join-Path $framework $_)
}

$outputDirectory = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$clipResource = '/resource:' + $video + ',OledVideoSync.CompatibleClip.mp4'
& $compiler /nologo /target:winexe /optimize+ ("/out:" + $output) @references $clipResource $source
if ($LASTEXITCODE -ne 0) {
    throw "Player build failed with exit code $LASTEXITCODE."
}
Write-Output $output
