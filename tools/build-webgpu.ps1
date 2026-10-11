[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateRange(1, 16)][int]$Jobs = 4,
    [switch]$SkipEngineBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (!$SkipEngineBuild) {
    & (Join-Path $PSScriptRoot 'build-godot-webgpu.ps1') -Jobs $Jobs
    if ($LASTEXITCODE -ne 0) { throw 'Godot WebGPU backend build failed.' }
}

Push-Location $repoRoot
$previousBinaryenCores = $env:BINARYEN_CORES
if (!$env:BINARYEN_CORES) { $env:BINARYEN_CORES = [string][Math]::Min($Jobs, 2) }
try {
    & dotnet publish game/HeightFall.WebGpu/HeightFall.WebGpu.csproj -c $Configuration -p:HeightFallWebGpu=true --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Godot WebGPU webpage publish failed.' }
    $bundle = Join-Path $repoRoot 'game/HeightFall.WebGpu/AppBundle'
    [IO.File]::WriteAllText((Join-Path $bundle '.nojekyll'), '')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'engine/NOTICE.md') -Destination (Join-Path $bundle 'THIRD-PARTY-NOTICES.md') -Force
    $licenseDir = Join-Path $bundle 'licenses'
    New-Item -ItemType Directory -Path $licenseDir -Force | Out-Null
    $licenses = [ordered]@{
        'Godot.txt' = '.engine-cache/2dog/godot/LICENSE.txt'
        'Godot-third-party.txt' = '.engine-cache/2dog/godot/COPYRIGHT.txt'
        '2dog.txt' = 'engine/licenses/2dog.txt'
        'Tint.txt' = '.engine-cache/2dog/godot/thirdparty/tint/LICENSE'
        'SPIRV-Tools.txt' = '.engine-cache/2dog/godot/thirdparty/spirv-tools/LICENSE'
        'SPIRV-Headers.txt' = '.engine-cache/2dog/godot/thirdparty/spirv-headers/LICENSE'
        'Emdawn.txt' = '.engine-cache/emdawnwebgpu/emdawnwebgpu_pkg/webgpu/src/LICENSE'
        'Emdawn-cpp.txt' = '.engine-cache/emdawnwebgpu/emdawnwebgpu_pkg/webgpu_cpp/LICENSE'
        'WebGPU-native.txt' = 'engine/licenses/webgpu-native.txt'
        'Noto-CJK-OFL.txt' = 'game/fonts/OFL.txt'
    }
    foreach ($entry in $licenses.GetEnumerator()) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $entry.Value) -Destination (Join-Path $licenseDir $entry.Key) -Force
    }
    $dotnet = Get-Item -LiteralPath (Get-Command dotnet).Source
    if ($dotnet.LinkType) { $dotnet = $dotnet.ResolveLinkTarget($true) }
    $toolchain = Get-Content -LiteralPath (Join-Path $repoRoot '.engine-cache/emdawnwebgpu/toolchain.json') -Raw | ConvertFrom-Json
    $runtimePack = Join-Path $dotnet.DirectoryName "packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/$($toolchain.dotnetPackVersion)"
    Copy-Item -LiteralPath (Join-Path $runtimePack 'LICENSE.TXT') -Destination (Join-Path $licenseDir 'dotnet.txt') -Force
    Copy-Item -LiteralPath (Join-Path $runtimePack 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenseDir 'dotnet-third-party.txt') -Force
    Write-Host "Godot WebGPU webpage: $bundle"
}
finally {
    $env:BINARYEN_CORES = $previousBinaryenCores
    Pop-Location
}
