# Godot WebGPU 网页入口

当前网页使用自编 Godot WebGPU 后端运行原始场景、Godot HUD、C# 脚本和 Effects。构建、宿主发布、本机 HTTP 服务及验证状态统一见 [Godot WebGPU 网页构建](web-build.md)。

桌面游戏保持 .NET 8；网页宿主使用 .NET 10。含 `0041` 资源生命周期适配的原生构建、Debug 和优化 Release 发布均已成功；Chrome 的最新 Release 已显示原 3D 场景和 Godot HUD，并完成所列交互检查。短时采样没有重现此前快速增长的内存现象，长时间稳定性和设备覆盖仍需验证，具体范围见上述构建说明。

独立 Three.js / `HeightFall.Sim` 网页客户端已停止作为发布方案。新的宿主项目为 `game/HeightFall.WebGpu/`，部署产物为该目录的 `AppBundle/`；部署应使用原游戏的 Godot 宿主。

部署目标为 [HeightFall 网页游戏](https://minsecrus.github.io/HeightFall/)。推送后的构建和部署状态见 [GitHub Actions](https://github.com/Minsecrus/HeightFall/actions/workflows/deploy-webgpu.yml)；本机验证结果不代表 CI 或线上版本已经通过验证。

开源引擎整合归属见 [engine/NOTICE.md](../engine/NOTICE.md)。
