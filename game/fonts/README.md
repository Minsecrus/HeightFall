# 中文 UI 字体

`HeightFallCjk-Regular.otf` 是 Noto Sans CJK SC Regular 2.004 的游戏字符子集，
以 SIL Open Font License 1.1 分发；完整许可证见 [OFL.txt](OFL.txt)。
字体原始版权为 © 2014–2021 Adobe。子集使用独立的 `HeightFall CJK` 家族名称。

官方来源：

- 仓库：[notofonts/noto-cjk](https://github.com/notofonts/noto-cjk)
- 固定提交：`f8d157532fbfaeda587e826d4cd5b21a49186f7c`
- 原字体：[Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf](https://github.com/notofonts/noto-cjk/blob/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf)
- 原许可证：[Sans/LICENSE](https://github.com/notofonts/noto-cjk/blob/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/LICENSE)
- 原字体 SHA-256：`2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`

子集包含 `game/scripts/**/*.cs` 和 `src/HeightFall.Sim/**/*.cs`
中的全部非 ASCII 字符，包括注释里的汉字、标点，以及实际绘制的
`垒`、`隼`、`普攻`、`取消`、装备名和 `World.Shop` 购买失败提示。
生成器还固定保留 `金币不足`、`装备栏已满`、`购买`、`出售`、`商店` 等基础商店文字。
ASCII 字符没有加入子集，继续由 Godot 原有主字体绘制。

`GameFonts.Install()` 在创建 UI 前将此字体追加到
`ThemeDB.FallbackFont.Fallbacks`，保留原主字体及已有 fallback 的顺序。
桌面和 Web 使用同一个资源，无需依赖浏览器系统中文字体。

新增中文文字后，从项目根目录运行以下命令重建子集。脚本使用 FontTools 4.63.0，
验证输入文件哈希、字符覆盖和 300 KB 的体积上限；关闭 hinting 以缩小资源，
保留字体版权与许可证元数据。执行后会打印最新字符数、大小和子集 SHA-256。

```powershell
# 将上面固定提交的官方原字体保存为此文件，仅作为本地构建缓存。
# .engine-cache/font-source/NotoSansCJKsc-Regular.otf
python -X utf8 game/fonts/subset-font.py
```

也可将官方原字体路径作为脚本的第一个参数传入。Godot 会导入 `.otf` 并将字体资源
纳入游戏导出；发布站点的许可证目录应同时包含 `OFL.txt`，以便用户直接查看。
