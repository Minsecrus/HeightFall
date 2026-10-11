# SPDX-License-Identifier: MIT
[CmdletBinding()]
param(
    [string]$DotNetRoot = '',
    [string]$PackVersion = '',
    [string]$CacheRoot = '',
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
if (!$DotNetRoot) {
    $dotnetExecutable = Get-Item -LiteralPath (Get-Command dotnet -ErrorAction Stop).Source
    if ($dotnetExecutable.LinkType) { $dotnetExecutable = $dotnetExecutable.ResolveLinkTarget($true) }
    $DotNetRoot = $dotnetExecutable.DirectoryName
}
$DotNetRoot = (Resolve-Path -LiteralPath $DotNetRoot).Path
$windowsHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
$linuxHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)
$macHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::OSX)
$architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($architecture -notin @('x64', 'arm64')) { throw "Unsupported Emscripten host architecture: $architecture" }
$platformName = if ($windowsHost) { 'win' } elseif ($linuxHost) { 'linux' } elseif ($macHost) { 'osx' } else { throw 'Unsupported Emscripten host OS.' }
if ($linuxHost -and [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier.StartsWith('linux-musl-')) { $platformName = 'linux-musl' }
$hostRid = "$platformName-$architecture"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$CacheRoot) { $CacheRoot = Join-Path $repoRoot '.engine-cache/emdawnwebgpu' }
$CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
$version = 'v20250531.224602'
$archiveHash = 'E0AC709B84CA14C0FFCF33E548165DCD6F20FF698F0C778D76A2A8A5C697170C'
$sourceHash = '02D9F2E8CB40CC4B6730785D731E8B496CB4753EB357A5A097C627E4FB310119'
$patchedSourceHash = 'E6D27C03C17BB1F30E7790E94F9336F8CF3248DC425262BD894CFA770A7DC8F0'
$archive = Join-Path $CacheRoot "emdawnwebgpu_pkg-$version.zip"
$package = Join-Path $CacheRoot 'emdawnwebgpu_pkg'
New-Item -ItemType Directory -Path $CacheRoot -Force | Out-Null

if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri "https://github.com/google/dawn/releases/download/$version/emdawnwebgpu_pkg-$version.zip" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) {
    throw "Emdawn source archive checksum differs from the pinned $version package: $archive"
}

$cpp = Join-Path $package 'webgpu/src/webgpu.cpp'
# Reuse the checksum-verified prepared source while a parallel engine build is
# reading its headers. The installed .NET SDK/cache stays read-only.
if (!(Test-Path -LiteralPath $cpp)) { Expand-Archive -LiteralPath $archive -DestinationPath $CacheRoot -Force }
$actualSourceHash = (Get-FileHash -LiteralPath $cpp -Algorithm SHA256).Hash
if ($actualSourceHash -notin @($sourceHash, $patchedSourceHash)) {
    throw 'Unexpected webgpu.cpp source; refusing to apply the private namespace fix.'
}
$source = [IO.File]::ReadAllText($cpp)
$start = '}  // extern "C"' + "`n`n" + '// ----------------------------------------------------------------------------' + "`n" + '// Implementation details that are not exposed upwards in the API.'
$end = '// ----------------------------------------------------------------------------' + "`n" + '// WGPU struct declarations.'
if ($actualSourceHash -eq $sourceHash) {
    if (!$source.Contains($start) -or !$source.Contains($end)) { throw 'Emdawn namespace patch anchors differ.' }
    $source = $source.Replace($start, '}  // extern "C"' + "`n`nnamespace {`n`n" + '// ----------------------------------------------------------------------------' + "`n" + '// Implementation details that are not exposed upwards in the API.')
    $source = $source.Replace($end, "}  // namespace`n`n" + '// ----------------------------------------------------------------------------' + "`n" + '// WGPU struct declarations.')
    [IO.File]::WriteAllText($cpp, $source, [Text.UTF8Encoding]::new($false))
}

# In 3.1.56 these are native exports rather than JS-library ($-prefixed) helpers.
$jsBinding = Join-Path $package 'webgpu/src/library_webgpu.js'
$jsSource = [IO.File]::ReadAllText($jsBinding)
$stackDeps = "errorCallback__deps: ['`$stackSave', '`$stackRestore', '`$stringToUTF8OnStack']"
$correctedStackDeps = "errorCallback__deps: ['stackSave', 'stackRestore', '`$stringToUTF8OnStack']"
if ($jsSource.Contains($stackDeps)) {
    $jsSource = $jsSource.Replace($stackDeps, $correctedStackDeps)
    [IO.File]::WriteAllText($jsBinding, $jsSource, [Text.UTF8Encoding]::new($false))
} elseif (!$jsSource.Contains($correctedStackDeps)) { throw 'Emdawn stack dependency patch anchor differs.' }

# The pinned package forgets to restore the native stack after a rejected
# buffer.mapAsync. Keep its error message alive through the C++ callback, then
# restore the stack before removing the buffer's unmap bookkeeping.
$mapReject = "      _emwgpuOnMapAsyncCompleted(futureId, status, {{{ gpu.passAsPointer('messagePtr') }}});" + "`n" + '      delete WebGPU.Internals.bufferOnUnmaps[bufferPtr];'
$correctedMapReject = "      _emwgpuOnMapAsyncCompleted(futureId, status, {{{ gpu.passAsPointer('messagePtr') }}});" + "`n" + '      stackRestore(sp);' + "`n" + '      delete WebGPU.Internals.bufferOnUnmaps[bufferPtr];'
if ($jsSource.Contains($mapReject)) {
    if ($jsSource.IndexOf($mapReject) -ne $jsSource.LastIndexOf($mapReject)) { throw 'Emdawn mapAsync stack patch anchor is ambiguous.' }
    $jsSource = $jsSource.Replace($mapReject, $correctedMapReject)
    [IO.File]::WriteAllText($jsBinding, $jsSource, [Text.UTF8Encoding]::new($false))
} elseif (!$jsSource.Contains($correctedMapReject)) { throw 'Emdawn mapAsync stack patch anchor differs.' }

