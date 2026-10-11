# Godot WebGPU 引擎组件署名

HeightFall 的游戏代码继续适用仓库根目录的 [MIT LICENSE](../LICENSE)。自编网页引擎由以下第三方组件及本仓库的适配组成；第三方组件保留各自版权和许可证。本文件是归属索引，不替代原始许可证文本或源文件中的版权声明。

## Godot Engine 与 2dog

- **Godot Engine**：MIT。Copyright (c) 2014-present Godot Engine contributors；Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur。原始作者名单与依赖清单由 Godot 的 `AUTHORS.md`、`COPYRIGHT.txt` 和各依赖许可证记录。[Godot 4.7.2 源码](https://github.com/godotengine/godot/tree/4.7.2-stable)。
- **2dog / thygrrr Godot fork**：MIT。2dog 的版权声明为 Copyright (c) 2026 Moritz Voss & Contributors。本仓库使用 Godot 4.7.2 fork 提交 `5fa0f824bf9223ab240658b85c061c171b5de677`，以及匹配的 2dog C# 绑定和宿主。[2dog 许可证](https://github.com/outfox/2dog/blob/4dbaf98d057b4f2113723a0114da75141dfd1cc1/LICENSE)、[实际引擎提交](https://github.com/thygrrr/godot/tree/5fa0f824bf9223ab240658b85c061c171b5de677)。

## WebGPU 后端与补丁整合

原始 Godot WebGPU 后端来自 **David Walter / dwalter/godotwebgpu**，归属保留给原作者及 Godot contributors；其 Godot 源码及新增驱动文件保留 MIT 版权声明。引用的源码谱系提交为 `f329e39ce8db7acaa5c9d6628a530fb769969228`。[原始后端](https://github.com/dwalter/godotwebgpu/tree/f329e39ce8db7acaa5c9d6628a530fb769969228)、[许可证](https://github.com/dwalter/godotwebgpu/blob/f329e39ce8db7acaa5c9d6628a530fb769969228/LICENSE.txt)。

**Studio Foundation / LX Solutions** 整理了 WebGPU 后端、Godot 4.7.1 接口适配及浏览器渲染修复。本仓库仅取用提交 `ef064f08102bee78e7b27603b6ce40e92f3645ad` 的 `engine/patches/0001` 至 `0033` 引擎补丁及所含 SPIR-V / Tint 源码，再将它们适配到上述 Godot 4.7.2 / 2dog 基底。[固定补丁来源](https://github.com/lxsolutions/studio-foundation/tree/ef064f08102bee78e7b27603b6ce40e92f3645ad/engine/patches)。

该提交的 [NOTICE.md](https://github.com/lxsolutions/studio-foundation/blob/ef064f08102bee78e7b27603b6ce40e92f3645ad/NOTICE.md) 在 “Studio Foundation WebGPU integration” 专节将引擎整合谱系列为 **MIT，版权由各贡献者保留**，并限定补丁内容为 WebGPU 实现及所需 SPIR-V / Tint 源码。该组件声明与 Studio Foundation 整体项目的许可范围有区别；其顶层 LICENSE 为 PolyForm Perimeter 加附加条款。本项目的引擎补丁归属说明不将 Studio Foundation 的全部工具、模板或项目声明为 MIT。

实际下载补丁的文件名、提交与 SHA-256 均记录在 [engine-lock.json](engine-lock.json)。源文件中的 Godot、WebGPU 原作者及第三方版权声明必须保留。

本仓库另外提供 `0034` 至 `0041` 的八个本地引擎适配补丁，涵盖构建平台、Tint 数字解析、MSAA / 深度采样绑定及资源生命周期。适配内容适用本仓库的 MIT LICENSE，并继续保留所修改源码的原有版权和许可证；技术说明和验证范围见 [网页构建说明](../docs/web-build.md)。

## 着色器编译依赖

- **Tint**：BSD 3-Clause。Copyright 2017-2023 The Dawn & Tint Authors；各源文件另有其自身年份和版权声明。用于 SPIR-V 到 WGSL 的转换。补丁随附的文本位于引擎 `thirdparty/tint/LICENSE`。[Dawn / Tint 项目](https://dawn.googlesource.com/dawn)。
- **SPIRV-Tools**：Apache License 2.0。各文件保留 Google、Khronos 及其他贡献者的版权，例如 `source/libspirv.cpp` 的 Copyright (c) 2016 Google Inc.；实际文件和许可证为 `thirdparty/spirv-tools/LICENSE`。[SPIRV-Tools 项目](https://github.com/KhronosGroup/SPIRV-Tools)。
- **SPIRV-Headers**：MIT，附 Khronos 标准修改提示，文本为 `thirdparty/spirv-headers/LICENSE`。主版权声明为 Copyright (c) 2015-2024 The Khronos Group Inc.；修改后的头文件不应被表述为未修改的 Khronos 标准。[SPIRV-Headers 项目](https://github.com/KhronosGroup/SPIRV-Headers)。
- Godot 原有着色器编译及其他依赖继续保留 `COPYRIGHT.txt` 和各 `thirdparty/` 许可证中的声明。

## Emdawn WebGPU 桥

使用 Google Dawn 的 **Emdawn WebGPU** 包 `v20250531.224602`，由本仓库工具链脚本校验并直接编译，以适配 .NET 的 Emscripten 3.1.56。原包源代码、头文件和 JS 桥的版权声明均保留；本仓库另对私有 C++ 命名空间和旧 Emscripten JS 依赖名做兼容适配。[固定发布包](https://github.com/google/dawn/releases/tag/v20250531.224602)。

- 包内 `webgpu/src/LICENSE` 包含 Emscripten 的 **MIT / University of Illinois-NCSA** 双许可，Copyright (c) 2010-2014 Emscripten authors；核心 `webgpu.cpp` 文件头为 Copyright 2024 The Emscripten Authors。
- C++ 包装头文件、包工具及 `webgpu_cpp/LICENSE` 使用 **BSD 3-Clause**，Copyright 2017-2025 The Dawn & Tint Authors。
- `webgpu.h` 另保留 **BSD 3-Clause** 的 Copyright 2019 WebGPU native developers；具体按对应文件头和包内许可证适用。

下载源码包与本地适配结果的校验值记录在 `engine/toolchain/prepare-emdawn.ps1`，准备后的工具链记录位于 `.engine-cache/emdawnwebgpu/toolchain.json`。这项记录说明所使用的源码和工具链，不替代完整场景渲染和游戏运行验证；当前验证状态见 [网页构建说明](../docs/web-build.md)。

## .NET WebAssembly 运行时

网页宿主使用 .NET 的 Mono `browser-wasm` 运行时，源项目为 [dotnet/runtime](https://github.com/dotnet/runtime)。随当前构建使用的 `Microsoft.NETCore.App.Runtime.Mono.browser-wasm` 运行时 pack 提供 `LICENSE.TXT`，许可为 **MIT**，Copyright (c) .NET Foundation and Contributors。

该运行时还包含具有各自许可证的第三方组件，其原始声明由同一 pack 的 `THIRD-PARTY-NOTICES.TXT` 提供。发布脚本根据匹配工具链的 pack 版本复制这两个实际文件至 `licenses/dotnet.txt` 和 `licenses/dotnet-third-party.txt`，保留原文；本文件的 MIT 说明不替代这些第三方声明。

## 发布文件

发布引擎二进制、WebAssembly 和 JS 时，应随静态产物保留本文件、Godot 的 `LICENSE.txt` / `COPYRIGHT.txt`，以及上述实际参与构建组件的许可证文本和版权声明。发布脚本还携带 `licenses/WebGPU-native.txt` 与 .NET 运行时的许可 / 第三方声明。源代码修改保留原有文件头；项目根 MIT LICENSE 不能替代这些第三方许可证。
