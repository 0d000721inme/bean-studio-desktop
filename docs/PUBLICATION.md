# PC 发布归档记录

整理日期：2026-10-09。当前归档 V5.7.2。本次只上传之前保存的源码及原构建、补充交接资料，未重新编译或运行测试。

发布页的完整 ZIP 保持原字节，其中旧 README 的“本地/尚未上传”等表述属于当时记录；源码 ZIP 与仓库 README 已整理为本次发布状态。两个 ZIP 与内部 EXE 的 SHA-256 见发布页 SHA256SUMS.txt；原包、EXE 与规范化 C# 源码对应关系见 [publication-manifest.json](publication-manifest.json)。

运行包包含 EXE、Tesseract.dll、x64 原生 DLL、tessdata 英文模型、clock-templates.txt 和完整许可；V5.7.3 另有原创 sounds/start.wav、end.wav。OCR 组件的四项哈希与 V5.7.1 已核对固定运行库一致，许可来源保持原记录。

本次上传排除 artifacts 测试图片、用户截图、诊断、校准、自定义媒体与凭据。源码可按 build.ps1 重新构建。既有检查记录见 [验证结果.txt](../验证结果.txt)，不代表多局实战识别率或实际 MuMu 鼠标延迟。PC 代码维持原 MIT，第三方组件保持各自许可。
