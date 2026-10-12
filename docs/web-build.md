# Godot WebGPU 网页构建

网页宿主直接加载 `game/project.godot` 的场景，运行与桌面共用的 C# 游戏脚本、Godot HUD、输入和 Effects。网页的渲染调用由自编 Godot 的 WebGPU 后端执行；触屏布局、头顶状态条和商店也使用这套 Godot UI 与 `HeightFall.Sim` 规则。桌面游戏和纯逻辑类库 `HeightFall.Sim` 保持 `net8.0`；网页宿主使用 `net10.0`，游戏项目仅在 `HeightFallWebGpu=true` 时切换为 `net10.0` 和网页绑定。

这是实验性的第三方引擎整合，不是官方 Godot 导出模板。启动页指定 `--rendering-driver webgpu --rendering-method forward_plus`，原生库使用 `webgpu=yes opengl3=no` 构建。WebGPU、Forward+ 和实际游戏效果必须在浏览器中验证；复用相同场景和脚本不代表所有显卡、浏览器都能得到逐像素相同的画面。

## 工具和源码

需要 Git、PowerShell 7、[uv](https://docs.astral.sh/uv/getting-started/installation/) 和 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。为所使用的 SDK 安装 WebAssembly 工具链：

```powershell
dotnet workload install wasm-tools
```

`uv` 使用 `engine/pyproject.toml` 和 `engine/uv.lock` 准备本地 Python / SCons 环境。编译器、Node 和 Emscripten sysroot 使用 .NET `wasm-tools` 的匹配组件；网页引擎和最终 .NET 宿主都使用 **Emscripten 3.1.56**。首次构建需要下载依赖并编译 Godot C++ 源码，应预留内存、磁盘空间和编译时间。

源码锁定在 [engine/engine-lock.json](../engine/engine-lock.json)：

- Godot **4.7.2** 的 2dog fork：`thygrrr/godot`，提交 `5fa0f824bf9223ab240658b85c061c171b5de677`。
- Studio Foundation WebGPU 引擎整合：提交 `ef064f08102bee78e7b27603b6ce40e92f3645ad` 的 **33 个补丁**，继承 `dwalter/godotwebgpu` 的后端；每个下载补丁都校验 SHA-256，再应用本仓库的适配补丁。
- Emdawn WebGPU：固定 `v20250531.224602`，由 `engine/toolchain/prepare-emdawn.ps1` 校验源码包并用同一 Emscripten 3.1.56 直接编译 C++ 接口及链接 JS 桥。

本仓库在这 33 个补丁之后应用八个本地适配：

- `0034-heightfall-runtime-wgsl-build.patch`：所有构建宿主默认生成空的 WGSL 预编译查找表，着色器在运行时通过引擎链接的 Tint 转换。只有显式设置 `GODOT_WEBGPU_PRECOMPILE=1` 才额外构建宿主侧 `tint_convert_cli`，执行 GLSL → SPIR-V → WGSL 预编译；该可选路径需要另行验证。
- `0035-heightfall-libgodot-webgpu-platform.patch`：保留 2dog 的库 / Mono 平台支持并加入 WebGPU，启用 `WEBGPU_ENABLED` / `RD_ENABLED`，使用固定 Emdawn 包的 C 与 C++ 头文件。宿主直接链接对应对象和 JS 桥，保持 .NET Emscripten 3.1.56 ABI 及只读 SDK 缓存。
- `0036-heightfall-emscripten-float-parsing.patch`：适配 Emscripten 3.1.56 旧 libc++ 缺少浮点 `std::from_chars` 重载的问题。Tint 的 float / double 解析在该工具链下改用固定 C locale 的转换，保留输入格式、完整消费及溢出 / 下溢检查；其他工具链继续使用 `std::from_chars`。
- `0037-heightfall-webgpu-msaa-bindings-and-resolve.patch`：让 MSAA 纹理绑定与 WGSL 的无 sampler 读取一致，保留准确的绑定布局，并通过不绘制图元的 render pass 执行颜色 resolve。
- `0038-heightfall-webgpu-depth-float-bindings.patch`：在 `UnfilterableFloat` 绑定中保留真实的多重采样深度纹理，包括 `Depth32Float`，避免将其替换成占位纹理；格式能力依据 [WebGPU 规范](https://www.w3.org/TR/webgpu/#texture-format-caps)。
- `0039-heightfall-webgpu-depth-aspect-views.patch`：为深度纹理采样创建 `DepthOnly` 视图，避免深度 / 模板纹理的 `All` 视图在 MSAA resolve 的浮点采样绑定中被拒绝；附件视图继续保留所需的深度 / 模板 aspect。
- `0040-heightfall-webgpu-command-buffer-lifetime.patch`：提交后释放命令缓冲区的 API 引用，并在重置、重新录制和销毁命令池时清理持有的对象，避免 Emdawn 的 JS 命令缓冲区引用逐帧累积。
- `0041-heightfall-webgpu-explicit-resource-destruction.patch`：对引擎拥有的 buffer / texture 显式调用 `Destroy`，再释放 API 引用，修正仅 `Release` 时底层资源生命周期没有及时结束的问题；借用的资源和 `mapAsync` 路径保持原逻辑。

引擎源码、原生输出和工具链准备文件存放在 `.engine-cache/`。准备脚本拒绝向该目录外写引擎源码；缓存记录包含锁文件和本地补丁的 SHA-256 源码签名，以及补丁所修改的受控源码文件指纹。每次重用都会核对锁定的 Git 提交、源码签名和这些文件的内容。

锁文件或本地补丁变化时，必须准备新的 `.engine-cache` checkout。脚本会拒绝默默沿用旧源码；受控文件与已记录指纹不一致时也会报错，应先检查差异并重新准备匹配的源码。

开源组件归属见 [engine/NOTICE.md](../engine/NOTICE.md)。使用范围是所列引擎补丁及其依赖；各项目工具和其他内容适用它们各自的许可证。

## 编译原生 WebGPU 引擎

在仓库根目录运行：

```powershell
.\tools\build-godot-webgpu.ps1
```

默认使用 4 个并行编译任务。可按本机资源调整，例如：

```powershell
.\tools\build-godot-webgpu.ps1 -Jobs 4 -VerifyToolchain
```

`-VerifyToolchain` 额外检查 Emdawn C++ 编译、JS 最终链接和 Node 中的实例生命周期；它不验证浏览器 GPU 或游戏画面。脚本随后仍会编译完整原生引擎。

主原生构建脚本默认运行 Tint 数字兼容回归：`engine/toolchain/verify-tint-floats.ps1` 使用同一 Emscripten 编译实际 vendored Tint 解析器，运行 `engine/tests/tint-float-parse.cc` 中的 **129 项边界检查**，覆盖负零、浮点范围、次正规数、错误格式、特殊值和整数边界。回归失败会中止原生构建；这项检查不依赖 `-VerifyToolchain` 开关。

已准备好引擎源码时，可在同一个 PowerShell 会话中单独运行：

```powershell
& .\engine\toolchain\prepare-emdawn.ps1
& .\engine\toolchain\verify-tint-floats.ps1
```

回归输出位于 `.engine-cache/tint-float-test/`，不修改安装的 .NET SDK 缓存。

成功后，原生 payload 位于：

```text
.engine-cache/2dog/godot/bin/web/template_release/
  libgodot/libgodot.a
  libgodot/config.json
  libgodot/js_library/...
  shell/godot.js
```

`libgodot.a` 将静态链接进 .NET 的 WebAssembly 运行时。它必须与网页宿主使用的工具链一致。原版 2dog 的预编译浏览器包没有这个自定义 WebGPU 后端，不能作为该原生输出的替代品。

## 发布 Godot 游戏宿主

新的宿主项目是 `game/HeightFall.WebGpu/HeightFall.WebGpu.csproj`。从仓库根目录完成引擎编译及宿主发布的统一入口为：

```powershell
.\tools\build-webgpu.ps1 -Jobs 4
```

默认使用 `Release`。已完成上节的原生编译时，可直接发布宿主：

```powershell
.\tools\build-webgpu.ps1 -SkipEngineBuild
```

需要迭代调试网页渲染时，可使用：

```powershell
.\tools\build-webgpu.ps1 -SkipEngineBuild -Configuration Debug
```

Debug 宿主设置 `EmccLinkOptimizationFlag=-O0`，用于减少本地迭代中的链接优化工作；自编原生引擎仍使用 `template_release`。Release 保持 SDK 的链接优化，设置 `WasmNativeDebugSymbols=false`，不生成原生调试符号。发布脚本在未预设 `BINARYEN_CORES` 时使用 `min(Jobs, 2)`，限制 WebAssembly 优化阶段的并行任务；已有值会保留，脚本结束时恢复调用前的环境变量。这些选项不免除大型原生链接与优化所需的内存。

宿主的对应直接发布命令是：

```powershell
dotnet publish game/HeightFall.WebGpu/HeightFall.WebGpu.csproj -c Release -p:HeightFallWebGpu=true
```

`HeightFallWebGpu=true` 必须作为全局 MSBuild 属性传入，让 restore 和 build 都选择网页绑定。桌面继续使用官方 Godot 绑定，网页使用匹配版本的 2dog 绑定；网页游戏程序集输出隔离在 `game/.godot/mono/webgpu/`，桌面输出保持原路径。

`game/HeightFall.csproj` 先声明无条件 `net10.0`，再通过 `HeightFallWebGpu != true` 的条件属性选择桌面 `net8.0`。这是为了让 2dog 导入 / 导出编辑器的 XML 最低框架检查不会把桌面配置自动改写为 .NET 10；真正使用的框架以 MSBuild 求值为准。已确认默认求值为 `net8.0`，传入 `HeightFallWebGpu=true` 时为 `net10.0`。

完整静态产物位于 `game/HeightFall.WebGpu/AppBundle/`。发布脚本还会添加 `.nojekyll`、第三方署名，以及 Godot、2dog、Tint、SPIRV-Tools、SPIRV-Headers、Emdawn、WebGPU native、Noto CJK 字体和 .NET WebAssembly 运行时的许可证 / 第三方声明文件；它不会启动浏览器或部署网站。

发布时必须包含 Godot 资源包、引擎脚本、WebAssembly、`_framework/` 及许可证文件，并保留所有相对路径。宿主会校验本地 `libgodot.a`、包含 WebGPU 设备交接逻辑的 Godot shell，以及 Emdawn 对象和 JS 桥；缺失时构建应报错。

## 本机打开

网页需要 HTTP 服务加载 WebAssembly 和 Godot 资源，不能双击 `index.html`。将静态产物准备到 `game/HeightFall.WebGpu/AppBundle/` 后，在仓库根目录运行：

```powershell
dotnet dnx dotnet-serve -d game/HeightFall.WebGpu/AppBundle -a 127.0.0.1 -p 8000
```

随后打开 <http://127.0.0.1:8000/>；按 `Ctrl+C` 停止服务。参数见 [dotnet-serve 官方说明](https://github.com/natemcmaster/dotnet-serve#usage)。正式部署使用 HTTPS；本机 loopback 可作为安全环境，WebGPU 的浏览器支持范围见 [MDN WebGPU 文档](https://developer.mozilla.org/en-US/docs/Web/API/WebGPU_API)。

操作和 HUD 使用桌面与网页共用的 Godot 游戏代码。浏览器需要能提供 WebGPU adapter/device 及所需功能、限制；启动失败时应查看页面错误和浏览器控制台。硬件加速、浏览器版本、设备限制与着色器转换都可能影响结果。

## 当前操作、HUD 与商店

键盘和鼠标继续支持桌面操作：右键移动或攻击，指向技能按住 Q / W / E / R / F 瞄准并松开释放，或点击技能图标后左键确认；无目标技能按下即用。右键 / Esc 取消瞄准，1–4 学习技能，B 回城、V 恢复、S 停止、Tab 切换测试英雄。镜头仍支持 Y 锁定 / 解锁、中键拖动、方向键和鼠标贴边平移、空格回到英雄。浏览器有键盘和鼠标时也可使用这些操作。

触屏模式使用左下角移动摇杆和右下角的环形技能布局，技能围绕普攻按钮排列。按住指向技能后拖动调整方向和距离，松开该手指施放；拖到取消区后松开或点取消图标可放弃施法。无目标技能按下即用。左手移动与右手瞄准可同时进行，各控件保留自己的触点；普攻支持按住，回城、恢复、技能学习和泉水内的召唤师更换均通过图标操作。手机技能按钮隐藏 Q / W / E / R / F / B / V 字母，保留图标、技能等级、冷却和进度。

两种布局的左上角都只显示金币及商店入口；生命、法力和等级移到英雄头顶，等级圆徽章位于血条左侧。点击或轻触金币打开同一个商店，选择装备后购买，选择已有装备格后出售；商店中还可切换当前测试英雄。商店打开时屏蔽地图和镜头操作，关闭后恢复控制，游戏模拟继续推进。

商店当前有 9 件装备、6 格背包，按全部 / 攻击 / 防御 / 移动分类。购买直接扣除模拟层金币并增加真实装备属性，出售按原价的 70% 返还金币；金币不足和背包满由 `World.CheckBuy` 校验，桌面、触屏和网页共用相同规则。装备数据与具体操作见 [设计文档](design.md#商店与装备)。

中文使用仓库内的 Noto Sans CJK SC 子集作为原字体的 fallback，保留拉丁字母和数字的原字体。字体覆盖商店、装备名和模拟层错误提示，来源与重建命令见 [字体说明](../game/fonts/README.md)，许可证见 [OFL.txt](../game/fonts/OFL.txt)；网页发布会复制为 `licenses/Noto-CJK-OFL.txt`。

触屏布局使用 960×540 逻辑基准并随横竖屏调整；桌面保留 1600×900 基准。网页不锁定屏幕方向：主要指针为触屏（`pointer: coarse`）且视口为竖屏时，游戏画面显示后在顶部提示「横屏游玩体验更好」，转为横屏后自动隐藏，点 × 关闭后通过 `localStorage` 记住不再显示。Web 安全边距从 CSS `safe-area-inset-*` 取得，再转换到 Godot 逻辑坐标。浏览器的触摸取消、失焦、页面隐藏或布局变化会取消持有的手势，避免松开时误施法或摇杆卡住。

Windows、Linux、macOS、Android、iOS 和 Web 是这套 Godot 源代码的移植目标。目前实际运行验证以 Windows、下节记录的 WebGPU 浏览器检查和 Android 调试包为限，仓库导出预设有 WebGPU 和 Android；其他平台仍需要各自的 SDK、模板、导出配置和设备验证，不能据此声称已有六平台发布产物。

Android 导出需要 Godot 4.7.2 .NET 版的 Android 导出模板、Android SDK，以及编辑器设置中的 Java SDK 路径。该版本的 .NET Android 模板运行在 .NET 9 上，因此 `game/HeightFall.csproj` 在 `GodotTargetPlatform` 为 `android` 时改用 `net9.0`；项目设置已开启 Android 必需的 ETC2 / ASTC 纹理导入。通过 WinGet 安装的 Godot 需要用安装目录中的真实可执行文件导出，经由 `WinGet\Links` 中的链接启动时会找不到 `GodotSharp`。

本次触屏、HUD 和商店改动可先运行以下检查；命令列出的是验证入口，不表示本次所有检查已经完成：

```powershell
dotnet build HeightFall.sln -c Debug --nologo
dotnet test tests/HeightFall.Sim.Tests/HeightFall.Sim.Tests.csproj -c Debug --nologo
.\tools\build-webgpu.ps1 -SkipEngineBuild -Configuration Debug
```

运行时应分别核对键鼠与触屏买卖、6 格容量和失败提示、头顶生命 / 法力 / 等级同步、横竖屏及刘海边距，以及双指移动加瞄准、取消、失焦和商店开关。真实 Android / iOS 设备的触摸和 WebGPU 能力需另行确认；桌面浏览器的移动仿真不能代替设备覆盖。

## 验证与部署状态

2026-10-12 的手机验证（Android 16，Adreno 830）：Android 调试包导出、安装并启动成功，使用 Vulkan Mobile 渲染器，系统方向切到竖屏时游戏仍保持横屏，触屏布局自动启用；Mobile 渲染器不支持 SSAO，启动时有一条忽略该设置的警告。手机 Chrome 154 的网页竖屏时正确显示横屏提示，提示位于金币和顶栏下方。网页频繁卡顿 200–460ms 的原因是小兵堵塞后重新寻路时终点被占，A* 搜遍整张地图（每次约 1.3 万格），在 wasm 解释器里开销很大；限制搜索后，桌面基准 5 分钟对局的寻路总耗时由约 525ms 降到约 13ms，4052 次寻路的终点与原结果完全一致。修复后的网页尚未在手机上重新测量卡顿。

2026-10-11 的触屏、HUD 和商店本机验证：`HeightFall.sln` Debug 构建通过，0 警告、0 错误；21 项模拟层测试通过。临时 Godot 输入场景通过 81 项断言，使用真实 `Input.ParseInputEvent` 核对双指移动与瞄准、施法与取消、触摸取消、失焦、横竖屏、同批输入切英雄、触摸与桌面鼠标买卖、商店输入屏蔽及桌面按键松开施法。临时场景未加入发布产物。Windows Vulkan 的 844×390、390×844 和 1600×900 商店截图已检查，中文及布局完整。

该轮 Debug 和优化 Release 网页发布均成功，Release `godot.wasm` 为 51,353,858 字节，加入字体后的 PCK 为 233,184 字节。Chrome 的 Release 已核对金币鼠标点击打开商店、中文商品与差额，及 844×390 触屏的技能学习、摇杆移动、双指移动与瞄准、系统取消不施法、拖动松指施法与冷却。这是桌面浏览器仿真验证，不代表真实 Android 或 iOS 设备验证；后续提交的 CI 和线上状态须单独核对。

以下引擎构建和运行数字记录的是新增触屏布局、商店与头顶等级之前的首轮验证。本轮已修改共享游戏脚本和 HUD，这些历史结果不表示新增界面、商店及后续提交的 CI 已通过。

截至 2026-10-11，已在全新 checkout 上完成锁定基底、33 个上游补丁及 8 个本地适配的准备，并通过缓存重用验证；全新源码与当前编译源码的 **1,307 个受控文件**指纹一致。WebGPU / Mono 构建配置 dry-run、固定 Emdawn 包的 C++ 对象 / JS 链接 / Node 实例生命周期、129 项 Tint 数字边界回归均已通过。

桌面 `net8.0` 配置的根 `HeightFall.sln` Debug 构建已通过，结果为 0 警告、0 错误；7 项 `HeightFall.Sim` 逻辑测试全部通过。官方 Godot 4.7.2 .NET 引擎的 headless 启动检查经过 20 帧后正常退出，退出码为 0。另已使用 Vulkan 1.4.335 / Forward+ 在 Intel Arc B390 上运行原桌面游戏并保存第 20 帧截图，确认场景、泉水、SSAO / 阴影及 HUD 显示，进程正常退出。检查使用 Winget 链接解析后的实际引擎可执行文件，避免通过链接启动时找不到 .NET 程序集的问题。上述结果验证桌面启动和渲染，不验证浏览器 3D 渲染。

完整 Godot WebGPU 原生库的 SCons 构建已通过；加入 `0041` 适配后的该轮增量构建耗时 16.67 秒，退出码为 0，此时间不代表首次完整编译的耗时。包含 `0041` 的 Debug / `-O0` 和优化 Release 宿主发布均以 0 退出。该轮 Release 的 `godot.wasm` 为 **51,353,858 字节**，原游戏 PCK 为 **4,428 字节**，已核对没有混入旧 `HeightFall.web` 宿主内容。后续加入字体等资源会改变 PCK 大小。

该轮 Chrome 实际运行报告 **WebGPU 1.0 / Forward+**，`0041` Release 显示原 3D 场景、泉水、格子、英雄及 Godot HUD，并保存实际画面截图。首轮验证时 `game/scripts/`、`game/scenes/`、`game/shaders/` 和 `game/project.godot` 未作修改；当前触屏、HUD 和商店实现已更新共享脚本。首轮在这个 Godot 后端实测以下操作，期间没有收到 WebGPU `uncapturederror`：

- 右键移动英雄，以及 Ctrl + 单击的 3×3 地形编辑。
- 学习技能后按 W 并单击目标，确认地形隆起、碎石特效和 14 秒冷却显示。
- F1 后等级变为 2；Tab 切换英雄后 HUD 显示 560 HP。
- V 的恢复冷却显示为 60 秒；B 的回城进度显示。

该轮优化 Release 上再次验证了右键移动、F1 升至等级 2、学习技能及 W 点击施放，确认 14 秒冷却、地形隆起、碎石和震屏显示，并保存截图；也重新检查了 Tab 切换后的 560 HP、V 恢复的 60 秒冷却，以及 B 回城进度。本机前台运行曾观察到约 115–135 FPS；这是当时设备和场景的观察值，不是跨设备性能基准。

`0041` 的短时内存检查中没有再次出现此前快速增长至约 17.9 GB 的现象。Debug 的浏览器 GPU 进程私有内存在约 6 分钟采样窗口内为 **2,439–2,841 MiB**，后段样本为 2,612、2,628、2,599 MiB；窗口包含后台遮挡 / 降帧间隔，不能表述为连续 6 分钟前台运行。Release 在约 2 分钟正常帧率的检查中，样本为 **2,375–2,475 MiB**。这些短时样本不证明长时间对局的内存稳定性，也不代表全部特效或所有设备兼容性已经通过验证。此前独立客户端的 Chrome 验证不能作为这个 Godot 后端的验证结果。

长时间对局、浏览器和移动端覆盖仍需持续验证；未逐项核对的阴影、伤害数字、后处理和其他技能 / 特效也应分别检查。在其他测试设备上仍需核对实际驱动为 WebGPU、渲染方法为 Forward+。设备功能不足或渲染 API 不兼容时，需记录具体限制；不能仅因出现画面就宣称全部桌面特性已支持。

部署工作流为 [.github/workflows/deploy-webgpu.yml](../.github/workflows/deploy-webgpu.yml)。它由 `main` 分支推送或手动触发，在 Ubuntu 上安装匹配的 WebAssembly 工具链、运行逻辑测试，以 4 个并行任务构建原 Godot 引擎并发布宿主，核对 AppBundle 后上传并部署 GitHub Pages。仓库 Pages 的发布来源需要配置为 GitHub Actions。

部署地址配置为 [HeightFall 网页游戏](https://minsecrus.github.io/HeightFall/)，运行状态见 [GitHub Actions](https://github.com/Minsecrus/HeightFall/actions/workflows/deploy-webgpu.yml)。推送后应核对对应提交的构建和 deploy job 均成功，再检查页面是否加载该版本。本节记录的本机结果不代替 CI 和线上运行验证。
