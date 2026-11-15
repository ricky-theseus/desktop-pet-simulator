param(
    [Parameter(Mandatory = $true)][string]$GodotExecutable,
    [Parameter(Mandatory = $true)][string]$WorkDirectory
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$work = [System.IO.Path]::GetFullPath($WorkDirectory)
$build = Join-Path $work 'public-build'
$project = Join-Path $work 'project'
$output = Join-Path $project '.godot/mono/temp/bin/Debug'
New-Item -ItemType Directory -Force -Path $build, $project, $output | Out-Null
& dotnet build (Join-Path $repository 'runtime/DesktopPet.Runtime.csproj') "-p:OutputPath=$build/" --nologo
if ($LASTEXITCODE -ne 0) { throw '公共运行时构建失败。' }
Copy-Item -LiteralPath (Join-Path $repository 'runtime/assets') -Destination $project -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AnimationLayerSmoke.cs') -Destination $project -Force
@'
config_version=5
[application]
config/name="Animation Layer Smoke"
run/main_scene="res://Smoke.tscn"
config/features=PackedStringArray("4.5", "C#", "GL Compatibility")
[display]
window/size/viewport_width=192
window/size/viewport_height=256
window/per_pixel_transparency/allowed=true
[dotnet]
project/assembly_name="DesktopPet.AnimationLayerSmoke"
[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $project 'project.godot') -Encoding utf8
@'
[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://AnimationLayerSmoke.cs" id="1"]
[node name="Smoke" type="Node"]
script = ExtResource("1")
'@ | Set-Content -LiteralPath (Join-Path $project 'Smoke.tscn') -Encoding utf8
& dotnet build (Join-Path $PSScriptRoot 'DesktopPet.AnimationLayerSmoke.csproj') "-p:PublicRuntimeDir=$build" "-p:OutputPath=$output/" --nologo
if ($LASTEXITCODE -ne 0) { throw '动画层测试构建失败。' }
Copy-Item -Path (Join-Path $build '*.dll') -Destination $output -Force
& $GodotExecutable --headless --path $project --editor --import --quit
if ($LASTEXITCODE -ne 0) { throw '测试资源导入失败。' }
$smokeOutput = & $GodotExecutable --headless --path $project --quit-after 600 -- --safe-mode "--mod-data-dir=$(Join-Path $work ('profile-' + [guid]::NewGuid()))"
$smokeExit = $LASTEXITCODE
$smokeOutput | Write-Output
if ($smokeExit -ne 0 -or !($smokeOutput -match 'Animation layer smoke: PASS \(0 failures\)')) {
    throw '动画层测试失败或未完成。'
}
