# 第三方依赖与运行包通知

Bean Studio 自有源码采用 MIT 许可。下面的 OCR 库及模型仍遵守各自的许可；本项目 MIT 许可不会替代它们。源码仓库通过 `scripts/fetch-dependencies.ps1` 从官方固定来源恢复依赖，Windows 运行包随包保留 `licenses/`、`license-sources.json` 和 `runtime-components.json`。

| 组件 | 实际版本与来源 | 许可与随包通知 |
| --- | --- | --- |
| Tesseract .NET 包装库 | [官方 NuGet 5.2.0](https://www.nuget.org/packages/Tesseract/5.2.0)，[固定源码提交](https://github.com/charlesw/tesseract/tree/2c993543f7fa66576a8890a6c4ab053c4598aaed) | [Apache-2.0](licenses/Tesseract.NET-LICENSE.txt)；NuGet 元数据署名 Copyright 2012–2020 Charles Weld。[原始元数据](licenses/Tesseract-NuGet-metadata.xml)与[该源码版本 README](licenses/Tesseract.NET-README.md)均原样保留。 |
| Tesseract OCR 原生引擎 | **5.0.0**；对运行包 `tesseract50.dll` 调用 `TessVersion()` 确认；[对应官方源码](https://github.com/tesseract-ocr/tesseract/tree/5.0.0) | [Apache-2.0](licenses/Tesseract.OCR-LICENSE.txt)，另保留[作者名单](licenses/Tesseract.OCR-AUTHORS.txt)和[上游说明](licenses/Tesseract.OCR-README.md)。包装库版本 5.2.0 不等于此原生 DLL 的版本。 |
| 英文 LSTM 模型 | tessdata_fast 4.1.0；[固定提交](https://github.com/tesseract-ocr/tessdata_fast/tree/65727574dfcd264acbb0c3e07860e4e9e9b22185)的 `eng.traineddata` | [Apache-2.0](licenses/tessdata_fast-LICENSE.txt) |
| Leptonica | **1.82.0**；`getLeptonicaVersion()` 确认；[官方源码](https://github.com/DanBloomberg/leptonica/tree/1.82.0) | [BSD 风格完整许可](licenses/Leptonica-LICENSE.txt)，Copyright (C) 2001–2020 Leptonica。 |
| InteropDotNet | 嵌入 Tesseract .NET 5.2.0 包装库；[上游许可固定提交](https://github.com/AndreyAkinshin/InteropDotNet/tree/0b15eee809716c50562458d6fe95d52bea46b9c6) | [MIT](licenses/InteropDotNet-LICENSE.md)，Copyright (c) 2014 Andrey Akinshin。此提交用于记录许可来源，不声称嵌入代码的版本号等于该提交。 |
| giflib / libgif | **5.2.1**；Leptonica 的 `getImagelibVersions()` 确认；[官方项目](https://sourceforge.net/projects/giflib/) | [MIT / COPYING](licenses/giflib-COPYING.txt)，Copyright (c) 1997 Eric S. Raymond；另原样保留其 [OpenBSD 分配辅助代码的版权通知](licenses/giflib-openbsd-reallocarray-notice.txt)，Copyright (c) 2008 Otto Moerbeek，SPDX MIT。 |
| libjpeg-turbo | **2.1.4**（libjpeg API 6b）；同一版本接口确认；[官方源码](https://github.com/libjpeg-turbo/libjpeg-turbo/tree/2.1.4) | [完整许可说明与 BSD 条款](licenses/libjpeg-turbo-LICENSE.md)、[IJG 原始 README](licenses/libjpeg-turbo-README.ijg)。 |
| libpng | **1.6.37**；同一版本接口确认；[官方源码](https://github.com/pnggroup/libpng/tree/v1.6.37) | [PNG Reference Library 完整许可与历年版权通知](licenses/libpng-LICENSE.txt) |
| zlib | **1.2.13**；同一版本接口确认；[官方源码](https://github.com/madler/zlib/tree/v1.2.13) | [zlib 许可](licenses/zlib-LICENSE.txt)，Copyright (C) 1995–2022 Jean-loup Gailly and Mark Adler。 |
| libtiff | **4.4.0**；同一版本接口确认；[官方源码](https://gitlab.com/libtiff/libtiff/-/tree/v4.4.0) | [完整 COPYRIGHT](licenses/libtiff-COPYRIGHT.txt)，Copyright (c) 1988–1997 Sam Leffler；Copyright (c) 1991–1997 Silicon Graphics, Inc。 |

This software is based in part on the work of the Independent JPEG Group.

以上原生图像库作为官方 NuGet 的 Leptonica 原生 DLL 组成部分分发，没有另外改写其二进制。`license-sources.json` 记录每份通知的官方下载 URL、版本、相对路径、原始字节数及 SHA-256；`runtime-components.json` 记录实际分发 DLL / 模型的校验值，以及调用版本接口得到的结果。通知文件保留原始字节，不以改写后的摘要代替完整许可。

依赖恢复脚本使用的固定下载校验值：

```text
Tesseract 5.2.0 nupkg
202d82fc7c7d8384df7da57206d5e1f456ccdabd648c46e67cdfaa3a911d4795

eng.traineddata
7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2
```

Windows、.NET Framework 与 Microsoft Visual C++ Runtime 属于运行环境依赖，本运行 ZIP 不复制或分发它们的安装包。上游包装库的运行环境说明保留在 `licenses/Tesseract.NET-README.md`。

`clock-templates.txt` 是标注数字的二值轮廓数据，不是新训练的 AI 模型。本项目源码及运行 ZIP 不包含完整游戏截图、角色素材、用户诊断图片或个人窗口配置。当前桌面界面为自绘 WinForms 控件，没有 ReaLTaiizor、在线字体或第三方图片依赖。
