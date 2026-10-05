# 第三方依赖

本仓库发布项目源码，不直接分发预编译 DLL 或 OCR 模型。scripts/fetch-dependencies.ps1 从官方来源下载以下固定版本，校验 SHA-256 并恢复必要文件到本机；它们不受项目 MIT 许可证替代。

| 依赖 | 版本 / 来源 | 许可 |
| --- | --- | --- |
| Tesseract .NET | 5.2.0，[官方 NuGet](https://www.nuget.org/packages/Tesseract/5.2.0)，[源码](https://github.com/charlesw/tesseract) | Apache-2.0；Copyright 2012–2020 Charles Weld |
| Tesseract OCR | 包含于上述官方包，[源码](https://github.com/tesseract-ocr/tesseract) | Apache-2.0 |
| 英文 LSTM 模型 | tessdata_fast 4.1.0，[固定提交](https://github.com/tesseract-ocr/tessdata_fast/tree/65727574dfcd264acbb0c3e07860e4e9e9b22185) | Apache-2.0 |
| Leptonica | 1.82.0，包含于官方 NuGet，[源码](https://github.com/DanBloomberg/leptonica) | BSD 风格许可 |
| InteropDotNet | 包装库上游使用，[上游依赖声明](https://github.com/charlesw/tesseract#dependencies) | MIT；Copyright 2014 Andrey Akinshin |

本仓库保留 Tesseract-LICENSE.txt、tessdata-LICENSE.txt、Leptonica-LICENSE.txt 供查阅。Tesseract 原生图像库还包含 libjpeg-turbo 2.1.4、libpng 1.6.37、zlib 1.2.13 和 libtiff 4.4.0；如果另外打包、分发下载得到的原生二进制，应同时保留这些组件以及 InteropDotNet 的完整许可和版权通知，不能只附本项目 MIT 许可。

官方下载校验值：

```text
Tesseract 5.2.0 nupkg
202d82fc7c7d8384df7da57206d5e1f456ccdabd648c46e67cdfaa3a911d4795

eng.traineddata
7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2
```

clock-templates.txt 是标注数字的二值轮廓数据，不是新训练的 AI 模型。仓库不包含完整游戏截图、角色素材或用户诊断图片。V5.7 界面是自绘 WinForms 控件，没有 ReaLTaiizor、在线字体或第三方图片依赖。
