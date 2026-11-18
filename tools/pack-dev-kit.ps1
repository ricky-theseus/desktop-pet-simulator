param(
    [Parameter(Mandatory=$true)][string]$SdkDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$version = [IO.File]::ReadAllText((Join-Path $repository 'VERSION')).Trim()
$output = [IO.Path]::GetFullPath($OutputDirectory)
$package = Join-Path $output 'kit'
$archive = Join-Path $output "DesktopPetSimulator-ModDevKit-$version.zip"
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $archive)) { throw 'Output exists. Choose a new directory.' }
New-Item -ItemType Directory -Force -Path $package,(Join-Path $package 'sdk/lib'),(Join-Path $package 'examples'),(Join-Path $package 'tools'),(Join-Path $package 'skills') | Out-Null
foreach ($name in @('DesktopPet.Core.dll','DesktopPet.Godot.dll')) {
    Copy-Item -LiteralPath (Join-Path $SdkDirectory $name) -Destination (Join-Path $package 'sdk/lib')
}
Get-ChildItem -LiteralPath (Join-Path $repository 'sdk') -File -Filter '*.md' | Copy-Item -Destination (Join-Path $package 'sdk')
foreach ($example in @('code-desk-lamp','sleepy-mushroom')) {
    $destination = Join-Path $package "examples/$example"
    New-Item -ItemType Directory -Path $destination | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $repository "examples/$example") -File | Where-Object {$_.Extension -in '.cs','.csproj','.json','.md'} | Copy-Item -Destination $destination
}
foreach ($tool in @('pack-code-mod.ps1','pack-example-mod.ps1','build-mod.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $tool) -Destination (Join-Path $package 'tools') }
$frames = Join-Path $package 'runtime/assets/mushroom/actions-v1'
New-Item -ItemType Directory -Force -Path $frames | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repository 'runtime/assets/mushroom/actions-v1') -Filter '*.png' | Copy-Item -Destination $frames
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($name in @('mod-authoring','mod-debugging','mod-publishing')) {
    $destination = Join-Path $package "skills/desktop-pet-$name"
    New-Item -ItemType Directory -Path $destination | Out-Null
    $content = [IO.File]::ReadAllText((Join-Path $repository "skills/$name/SKILL.md"))
    $content = $content.Replace("name: $name", "name: desktop-pet-$name")
    [IO.File]::WriteAllText((Join-Path $destination 'SKILL.md'), $content, $utf8)
}
foreach ($name in @('LICENSE','NOTICE.md','VERSION')) { Copy-Item -LiteralPath (Join-Path $repository $name) -Destination $package }
Copy-Item -LiteralPath (Join-Path $repository 'README.md') -Destination $package
# Windows PowerShell 5.1 需要 BOM 才能正确读取脚本中的中文。
foreach ($script in Get-ChildItem -LiteralPath (Join-Path $package 'tools') -Filter '*.ps1') {
    [IO.File]::WriteAllText($script.FullName, [IO.File]::ReadAllText($script.FullName), (New-Object Text.UTF8Encoding($true)))
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package, $archive)
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "DEVKIT_ARCHIVE=$archive"
Write-Output "DEVKIT_SHA256=$hash"
