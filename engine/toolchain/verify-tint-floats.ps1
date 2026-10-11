# SPDX-License-Identifier: MIT
<#
.SYNOPSIS
Compile and run the real Tint float parsing regression with Emscripten 3.1.56.
.DESCRIPTION
Requires the prepared Godot source and the environment set by prepare-emdawn.ps1
in this PowerShell session. Uses EMSDK_PYTHON, DOTNET_EMSCRIPTEN_BINARYEN_ROOT,
and DOTNET_EMSCRIPTEN_NODE_JS, along with the matching LLVM/cache settings.
Compiles the current vendored parse_num.cc and its ICE dependencies, then runs
129 numeric boundary checks in Node. Outputs stay in .engine-cache/tint-float-test.
.EXAMPLE
& ./engine/toolchain/prepare-emdawn.ps1
& ./engine/toolchain/verify-tint-floats.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($name in @('EMSDK_PYTHON', 'DOTNET_EMSCRIPTEN_BINARYEN_ROOT', 'DOTNET_EMSCRIPTEN_NODE_JS')) {
    if (![Environment]::GetEnvironmentVariable($name)) {
        throw "Missing $name. Run engine/toolchain/prepare-emdawn.ps1 in this PowerShell session first."
    }
}

$compiler = Join-Path $env:DOTNET_EMSCRIPTEN_BINARYEN_ROOT 'emscripten/em++.py'
$tintRoot = Join-Path $repoRoot '.engine-cache/2dog/godot/thirdparty/tint'
$testSource = Join-Path $repoRoot 'engine/tests/tint-float-parse.cc'
$parser = Join-Path $tintRoot 'src/tint/utils/strconv/parse_num.cc'
$ice = Join-Path $tintRoot 'src/tint/utils/ice/ice.cc'
$debugger = Join-Path $tintRoot 'src/tint/utils/ice/debugger.cc'
foreach ($path in @($env:EMSDK_PYTHON, $env:DOTNET_EMSCRIPTEN_NODE_JS, $compiler, $testSource, $parser, $ice, $debugger)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required tool or prepared Tint source is missing: $path"
    }
}

$compilerVersion = & $env:EMSDK_PYTHON $compiler --version
if ($LASTEXITCODE -ne 0 -or ($compilerVersion -join "`n") -notmatch '\b3\.1\.56\b') {
    throw 'This regression requires the .NET 10 Emscripten 3.1.56 toolchain.'
}
Write-Host $compilerVersion[0]

$outputDir = Join-Path $repoRoot '.engine-cache/tint-float-test'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$output = Join-Path $outputDir 'test.js'
$compilerArgs = @(
    $compiler, $testSource, $parser, $ice, $debugger,
    '-I', $tintRoot, '-std=c++20', '-O1', '-fno-exceptions', '-fno-rtti', '-DNDEBUG',
    '-sENVIRONMENT=node', '-sSINGLE_FILE=1', '-o', $output
)
& $env:EMSDK_PYTHON @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Tint float parsing regression compilation failed (exit code $LASTEXITCODE)."
}

& $env:DOTNET_EMSCRIPTEN_NODE_JS $output
if ($LASTEXITCODE -ne 0) {
    throw "Tint float parsing regression failed (exit code $LASTEXITCODE)."
}
