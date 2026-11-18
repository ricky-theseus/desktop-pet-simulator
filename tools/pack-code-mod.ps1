param(
    [Parameter(Mandatory = $true)][string]$Project,
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/mods'),
    [string]$AssetsDirectory,
    [string]$DesktopPetSdkDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectPath = (Resolve-Path -LiteralPath $Project).Path
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($definition.formatVersion -ne 1 -or $definition.apiVersion -ne '1.0' -or $definition.kind -ne 'code') {
    throw '需要 formatVersion 1 / apiVersion 1.0 的 code manifest。'
}
if ($definition.id -notmatch '^[a-z0-9][a-z0-9._-]{1,79}$' -or $definition.id -match '[. ]$') { throw 'Mod ID 无效。' }
if (@($definition.permissions).Count -ne 1 -or $definition.permissions[0] -ne 'full-trust') { throw '代码 Mod 必须且只能声明 full-trust。' }
if ([string]::IsNullOrWhiteSpace($definition.entryType)) { throw '缺少 entryType。' }
if ($definition.entry -notmatch '^[A-Za-z0-9_-][A-Za-z0-9._-]*\.dll$') { throw '打包工具要求 entry 为根目录 DLL 文件名。' }
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$packagePath = Join-Path $output $definition.id
$zipPath = Join-Path $output ($definition.id + '.zip')
if ((Test-Path -LiteralPath $packagePath) -or (Test-Path -LiteralPath $zipPath)) { throw '输出已存在，请选择新目录，避免覆盖已有包。' }
$temporary = Join-Path ([System.IO.Path]::GetTempPath()) ('desktop-pet-mod-' + [Guid]::NewGuid().ToString('N'))
$buildPath = Join-Path $temporary 'compiled'
$staging = Join-Path $temporary 'package'
try {
    $buildArguments = @('build', $projectPath, '-c', 'Release', '--output', $buildPath)
    if ($DesktopPetSdkDirectory) {
        $sdkPath = (Resolve-Path -LiteralPath $DesktopPetSdkDirectory).Path
        $buildArguments += "-p:DesktopPetSdkDirectory=$sdkPath"
    }
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Mod 构建失败：$LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath (Join-Path $buildPath $definition.entry))) { throw '构建输出找不到 manifest.entry。' }
    [System.IO.Directory]::CreateDirectory($staging) | Out-Null
    # 共享契约必须由宿主提供，否则类型身份不一致；PDB、源码和构建元数据不进入包。
    foreach ($file in Get-ChildItem -LiteralPath $buildPath -Recurse -File -Filter '*.dll') {
        if ($file.Name -match '^(DesktopPet\.(Core|Godot)|GodotSharp(?:Editor)?)\.dll$') { continue }
        $relative = [System.IO.Path]::GetRelativePath($buildPath, $file.FullName)
        $destination = Join-Path $staging $relative
        [System.IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    if ($AssetsDirectory) {
        $assetRoot = (Resolve-Path -LiteralPath $AssetsDirectory).Path
        $allowed = @('.json', '.png', '.md')
        foreach ($file in Get-ChildItem -LiteralPath $assetRoot -Recurse -File) {
            if ($allowed -notcontains $file.Extension.ToLowerInvariant() -or ($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                throw "素材目录含不支持的文件或链接：$($file.Name)"
            }
            $relative = [System.IO.Path]::GetRelativePath($assetRoot, $file.FullName)
            $destination = Join-Path $staging $relative
            if ((Test-Path -LiteralPath $destination) -or $relative -eq 'manifest.json') { throw "素材路径冲突：$relative" }
            [System.IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $staging 'manifest.json')
    [System.IO.Directory]::CreateDirectory($output) | Out-Null
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath)
    Copy-Item -LiteralPath $staging -Destination $packagePath -Recurse
    Write-Output "目录包：$packagePath"
    Write-Output "ZIP：$zipPath"
} finally {
    # 只清理本次生成且位于系统临时目录内的随机目录。
    $resolvedTemporary = [System.IO.Path]::GetFullPath($temporary)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ((Split-Path $resolvedTemporary -Parent) -ne $expectedParent -or (Split-Path $resolvedTemporary -Leaf) -notmatch '^desktop-pet-mod-[a-f0-9]{32}$') {
        throw '临时目录校验失败，未执行清理。'
    }
    if (Test-Path -LiteralPath $resolvedTemporary) { Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force }
}
