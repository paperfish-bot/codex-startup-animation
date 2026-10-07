# macOS 使用说明

需要 macOS 13 或更新版本、Xcode Command Line Tools，以及已安装的官方 Codex / ChatGPT 桌面应用。

## 构建

在项目根目录打开终端，依次运行：

```sh
bash tools/build-extension.sh
bash tools/build-macos.sh
```

构建完成后，`dist/Startup Animation.app` 是正式启动入口，`dist/Startup Animation Preview.app` 是独立预览。项目不附带已编译的 macOS 应用。

## 启动与设置

完全退出官方应用后，打开 `dist/Startup Animation.app`，或双击 `扩展启动Codex.command`。动画与官方页面并行加载。官方应用已经运行时，启动器会切回现有窗口；直接点官方图标不会播放本项目动画。

在动画或静态背景显示期间，按 **⌘⌥B** 打开图片与文字设置。设置存储在这台 Mac 上。换默认图片和文字也可以按[更换素材指南](change-wallpaper.md)操作，再重新构建启动器。`wallpaper.mp4` 目前只供 Windows 使用。

## 更新与限制

官方应用更新后通常可以继续使用，但窗口结构或调试接口改变时可能需要更新本项目。整页刷新、新窗口或官方应用自动重启也可能使临时动画层失效。此启动方式会在本机开启仅绑定回环地址的调试端口；完全退出官方应用后再从官方图标正常打开，可结束本次扩展启动的调试状态。
