param(
    [string]$Project,
    [string]$Manifest,
    [string]$OutputDirectory,
    [string]$AssetsDirectory
)
$ErrorActionPreference = 'Stop'
$kitRoot = Split-Path $PSScriptRoot -Parent
$statePath = Join-Path $kitRoot 'install.json'
if (!(Test-Path -LiteralPath $statePath)) { throw 'Run Install-ModDevKit.ps1 first.' }
$state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
if (!$state.dotnet -or !(Test-Path -LiteralPath $state.dotnet)) { throw 'Run the full installer to prepare the .NET SDK.' }
if (!$Project) { $Project = Join-Path $kitRoot 'examples/code-desk-lamp/DeskLamp.Mod.csproj' }
if (!$Manifest) { $Manifest = Join-Path (Split-Path ([IO.Path]::GetFullPath($Project)) -Parent) 'manifest.json' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $kitRoot 'build/mods' }
$parameters = @{
    Project = $Project; Manifest = $Manifest; OutputDirectory = $OutputDirectory
    DesktopPetSdkDirectory = (Join-Path $kitRoot 'sdk/lib'); DotnetExecutable = $state.dotnet
}
if ($AssetsDirectory) { $parameters.AssetsDirectory = $AssetsDirectory }
& (Join-Path $PSScriptRoot 'pack-code-mod.ps1') @parameters
