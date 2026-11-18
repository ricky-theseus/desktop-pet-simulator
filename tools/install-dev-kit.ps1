#requires -Version 5.1
param(
    [string]$InstallDirectory = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'DesktopPetModKit'),
    [ValidateSet('Codex', 'Claude', 'Both', 'None')][string]$Agent = 'Codex',
    [string]$SkillsDirectory,
    [switch]$SkillsOnly,
    [string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'This one-click installer supports Windows. See README for manual SDK use on other systems.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'
$version = '1.0.0'
$archiveHash = '9cd823c04d1d29f1998c6427480f6c1e046cac4d23af32f89409186e8fae2b2a'
$releaseApi = 'https://api.github.com/repos/ricky-theseus/desktop-pet-simulator/releases/tags/v1.0.0'
$kitRoot = [IO.Path]::GetFullPath($InstallDirectory)
$statePath = Join-Path $kitRoot 'install.json'
$utf8 = New-Object Text.UTF8Encoding($false)
$userDirectory = [Environment]::GetFolderPath('UserProfile')
$skillRoots = @()
if ($SkillsDirectory) { $skillRoots += [IO.Path]::GetFullPath($SkillsDirectory) }
else {
    if ($Agent -in @('Codex', 'Both')) {
        $codexDirectory = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $userDirectory '.codex' }
        $skillRoots += Join-Path $codexDirectory 'skills'
    }
    if ($Agent -in @('Claude', 'Both')) { $skillRoots += Join-Path $userDirectory '.claude/skills' }
}
$skillNames = @('desktop-pet-mod-authoring', 'desktop-pet-mod-debugging', 'desktop-pet-mod-publishing')
if (Test-Path -LiteralPath $kitRoot) {
    if (!(Test-Path -LiteralPath $statePath)) { throw "Directory already exists and is not managed by this installer: $kitRoot" }
    $previous = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.version -ne $version -or $previous.archiveSha256 -ne $archiveHash) { throw 'Existing kit differs. Choose a new -InstallDirectory; no files were replaced.' }
}
foreach ($root in $skillRoots) {
    foreach ($name in $skillNames) {
        $destination = Join-Path $root $name
        if (!(Test-Path -LiteralPath $destination)) { continue }
        $markerPath = Join-Path $destination 'desktop-pet-install.json'
        if (!(Test-Path -LiteralPath $markerPath)) { throw "Skill already exists; choose another -SkillsDirectory: $destination" }
        $marker = Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($marker.kitRoot -ne $kitRoot -or $marker.archiveSha256 -ne $archiveHash) { throw "Skill belongs to a different installation: $destination" }
    }
}
if (!(Test-Path -LiteralPath $kitRoot)) {
    $downloaded = !$ArchivePath
    if ($downloaded) {
        $ArchivePath = Join-Path ([IO.Path]::GetTempPath()) ('desktop-pet-devkit-' + [guid]::NewGuid().ToString('N') + '.zip')
        Write-Host 'Downloading SDK, examples, tools and skills from GitHub...'
        $release = Invoke-RestMethod -Uri $releaseApi -TimeoutSec 30
        $asset = $release.assets | Where-Object name -eq "DesktopPetSimulator-ModDevKit-$version.zip"
        if (!$asset) { throw 'The matching dev kit asset was not found in the release.' }
        Invoke-WebRequest -UseBasicParsing -Uri $asset.url -Headers @{Accept='application/octet-stream'} -OutFile $ArchivePath -TimeoutSec 120
    }
    try {
        if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $archiveHash) { throw 'Dev kit SHA-256 verification failed. Nothing installed.' }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($ArchivePath), $kitRoot)
        $state = [ordered]@{ version = $version; archiveSha256 = $archiveHash; dotnet = $null }
        [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
    } finally {
        if ($downloaded -and (Test-Path -LiteralPath $ArchivePath)) { Remove-Item -LiteralPath $ArchivePath }
    }
}
foreach ($required in @('sdk/lib/DesktopPet.Core.dll', 'sdk/lib/DesktopPet.Godot.dll', 'tools/build-mod.ps1', 'tools/pack-code-mod.ps1', 'LICENSE', 'NOTICE.md')) {
    if (!(Test-Path -LiteralPath (Join-Path $kitRoot $required))) { throw "Incomplete kit: $required. Use a new installation directory." }
}
$state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
if (!$SkillsOnly) {
    $candidates = @()
    if ($state.dotnet) { $candidates += $state.dotnet }
    $candidates += Join-Path $kitRoot '.dotnet/dotnet.exe'
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $dotnetPath = $null
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (!(Test-Path -LiteralPath $candidate)) { continue }
        $sdks = & $candidate --list-sdks
        if ($LASTEXITCODE -eq 0 -and @($sdks | Where-Object { $_ -match '^([89]|[1-9][0-9])\.\d+\.\d+\s' }).Count -gt 0) { $dotnetPath = $candidate; break }
    }
    if (!$dotnetPath) {
        $dotnetInstaller = Join-Path $kitRoot 'dotnet-install.ps1'
        Write-Host 'No compatible .NET SDK found. Installing .NET 8 in this kit directory using the Microsoft installer...'
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $dotnetInstaller
        & $dotnetInstaller -Channel 8.0 -InstallDir (Join-Path $kitRoot '.dotnet') -NoPath
        $dotnetPath = Join-Path $kitRoot '.dotnet/dotnet.exe'
        if (!(Test-Path -LiteralPath $dotnetPath)) { throw '.NET SDK installation did not finish. Re-run this installer to retry.' }
        $sdks = & $dotnetPath --list-sdks
        if ($LASTEXITCODE -ne 0 -or !($sdks -match '^8\.')) { throw '.NET SDK verification failed.' }
    }
    $state.dotnet = $dotnetPath
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
}
foreach ($root in $skillRoots) {
    foreach ($name in $skillNames) {
        $destination = Join-Path $root $name
        if (Test-Path -LiteralPath $destination) { Write-Host "Keeping existing skill: $name"; continue }
        New-Item -ItemType Directory -Force -Path $root | Out-Null
        Copy-Item -LiteralPath (Join-Path $kitRoot "skills/$name") -Destination $destination -Recurse
        $referenceDirectory = Join-Path $destination 'references'
        New-Item -ItemType Directory -Force -Path $referenceDirectory | Out-Null
        $reference = @"
# Installed Desktop Pet Mod Dev Kit

Kit version: $version / Mod API: 1.0 / Godot: 4.5.1 .NET / target: net8.0.

Treat all sdk/, examples/ and tools/ paths in this skill as relative to:
$kitRoot

SDK documentation: $kitRoot/sdk/code-mod-v1.md
SDK assemblies: $kitRoot/sdk/lib
Example project: $kitRoot/examples/code-desk-lamp/DeskLamp.Mod.csproj
Build tool: $kitRoot/tools/build-mod.ps1

Read install.json for the actual .NET SDK path. If dotnet is empty, run the full installer first.
Use build-mod.ps1 -Project <project.csproj> -Manifest <manifest.json> -OutputDirectory <new-directory>.
Copy the example into the player's own project directory and change its ID/name/entry consistently; preserve the installed SDK.
Keep the DesktopPetSdkDirectory conditional binary references in the example project.
Build tools work in Windows PowerShell 5.1 and PowerShell 7. The game is a separate download.
Read LICENSE and NOTICE.md: PolyForm Noncommercial 1.0.0; commercial use needs separate authorization.
"@
        [IO.File]::WriteAllText((Join-Path $referenceDirectory 'devkit.md'), $reference, $utf8)
        [IO.File]::WriteAllText((Join-Path $destination 'desktop-pet-install.json'), (@{kitRoot=$kitRoot;archiveSha256=$archiveHash} | ConvertTo-Json), $utf8)
    }
}
if (!$SkillsOnly) {
    $checkOutput = Join-Path $kitRoot ('build/install-check-' + [guid]::NewGuid().ToString('N'))
    Write-Host 'Building and packaging the independent example Mod...'
    & (Join-Path $kitRoot 'tools/build-mod.ps1') -OutputDirectory $checkOutput
    Write-Host "Example ZIP: $checkOutput/example-desk-lamp.zip"
}
Write-Host "Ready. Dev kit: $kitRoot"
if ($skillRoots.Count) { Write-Host 'Skills installed: desktop-pet-mod-authoring, desktop-pet-mod-debugging, desktop-pet-mod-publishing. Start a new agent session to discover them.' }
Write-Host 'License: PolyForm Noncommercial 1.0.0. Commercial use requires separate authorization.'
