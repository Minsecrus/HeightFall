# Emdawn WebGPU for the .NET 10 Emscripten toolchain

`prepare-emdawn.ps1 -Verify` downloads the pinned, text-only Dawn package
`v20250531.224602`, verifies its SHA-256, isolates private C++ implementation
types, and compiles its C++ with the installed .NET 10 Emscripten 3.1.56 compiler.
It then links all four WebGPU JavaScript libraries and runs an instance/lifetime
smoke check. The script does not change the installed SDK or its frozen cache.
The cache and generated integration manifest are under `.engine-cache/emdawnwebgpu`.
The script discovers the active `dotnet` executable's actual installation root
(following symlinks), selects Emscripten packs for the host OS and architecture,
and uses one exact pack version throughout. `-DotNetRoot` and `-PackVersion` can
pin an installation explicitly. Linux uses system `python3`, because the .NET
workload supplies Python packs only for Windows and macOS; Unix Node is at
`tools/bin/node`, and macOS Python is at `tools/bin/python3`.

The preparation script is original project code. It does not incorporate the
Studio Foundation PolyForm build tools. The private namespace fix independently
backports the two namespace boundaries from Dawn commit
[`2752c7d71a190c8512f38ceda922253d23876fb4`](https://github.com/google/dawn/commit/2752c7d71a190c8512f38ceda922253d23876fb4).
They isolate Dawn's private `RefCounted` implementation from Godot's same-named
class so the linker cannot select an unrelated constructor.

One compatibility correction changes the `errorCallback` JS dependency list
from `$stackSave` / `$stackRestore` to `stackSave` / `stackRestore`. Emscripten
3.1.56 exposes these as native exports; modern versions expose JS-library
helpers. The generated structure/signature tables, integer/pointer macros,
callback helpers, and C++20 compilation use the matching installed toolchain.

The pinned package also omits `stackRestore(sp)` after the failure callback in
`emwgpuBufferMapAsync`. The preparation script adds it after
`_emwgpuOnMapAsyncCompleted(...)` and before deleting `bufferOnUnmaps`, preventing
rejected GPU readbacks from consuming native stack space. Both JS corrections
are idempotent and reject unexpected patch anchors. They only change the JS
bridge; the C++ object does not need to be rebuilt for this correction.

The downloaded package retains its upstream LICENSE files and file notices:
The Dawn C++ wrapper headers and package tools use BSD-3-Clause;
`webgpu.cpp` uses Emscripten's MIT / University of Illinois-NCSA dual licensing,
and the JavaScript files retain their Emscripten license notices. Package sources and
upstream documentation are available in the
[Dawn release](https://github.com/google/dawn/releases/tag/v20250531.224602) and
[Emdawn documentation](https://github.com/google/dawn/blob/main/src/emdawnwebgpu/pkg/README.md).

SCons places `webgpu/include` before Emscripten's bundled older WebGPU
headers. The preparation script compiles `webgpu/src/webgpu.cpp` with
`-std=c++20 -fno-exceptions`; the .NET host includes that object through
`NativeLibrary` alongside `libgodot.a`. The final .NET WASM link must use the JS
libraries in manifest order: enum tables, generated struct info, generated
signature info, then `library_webgpu.js`. Do not enable `-sUSE_WEBGPU=1`, which
would introduce the older competing WebGPU binding. Closure builds using
Emscripten 3.1.56 also need the package's `webgpu-externs.js` passed through
`--closure-args=--externs=...`.

The Node smoke test verifies source compilation, link compatibility, and native
lifetime handling. It does not establish GPU rendering correctness; browser
validation remains necessary after the Godot renderer and .NET host are integrated.

`verify-tint-floats.ps1` compiles the patched Tint parser with the same compiler
and checks 129 numeric boundaries in Node. The main native build runs this check
after toolchain preparation. To run it independently in one PowerShell session:

```powershell
& ./engine/toolchain/prepare-emdawn.ps1
& ./engine/toolchain/verify-tint-floats.ps1
```
