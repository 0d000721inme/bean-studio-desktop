# Bean Studio · Windows 桌面版

用于 MuMu 等 Windows 游戏窗口的豆子与游戏时间识别工具。基于独立桌面 V5.7，C# / WinForms，自绘圆角界面和逐像素透明置顶浮窗，所有识别在本机进行。

## 功能与计时规则

- 自动寻找左右两排豆子，选择监测侧；支持蓝、金、紫色及暗豆轮廓。
- 初次读到豆数建立基准；确认减少，重置完整 15 秒冷却；计时中再次减少也重置 15 秒。
- 增长不触发计时。不确定画面、技能黑屏不会自动清除已有冷却。
- 浮窗提供左右快捷切换，冷却接近结束时显示紧急颜色。
- 游戏时间范围为 0–60 秒，支持两位整数与低于 10 秒的小数布局；使用轮廓模板、Tesseract LSTM 和连续帧确认。
- 少豆时固定显示“当时游戏时间 − 15 秒”的恢复点；中途补读则减去剩余冷却，并标为估算。
- 优先使用 Windows Graphics Capture 获取窗口画面，失败时回退无遮挡的屏幕兼容截图。

这不是新训练的通用 AI 模型。完全遮挡、错误裁剪、低分辨率和特效仍可能造成未确认；不承诺零误识别或 200 ms 实战延迟。

## 从源码构建

使用 64 位 Windows 10（1903 或更新版本）或 Windows 11，安装 .NET Framework 4.8 / 4.8.1，以及原生 OCR 所需的 Microsoft Visual C++ 2015–2022 x64 运行库。构建使用系统 .NET Framework 的 csc 编译器及 Windows WinRT 元数据，不需要 OBS、Node.js 或 Python。

在 Windows PowerShell 中运行：

```powershell
git clone https://github.com/0d000721inme/bean-studio-desktop.git
cd bean-studio-desktop
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

第一次构建会从官方 NuGet 下载 Tesseract 5.2.0，从 Tesseract 官方仓库下载固定版本的英文模型，核对 SHA-256 后恢复到本机；之后可复用 .cache 缓存。下载地址和期望哈希可在 scripts/fetch-dependencies.ps1 中检查。

构建后运行根目录 BeanStudio.exe。保持 Tesseract.dll、x64、tessdata 和 clock-templates.txt 与 EXE 同目录。这里发布源码，不上传原始游戏截图、个人诊断或第三方预编译库。

## 使用

1. 手动打开 MuMu 和游戏。本程序不自动启动模拟器。
2. 选择窗口 → 扫描画面 → 选择左侧或右侧 → 开始监测。
3. 游戏时间未确认时，点“校准”，框住完整中间数字，为低于 10 秒的小数留足空间，避开“第几回”。
4. “悬浮倒计时”打开二级菜单；显示浮窗后可以拖动、切换左右。

详细操作、捕获限制和历史 V5.7 检查范围见 [USAGE.md](USAGE.md)。普通外部窗口遮挡时 WGC 通常仍能捕获；最小化、源窗口停止渲染或画面内部遮挡仍可能失效。

## 检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

该脚本运行 12 项时钟连续状态与恢复点检查，不启动 MuMu，不使用用户截图。结果保存在 artifacts/tests。已在 PowerShell 7 和 Windows PowerShell 5.1 中实际构建并通过这 12 项检查。源码还保留历史图片和捕获测试入口，若要运行需要自行提供合适样本；历史样本测试不能代表独立实战准确率。

## 文件与隐私

正常监测不持续录屏、不上传画面、不持续写截图。最近失败裁图和日志有数量上限，保留在内存中；保存诊断、校准或发生启动错误时才写入程序目录。校准、诊断、依赖缓存和编译产物已加入 .gitignore。

## 目录

| 路径 | 内容 |
| --- | --- |
| src/ | 捕获、定位、识别、计时、界面与历史检查代码 |
| scripts/ | 官方依赖恢复与状态检查脚本 |
| clock-templates.txt | 标注数字的二值轮廓模板 |
| build.ps1 | x64 桌面程序构建 |
| THIRD_PARTY.md | 第三方依赖来源与许可 |

项目代码采用 [MIT](LICENSE)。下载的第三方依赖保持各自许可证。项目与 MuMu、游戏厂商及 OBS 无隶属关系。
