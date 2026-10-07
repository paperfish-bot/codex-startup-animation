# 参考与素材说明

此版本基于 [panding999/codex-startup-animation](https://github.com/panding999/codex-startup-animation) 的 macOS 项目开发，保留了原有的动画和 macOS 接入代码，并加入 Windows 适配、素材更换工具及视觉调整。以下项目只是实现时的参考，没有复制其源代码，也不需要安装或运行它们。

| 参考项目 | 参考范围 |
| --- | --- |
| [Tangc/codex-skin-launcher](https://github.com/Tangc/codex-skin-launcher) | macOS 原生窗口、应用标识与独立启动入口 |
| [Fei-Away/Codex-Dream-Skin](https://github.com/Fei-Away/Codex-Dream-Skin) | 整窗壁纸、透明表面、本机 CDP 接入及目标进程校验 |
| [NativeDog1/dsh-boot-animation](https://github.com/NativeDog1/dsh-boot-animation) | 可跳过的覆盖式片头、完成后移除、素材就绪后播放和超时兜底 |

DSH 使用自己的覆盖层接口，不能直接安装到 Codex。本项目的动画和外部接入独立实现，不引入视频片库。Windows 版使用 Microsoft WebView2，并有当前用户登录期间运行的轻量进程监听程序。

## 视觉素材

- 公开版默认头像和背景由本项目原创 SVG 生成，源文件分别为 `assets/demo-avatar.svg` 和 `assets/demo-artwork.svg`。
- `assets/contours.js` 从示例背景生成。自定义背景在导入时由本机 Canvas 生成轮廓。
- 公开版不包含基于官方标志修改的个人启动图标，也不包含个人 Wallpaper Engine 壁纸和头像。
- 早期原型使用过其他参考画面；公开版示例素材与那些画面无关。
- 动画本身由 HTML、CSS、Canvas 和 SVG 绘制，无音轨、远程字体、CDN 或在线图片请求。

动画从头像碎片过渡到背景的散线，再从左到右汇聚，最后显现彩色图。原项目目前没有声明开源许可证；此版本也不对原作者代码、第三方素材、品牌或商标授予额外权利。
