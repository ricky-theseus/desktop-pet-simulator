param([Parameter(Mandatory=$true)][string]$GodotExecutable, [Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$work = [IO.Path]::GetFullPath($WorkDirectory)
$build = Join-Path $work 'public-build'
$project = Join-Path $work 'project'
$output = Join-Path $project '.godot/mono/temp/bin/Debug'
$profile = Join-Path $work ('profile-' + [guid]::NewGuid().ToString('N'))
$captures = Join-Path $work 'captures'
New-Item -ItemType Directory -Force -Path $build, $project, $output, $profile, $captures | Out-Null
& dotnet build (Join-Path $repository 'runtime/DesktopPet.Runtime.csproj') "-p:OutputPath=$build/" --nologo
if ($LASTEXITCODE -ne 0) { throw '公共运行时构建失败。' }
if (!(Test-Path -LiteralPath (Join-Path $project 'assets'))) { Copy-Item -LiteralPath (Join-Path $repository 'runtime/assets') -Destination $project -Recurse }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PetInstancesSmoke.cs') -Destination $project -Force
@'
config_version=5
[application]
config/name="Pet Instances GPU Smoke"
run/main_scene="res://Smoke.tscn"
config/features=PackedStringArray("4.5", "C#", "GL Compatibility")
[display]
window/size/viewport_width=192
window/size/viewport_height=256
window/per_pixel_transparency/allowed=true
window/subwindows/embed_subwindows=false
[dotnet]
project/assembly_name="DesktopPet.PetInstancesSmoke"
[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $project 'project.godot') -Encoding utf8
@'
[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://PetInstancesSmoke.cs" id="1"]
[node name="Smoke" type="Node"]
script = ExtResource("1")
'@ | Set-Content -LiteralPath (Join-Path $project 'Smoke.tscn') -Encoding utf8
& dotnet build (Join-Path $PSScriptRoot 'DesktopPet.PetInstancesSmoke.csproj') "-p:PublicRuntimeDir=$build" "-p:OutputPath=$output/" --nologo
if ($LASTEXITCODE -ne 0) { throw '实例验证构建失败。' }
Copy-Item -Path (Join-Path $build '*.dll') -Destination $output -Force
& $GodotExecutable --headless --path $project --editor --import --quit
if ($LASTEXITCODE -ne 0) { throw '资源导入失败。' }
$log = & $GodotExecutable --path $project --max-fps 60 --quit-after 1200 -- --safe-mode "--mod-data-dir=$profile" "--captures=$captures" 2>&1
$exit = $LASTEXITCODE
$log | Set-Content -LiteralPath (Join-Path $work 'last-run.log') -Encoding utf8
$log | Write-Output
if ($exit -ne 0 -or !($log -match 'Pet instances GPU smoke: PASS') -or ($log -match '^ERROR:')) { throw '宠物实例验证失败或未完成。' }
