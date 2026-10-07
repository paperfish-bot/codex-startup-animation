# 工作方式与验证范围

## 动画素材

浏览器动画由 `index.html`、`style.css` 和 `animation.js` 绘制。`assets/avatar.jpg` 是开场头像；`assets/artwork.jpg` 是彩色背景；`assets/contours.js` 给出匹配这张背景的线稿路径。先显示头像，再让碎片散开并沿路径聚拢，最后显露彩色画面。`assets/config.js` 提供默认文字。

## Windows

安装脚本复制动画文件、编译 `SeamlessAgent`，并为当前用户注册启动入口。监听程序只寻找官方 App 主窗口；播放时显示独立的 WebView2 动画窗口。结束后，由可点击穿透的背景窗口显示静态图或本地视频。官方 App 的安装文件不被修改。

背景窗口位于官方页面上方，所以图片和文字会同时受到叠加影响，无法等同于页面内部真正的背景。窗口首次出现前也可能有短暂可见的官方加载画面。使用模拟窗口验证过冷启动、页面重进与短暂切换的判定；真实官方 App 的所有版本和屏幕配置尚未逐一验证。

## macOS

单独的启动器打开官方 App，在经过目标校验的本地会话中临时显示动画与静态背景。它使用本机回环调试端口，不改官方应用包。用户需要从这个启动器进入；官方图标自身不会自动播放。

## 本地检查

项目包含 `tools/extension.test.mjs`，可用 Node.js 运行：

```sh
node --test tools/extension.test.mjs
```

Windows 源码可用系统 C# 编译器和仓库附带的 WebView2 SDK 文件编译。浏览器导出工具在本机处理图片；它不向网络上传素材。macOS 构建及实际播放需要在 macOS 上验证。

## 项目来源

此版本基于 [panding999/codex-startup-animation](https://github.com/panding999/codex-startup-animation) 的 macOS 动画继续开发，并加入 Windows 监听、背景层与素材导出流程。参考项目和实现范围见[参考说明](references.md)。
