# Bean Studio · Windows 桌面版

[下载 V5.7.3 运行包](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.3) · [设计与实现说明](docs/DESIGN.md) · [更新记录](CHANGELOG.md) · [历史版本归档](HISTORY.md)

## V5.7.3 开始与结束音效

当前主分支为 V5.7.3，发布页保留此前已经构建的完整运行包。退出旧版后运行本目录的 BeanStudio.exe，保留 V5.7.2 的四空豆和重新定位修复。

确认少豆并重置 15 秒时播放开始音；该轮冷却自然结束时播放一次结束音。计时中再次少豆会重新播放开始音，结束音跟随新的截止时间。初次识别、增长、黑屏和未确认不会触发开始音；手动清除取消该轮待播放的结束音。暂停监测时原冷却仍继续，因此它到期后仍可播放结束音。

点主界面的“音效设置”，可分别开关两种提示、调整 0–100% 音量、更换文件、试听及恢复默认。拖动音量滑块立即改变正在试听的音量；保存后自动提示使用新音量，0% 静音，不修改游戏音量。支持选择 WAV、MP3、FLAC、M4A、AAC、MP4、M4V；FLAC 与 PCM WAV 可用作无损音效，MP4/M4V 只播放声音，不弹出视频窗口。实际能否播放还取决于文件内部编码与当前 Windows 解码能力，选择后请先试听。

更换文件保存的是本机路径，不复制或上传媒体；移动或删除原文件后需重新选择。自带两段原创合成 WAV，音效文件与使用说明见 [sounds/README.md](sounds/README.md)。本次上传保留原始运行 ZIP；其中旧 README 所述本地状态属于此前记录，最新发布和构建信息以本仓库为准。

## V5.7.2 四空豆修复

V5.7.2 的修复保留在本版中。

所选一侧连续确认 0 颗时，主计时与浮窗优先显示“对方已无豆”，并标明 0 / 4 颗。恢复豆子后还原数字计时，增长不触发。最后一豆消耗仍按原规则设置 15 秒截止时间；无豆提示不会改变该截止时间。

识别框持续失效约 0.45 秒后，自动模式会限频重新验证位置，解决同尺寸角色/HUD布局变化后一直沿用旧框的问题。仅采用四格全部有效的新位置；失败保留旧框和已有倒计时，黑屏、遮挡不猜成 0 颗。未修改颜色与轮廓判定门槛。

V5.7.2 的源码与原构建另见[版本发布页](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.2)。构建本包中的修复源码，在本目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`；已带依赖时可加 `-SkipDependencies`。

## V5.7.1 暂停修复

运行本目录的 BeanStudio.exe 前，先退出旧版计时器。旧版的“暂停监测”只暂停计时触发，后台窗口捕获与游戏时间识别仍在运行；本版暂停时关闭 WGC 会话，让豆子和时间识别线程进入等待，已有冷却继续倒计时。暂停中的左右切换、区域校准不会重新启动捕获；手动扫描只截取一次屏幕。恢复监测后重新确认豆数与游戏时间。

暂停还会降低主界面与浮窗的刷新频率。界面等捕获和在途识别全部结束后才显示“捕获与识别已停止”。这是针对暂停行为的修复，未据此保证实际 MuMu 运行时的鼠标卡顿已经完全解决。

修复 WGC 处理忙碌时直接丢弃新帧通知的问题：现在保留待处理通知，排空积压帧，只对最新帧做读回。本机合成窗口的同一检查中，修复前持续重绘 10 秒仅完成一帧，修复后首次与恢复均可完成连续帧采集。

V5.7.1 发布页面提供带依赖的运行包，解压后运行 BeanStudio.exe 即可。历史源码与原版构建包见 HISTORY.md；旧版只用于回溯，不含本版的暂停和帧池修复。

用于 MuMu 等 Windows 游戏窗口的豆子与游戏时间识别工具。V5.7.3 保留此前的暂停、帧池与四空豆修复，C# / WinForms，自绘圆角界面和逐像素透明置顶浮窗，所有识别在本机进行。

## 功能与计时规则

- 自动寻找左右两排豆子，选择监测侧；支持蓝、金、紫色及暗豆轮廓。
- 初次读到豆数建立基准；确认减少，重置完整 15 秒冷却；计时中再次减少也重置 15 秒。
- 增长不触发计时。不确定画面、技能黑屏不会自动清除已有冷却。
- 已确认四空豆显示“对方已无豆”；持续失效时自动重新验证位置。
- 少豆开始与冷却结束分别提示音；支持自定义音频及 MP4 声音、独立开关和音量。
- 浮窗提供左右快捷切换，冷却接近结束时显示紧急颜色。
- 游戏时间范围为 0–60 秒，支持两位整数与低于 10 秒的小数布局；使用轮廓模板、Tesseract LSTM 和连续帧确认。
- 少豆时固定显示“当时游戏时间 − 15 秒”的恢复点；中途补读则减去剩余冷却，并标为估算。
- 优先使用 Windows Graphics Capture 获取窗口画面，失败时回退无遮挡的屏幕兼容截图。

这不是新训练的通用 AI 模型。完全遮挡、错误裁剪、低分辨率和特效仍可能造成未确认；不承诺零误识别或 200 ms 实战延迟。

## 下一模型接手

从[下一模型构建与交接说明](docs/NEXT_MODEL_BUILD.md)开始，包含依赖、检查入口、版本差异、计时规则和未解决事项。原构建对应关系见[发布归档记录](docs/PUBLICATION.md)。

## 从源码构建

使用 64 位 Windows 10（1903 或更新版本）或 Windows 11，安装 .NET Framework 4.8 / 4.8.1，以及原生 OCR 所需的 Microsoft Visual C++ 2015–2022 x64 运行库。构建使用系统 .NET Framework 的 csc 编译器及 Windows WinRT 元数据，不需要 OBS、Node.js 或 Python。

在 Windows PowerShell 中运行：

```powershell
git clone https://github.com/0d000721inme/bean-studio-desktop.git
cd bean-studio-desktop
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

