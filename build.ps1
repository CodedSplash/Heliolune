param([string]$Output = (Join-Path $PSScriptRoot 'bin'))

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler could not be found.' }

$null = New-Item -ItemType Directory -Force -Path $Output
$application = Join-Path (Resolve-Path -LiteralPath $Output).Path 'Sutki.exe'
Push-Location -LiteralPath (Join-Path $PSScriptRoot 'src\Sutki')
try {
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32icon:Sutki.ico `
        /win32manifest:app.manifest /resource:Sutki.ico /resource:Styles.xaml `
        "/reference:$framework\System.Xaml.dll" "/reference:$framework\WPF\PresentationFramework.dll" `
        "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\WindowsBase.dll" `
        /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$application" AssemblyInfo.cs LocalizedText.cs Model.cs Windows.cs Panel.cs Program.cs SkyDial.cs Celestial.cs SkyBackdrop.cs ControlMotion.cs TrayMenu.cs
    if ($LASTEXITCODE -ne 0) { throw "C# compilation failed ($LASTEXITCODE)." }
    Copy-Item -LiteralPath 'Sutki.exe.config' -Destination ($application + '.config') -Force
    Write-Output $application
}
finally { Pop-Location }
