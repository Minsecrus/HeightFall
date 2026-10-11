[CmdletBinding()]
param(
    [ValidateRange(1, 16)][int]$Jobs = 4,
    [string]$DotNetRoot = '',
    [switch]$VerifyToolchain
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $repoRoot '.engine-cache/2dog/godot'
if (!$DotNetRoot) {
    $dotnet = Get-Item -LiteralPath (Get-Command dotnet).Source
    if ($dotnet.LinkType) { $dotnet = $dotnet.ResolveLinkTarget($true) }
    $DotNetRoot = $dotnet.DirectoryName
}

& uv run --project (Join-Path $repoRoot 'engine') python (Join-Path $repoRoot 'engine/prepare_engine.py')
if ($LASTEXITCODE -ne 0) { throw 'Godot WebGPU source preparation failed.' }
& (Join-Path $repoRoot 'engine/toolchain/prepare-emdawn.ps1') -DotNetRoot $DotNetRoot -Verify:$VerifyToolchain
if ($LASTEXITCODE -ne 0) { throw 'Emdawn preparation failed.' }
& (Join-Path $repoRoot 'engine/toolchain/verify-tint-floats.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Tint numeric compatibility verification failed.' }

$toolchain = Get-Content -LiteralPath (Join-Path $repoRoot '.engine-cache/emdawnwebgpu/toolchain.json') -Raw | ConvertFrom-Json
$env:EMDAWNWEBGPU_INCLUDE = $toolchain.include
$env:EMDAWNWEBGPU_CPP_INCLUDE = $toolchain.cppInclude
$sdkTools = $env:DOTNET_EMSCRIPTEN_BINARYEN_ROOT
$env:EMSDK_PATH = $sdkTools + [IO.Path]::DirectorySeparatorChar
$toolPaths = @((Join-Path $sdkTools 'emscripten'), (Join-Path $sdkTools 'bin'), (Split-Path $env:DOTNET_EMSCRIPTEN_NODE_JS))
$env:PATH = ($toolPaths -join [IO.Path]::PathSeparator) + [IO.Path]::PathSeparator + $env:PATH
$env:EM_FROZEN_CACHE = '1'

$buildArgs = @(
    'run', '--project', (Join-Path $repoRoot 'engine'), 'scons', '-C', $source, "-j$Jobs",
    'platform=web', 'arch=wasm32', 'target=template_release',
    'module_mono_enabled=yes', 'library_type=static_library', 'extra_suffix=static_library',
    'threads=no', 'lto=none', 'disable_crash_handler=yes', 'dev_build=no',
    'scu_build=yes', 'disable_path_overrides=no', 'webgpu=yes', 'opengl3=no'
)
& uv @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Godot WebGPU native compilation failed.' }

# Stage a native library payload, not a standalone Godot WebAssembly module.
# The .NET host links this archive into its own matching runtime.
$staging = Join-Path $source 'bin/.web_zip'
$payload = Join-Path $source 'bin/web/template_release'
$archive = Join-Path $staging 'libgodot/libgodot.a'
if (!(Test-Path -LiteralPath $archive)) { throw "Native archive not produced: $archive" }
New-Item -ItemType Directory -Path (Join-Path $payload 'libgodot'), (Join-Path $payload 'shell') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $staging 'libgodot') -Destination $payload -Recurse -Force
foreach ($name in @('godot.js', 'godot.audio.worklet.js', 'godot.audio.position.worklet.js')) {
    Copy-Item -LiteralPath (Join-Path $staging $name) -Destination (Join-Path $payload 'shell') -Force
}
Write-Host "Godot 4.7.2 WebGPU native payload: $payload"