第一次构建会从官方 NuGet 下载 Tesseract 5.2.0，从 Tesseract 官方仓库下载固定版本的英文模型，核对 SHA-256 后恢复到本机；之后可复用 .cache 缓存。下载地址和期望哈希可在 scripts/fetch-dependencies.ps1 中检查。

构建后运行根目录 BeanStudio.exe。保持 Tesseract.dll、x64、tessdata、sounds 和 clock-templates.txt 与 EXE 同目录。仓库主分支保留源码与文档，运行包放在 Releases；不公开原始游戏截图、个人诊断和本地校准。

## 使用

1. 手动打开 MuMu 和游戏。本程序不自动启动模拟器。
2. 选择窗口 → 扫描画面 → 选择左侧或右侧 → 开始监测。
3. 游戏时间未确认时，点“校准”，框住完整中间数字，为低于 10 秒的小数留足空间，避开“第几回”。
4. “悬浮倒计时”打开二级菜单；显示浮窗后可以拖动、切换左右。
5. “音效设置”更换开始与结束音，分别试听后保存。确认 0 颗时仍显示“对方已无豆”，声音按底层冷却的开始和到期事件提示。

详细操作、捕获限制和历史 V5.7 检查范围见 [USAGE.md](USAGE.md)。普通外部窗口遮挡时 WGC 通常仍能捕获；最小化、源窗口停止渲染或画面内部遮挡仍可能失效。

## 检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

该脚本运行 12 项时钟连续状态与恢复点检查，不启动 MuMu，不使用用户截图。结果保存在 artifacts/tests。已在 PowerShell 7 和 Windows PowerShell 5.1 中实际构建并通过这 12 项检查。源码还保留历史图片和捕获测试入口，若要运行需要自行提供合适样本；历史样本测试不能代表独立实战准确率。

本地 V5.7.1 的编译产物已通过这 12 项检查，以及 20 项暂停、恢复和真实 WGC 合成窗口检查，详见验证结果.txt。可在普通 Windows 桌面会话中复现后者：

```powershell
$report = Join-Path $PWD 'artifacts\tests\pause'
Start-Process -FilePath .\BeanStudio.exe -ArgumentList @('--pause-test', ('"{0}"' -f $report)) -WindowStyle Hidden -Wait
Get-Content (Join-Path $report '暂停回归验证.txt') -Encoding UTF8
```

该检查只创建和捕获自己的动态测试窗口，不操作 MuMu；短暂显示合成测试窗。它验证帧采集和暂停生命周期，不测量真实游戏鼠标延迟。

## 文件与隐私

正常监测不持续录屏、不上传画面、不持续写截图。最近失败裁图和日志有数量上限，保留在内存中；保存诊断、校准或发生启动错误时才写入程序目录。音效设置保存在 `%LOCALAPPDATA%\BeanStudio\sound-settings.xml`，保存开关、音量与自定义文件路径，不复制媒体。校准、诊断、依赖缓存和编译产物已加入 .gitignore。

## 目录

| 路径 | 内容 |
| --- | --- |
| src/ | 捕获、定位、识别、计时、界面与历史检查代码 |
| sounds/ | 默认原创 WAV 音效与更换说明 |
| scripts/ | 官方依赖恢复与状态检查脚本 |
| clock-templates.txt | 标注数字的二值轮廓模板 |
| build.ps1 | x64 桌面程序构建 |
| THIRD_PARTY.md | 第三方依赖来源与许可 |
| docs/NEXT_MODEL_BUILD.md | 下一模型构建与交接入口 |
| docs/DESIGN.md | 检测、计时、捕获、线程与性能的设计依据和取舍 |
| CHANGELOG.md / HISTORY.md | 版本变化、历史包下载与已知缺陷 |
| licenses/ | 运行包第三方组件的完整许可与来源 |

项目代码采用 [MIT](LICENSE)。下载的第三方依赖保持各自许可证。项目与 MuMu、游戏厂商及 OBS 无隶属关系。