$packs = Join-Path $DotNetRoot 'packs'
if (!$PackVersion) {
    $sdkVersions = @(Get-ChildItem -LiteralPath (Join-Path $packs "Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.$hostRid") -Directory |
        Where-Object { ([version]$_.Name).Major -eq 10 } | Sort-Object { [version]$_.Name } -Descending)
    if (!$sdkVersions) { throw '.NET 10 wasm-tools with Emscripten 3.1.56 is missing.' }
    $PackVersion = $sdkVersions[0].Name
}
$sdkTools = Join-Path $packs "Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.$hostRid/$PackVersion/tools"
$cacheTools = Join-Path $packs "Microsoft.NET.Runtime.Emscripten.3.1.56.Cache.$hostRid/$PackVersion/tools"
$nodeTools = Join-Path $packs "Microsoft.NET.Runtime.Emscripten.3.1.56.Node.$hostRid/$PackVersion/tools"
$node = Join-Path $nodeTools $(if ($windowsHost) { 'bin/node.exe' } else { 'bin/node' })
if ($linuxHost) {
    # The workload manifest has no Linux Python pack; the host supplies python3.
    $python = (Get-Command python3 -ErrorAction Stop).Source
} else {
    $pythonTools = Join-Path $packs "Microsoft.NET.Runtime.Emscripten.3.1.56.Python.$hostRid/$PackVersion/tools"
    $python = Join-Path $pythonTools $(if ($windowsHost) { 'python.exe' } else { 'bin/python3' })
}
$emscript = Join-Path $sdkTools 'emscripten/em++.py'
$include = Join-Path $package 'webgpu/include'
$cppInclude = Join-Path $package 'webgpu_cpp/include'
$jsRoot = Join-Path $package 'webgpu/src'
$jsFiles = @('library_webgpu_enum_tables.js', 'library_webgpu_generated_struct_info.js', 'library_webgpu_generated_sig_info.js', 'library_webgpu.js')
foreach ($tool in @($python, $emscript, $node)) {
    if (!(Test-Path -LiteralPath $tool)) { throw "Matching .NET Emscripten tool missing: $tool" }
}

$env:DOTNET_EMSCRIPTEN_LLVM_ROOT = Join-Path $sdkTools 'bin'
$env:DOTNET_EMSCRIPTEN_BINARYEN_ROOT = $sdkTools
$env:DOTNET_EMSCRIPTEN_NODE_JS = $node
$env:EM_CACHE = Join-Path $cacheTools 'emscripten/cache'
$env:FROZEN_CACHE = 'True'
$env:EMSDK_PYTHON = $python
$env:PYTHONUTF8 = '1'

# Compile the source with the exact same compiler used by the .NET 10 runtime.
# Direct source integration avoids asking the old port manager to modify the SDK cache.
$object = Join-Path $CacheRoot 'webgpu.o'
& $python $emscript $cpp '-std=c++20' '-fno-exceptions' '-fno-rtti' '-O2' '-DNDEBUG' '-g' '-isystem' $include '-c' '-o' $object
if ($LASTEXITCODE -ne 0) { throw 'Emdawn C++ compilation failed.' }

$settings = [ordered]@{
    version = $version
    archiveSha256 = $archiveHash
    originalSourceSha256 = $sourceHash
    patchedSourceSha256 = (Get-FileHash -LiteralPath $cpp -Algorithm SHA256).Hash
    emscriptenVersion = '3.1.56'
    dotnetPackVersion = $PackVersion
    hostRid = $hostRid
    package = $package.Replace('\', '/')
    include = $include.Replace('\', '/')
    cppInclude = $cppInclude.Replace('\', '/')
    cppSource = $cpp.Replace('\', '/')
    object = $object.Replace('\', '/')
    jsLibraries = @($jsFiles | ForEach-Object { (Join-Path $jsRoot $_).Replace('\', '/') })
    closureExterns = (Join-Path $jsRoot 'webgpu-externs.js').Replace('\', '/')
}
$manifest = Join-Path $CacheRoot 'toolchain.json'
[IO.File]::WriteAllText($manifest, ($settings | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))

if ($Verify) {
    $smokeSource = Join-Path $PSScriptRoot 'smoke-emdawn.cpp'
    $smokeOutput = Join-Path $CacheRoot 'smoke.js'
    $linkArgs = @($smokeSource, $object, '-std=c++20', '-O1', '-fno-exceptions', '-isystem', $include, '-sWASM_BIGINT', '-sENVIRONMENT=node', '-sASSERTIONS=1', '-o', $smokeOutput)
    foreach ($js in $settings.jsLibraries) { $linkArgs += @('--js-library', $js) }
    & $python $emscript @linkArgs
    if ($LASTEXITCODE -ne 0) { throw 'Emdawn JavaScript final link failed.' }
    & $env:DOTNET_EMSCRIPTEN_NODE_JS $smokeOutput
    if ($LASTEXITCODE -ne 0) { throw 'Emdawn instance/lifetime smoke test failed.' }
    $settings.verified = 'C++ object, JS final link, Node instance/lifetime'
    [IO.File]::WriteAllText($manifest, ($settings | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
}

Write-Host "Prepared Emdawn $version with Emscripten 3.1.56. Integration paths: $manifest"
