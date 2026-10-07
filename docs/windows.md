# Windows 使用说明

## 安装

需要 Microsoft Store 版 Codex / ChatGPT 桌面应用、Windows PowerShell 5.1 和 Microsoft Edge WebView2 Runtime。项目文件夹可以放在任意固定位置；安装后请保留，因为更换素材和修复时会再次读取它。

在项目根目录打开 PowerShell，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\install-seamless-windows.ps1
```

脚本会复制动画文件、编译 Windows 动画监听程序、注册当前用户登录启动项与计划任务，并创建桌面“启动动画修复”快捷方式。它会备份此前安装的动画文件；不会修改官方 App 安装包或覆盖已有的“修复 ChatGPT”入口。新菜单分别提供官方 App 修复、动画修复与打开官方 App；只有你选了修复项，才会运行相应修复脚本。安装后继续直接点官方 App 图标。

若只想查看安装状态，不更改电脑，可运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\install-seamless-windows.ps1 -CheckOnly
```

## 播放方式

监听程序随用户登录启动，在官方 App 主窗口打开时显示动画。设计目标是冷启动播放完整片头，关闭页面后重新进入时只播放后半段；单纯切换窗口或最小化后恢复不重播。动画结束时，背景层继续跟随官方窗口。上传文件时出现的文件选择窗口不应触发片头。

如果 `assets/wallpaper.mp4` 存在，背景为静音循环视频；否则显示 `assets/artwork.jpg`。二者都会以半透明窗口盖在官方页面上，因此可能改变文字颜色。画面效果和网页内真正的背景不同。

## 更新和修复

更换素材或下载项目更新后，重新运行安装命令。动画没有播放时，打开桌面“启动动画修复”选择动画检查；若官方 App 自身无法打开，可在同一菜单选择 App 修复。菜单优先使用电脑上已安装的原修复脚本，若没有才使用项目附带的版本。原有“修复 ChatGPT”快捷方式仍保持原样。日志位置：`%LOCALAPPDATA%\OpenAI\ChatGPTFix\Logs`。

## 停用动画

在项目根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\disable-seamless-windows.ps1
```

该脚本只停用本项目注册的动画监听，并停止对应的后台进程。已安装素材和备份会保留；官方 App 和原有修复程序仍可使用。想重新启用时，再运行安装脚本。

## 已知限制

- 动画需要等官方窗口被发现。某些冷启动过程中，可能短暂看到官方加载页。
- 透明背景层会同时覆盖图片和文字；默认不透明度为 28%。太亮或太花的图片会降低文字可读性。
- 如果官方 App 或 Windows 更新改变窗口与进程行为，监听可能需要适配。
- 本项目在 Windows 上进行了源码编译、脚本检查和模拟窗口验证；这些检查不能保证每个官方版本和屏幕配置都完全一致。
