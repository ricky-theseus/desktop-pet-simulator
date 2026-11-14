param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/mods'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = Split-Path $PSScriptRoot -Parent
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($output) | Out-Null
$archivePath = Join-Path $output 'sleepy-mushroom.zip'
# Create 会拒绝覆盖已有包，避免误改用户选择的输出文件。
$archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('manifest.json', 'appearance.json')) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $repo "examples/sleepy-mushroom/$name"), $name) | Out-Null
    }
    foreach ($frame in 1..8) {
        $name = '{0:D2}.png' -f $frame
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $repo "runtime/assets/mushroom/actions-v1/$name"), $name) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Output $archivePath
