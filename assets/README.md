# 素材文件说明

安装和构建时会从此文件夹读取以下文件：

| 文件 | 用途 | 更换方法 |
| --- | --- | --- |
| `avatar.jpg` | 动画前半段的头像 | 用 `tools/export-wallpaper-assets.html` 生成 |
| `artwork.jpg` | 后半段彩色画面；Windows 静态背景 | 与 `contours.js` 同时生成 |
| `contours.js` | 粒子汇聚的线稿坐标 | 与 `artwork.jpg` 同时生成 |
| `config.js` | 标题、小字和字幕的默认文字 | 用导出工具填写文字并下载 |
| `wallpaper.mp4`（可选） | Windows 动态背景 | 使用自己有权使用的视频，放入本地文件夹 |

公开版中的 `avatar.jpg` 和 `artwork.jpg` 来自同目录的原创示例 SVG。`wallpaper.mp4` 不随仓库发布，也被 `.gitignore` 忽略。完整操作见[更换素材指南](../docs/change-wallpaper.md)。
