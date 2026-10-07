# Codex Startup Animation

给 Codex / ChatGPT 桌面应用添加一段可以更换头像、背景和文字的启动动画。动画大约 12 秒：头像出现、碎片散开、粒子汇成线稿、背景显现，最后收拢并露出官方应用。

这是基于 [panding999 的 macOS 启动动画项目](https://github.com/panding999/codex-startup-animation)继续开发的**非官方版本**，主要增加 Windows 官方入口适配、动态背景和换图工具，并调整了动画画面。它不包含官方应用，也不修改官方安装包。先看 [演示背景](assets/artwork.jpg)；下载源码后，双击 `index.html` 可以在浏览器里预览完整动画。

这个版本的主要变化：

- 将画面改为 16:9 横版，调整头像碎片、粒子汇聚和结尾收束，并让背景与线稿使用同一帧素材。
- 增加 Windows 监听程序，让桌面、任务栏等官方入口打开窗口时也能触发动画；动画后可保留静态或动态背景。
- 增加本机素材导出工具、按需修复菜单和停用脚本，方便更换壁纸和排查更新后的问题。

## 选择你的系统

| | Windows | macOS |
| --- | --- | --- |
| 从哪里启动 | 安装后仍点官方 App 图标 | 从本项目构建的启动器打开 |
| 动画结束后 | 可显示静态图；放入本地视频后可显示动态背景 | 可显示静态背景 |
| 更换素材 | 在本机生成素材，重新运行安装脚本 | 在预览页设置，或更换素材后重新构建 |
| 关闭页面再进入 | 设计为只播放后半段；普通窗口切换不重播 | 已运行的 App 会被切回前台 |

## Windows：开始使用

需要已安装 Microsoft Store 版 `OpenAI.Codex`、Windows PowerShell 5.1 和 Microsoft Edge WebView2 Runtime。安装脚本会检查官方应用和所需文件。

1. 下载并解压本项目，把文件夹放在固定位置。以后要更换素材或修复动画，安装脚本还会用到这个文件夹。
2. 双击 `index.html` 预览动画。预览不会安装或更改官方应用。
3. 在项目文件夹空白处打开 PowerShell，运行：

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\install-seamless-windows.ps1
   ```

4. 安装完成后，照常点击官方 App 图标。桌面会出现“启动动画修复”入口；仅在官方 App 或动画出问题时使用。菜单会分别提供 App 修复和动画修复。

安装脚本会把动画文件复制到当前用户的 `%LOCALAPPDATA%\OpenAI\ChatGPTFix\Animation`，注册登录后的动画监听，并备份它更新的旧动画文件。它不会替换官方 App，也不会覆盖已有的 `Repair-ChatGPT.ps1` 或“修复 ChatGPT”桌面入口；菜单优先使用电脑上已有的 App 修复脚本。重新运行安装脚本会更新已安装的动画素材。

**预览页和已安装动画的设置分开保存。** 在浏览器预览页点击“保存并预览”，不会改变 Windows 官方入口播放的内容。要让新图片、视频或文字在官方入口生效，请按[更换素材指南](docs/change-wallpaper.md)导出文件，放入 `assets` 文件夹，再运行一次安装脚本。

需要停用动画监听时，按[停用说明](docs/windows.md#停用动画)操作；官方 App 本身仍可正常打开。

## macOS：开始使用

需要 macOS 13 及以上版本、Xcode Command Line Tools，以及已安装的官方桌面应用。按 [macOS 使用指南](docs/macos.md)构建后，从 `Startup Animation.app` 启动。macOS 版使用单独入口；直接点击官方 App 图标不会播放本项目动画。

## 更换头像、壁纸和文字

打开[更换素材指南](docs/change-wallpaper.md)，按图文步骤使用本机导出工具生成：

- `avatar.jpg`：开场头像。
- `artwork.jpg` 与 `contours.js`：同一画面的彩色背景和粒子线稿。
- `config.js`：可选的标题、小字和字幕。
- `wallpaper.mp4`：可选的 Windows 动态背景视频，留在自己的电脑上即可。

公开版附带原创的抽象示例素材，源文件在 `assets/demo-*.svg`。不含 Wallpaper Engine 工坊壁纸、个人头像或视频。[素材文件说明](assets/README.md)

## 使用时要知道的限制

- Windows 动画和背景是盖在官方窗口上的独立透明窗口，不是官方页面的一部分。首次冷启动需要等官方窗口出现，可能短暂露出官方加载页。
- Windows 背景层以 28% 不透明度显示在页面上方，因此也会改变文字与控件的颜色；画面越亮，文字对比度越容易受影响。没有视频时使用静态背景。
- 官方 App 更新后通常可以继续使用；如果窗口或进程行为改变，可能需要更新本项目。Windows 的动画故障日志在 `%LOCALAPPDATA%\OpenAI\ChatGPTFix\Logs`。
- 公开版的示例素材已经完成浏览器预览和 Windows 源码编译检查，尚未在每个官方 App 版本上实际播放验证。macOS 构建需要在 macOS 上完成。

Windows 的安装、停用和常见问题见 [Windows 使用说明](docs/windows.md)。想了解实现方式和测试范围，可看 [技术说明](docs/how-it-works.md)。

## 许可与来源

此版本保留最初 macOS 项目的代码与来源信息。原项目目前未声明开源许可证，因此这份完整代码不能被重新标为 MIT；公开的 GitHub 仓库也不自动等于获得自由复制和再发布的许可。Windows 所需的 Microsoft WebView2 SDK 文件附有自己的许可和声明，见 [第三方说明](THIRD_PARTY_NOTICES.md)。本项目与 OpenAI 无隶属关系，不提供第三方壁纸或商标的再分发许可。项目来源及其他参考见[参考说明](docs/references.md)。
