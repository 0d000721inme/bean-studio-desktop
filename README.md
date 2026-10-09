# Bean Studio · Windows 桌面版

[下载 V5.7.2 运行包](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.2) · [设计与实现说明](docs/DESIGN.md) · [更新记录](CHANGELOG.md) · [历史版本归档](HISTORY.md)

## V5.7.2 四空豆修复

本目录为 V5.7.2 发布归档，运行包沿用原构建，没有重新编译。退出旧版后运行本目录的 BeanStudio.exe。

所选一侧连续确认 0 颗时，主计时与浮窗优先显示“对方已无豆”，并标明 0 / 4 颗。恢复豆子后还原数字计时，增长不触发。最后一豆消耗仍按原规则设置 15 秒截止时间；无豆提示不会改变该截止时间。

识别框持续失效约 0.45 秒后，自动模式会限频重新验证位置，解决同尺寸角色/HUD布局变化后一直沿用旧框的问题。仅采用四格全部有效的新位置；失败保留旧框和已有倒计时，黑屏、遮挡不猜成 0 颗。未修改颜色与轮廓判定门槛。

V5.7.2 的源码与原构建另见[版本发布页](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.2)。构建本包中的修复源码，在本目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`；已带依赖时可加 `-SkipDependencies`。

## V5.7.1 暂停修复

运行本目录的 BeanStudio.exe 前，先退出旧版计时器。旧版的“暂停监测”只暂停计时触发，后台窗口捕获与游戏时间识别仍在运行；本版暂停时关闭 WGC 会话，让豆子和时间识别线程进入等待，已有冷却继续倒计时。暂停中的左右切换、区域校准不会重新启动捕获；手动扫描只截取一次屏幕。恢复监测后重新确认豆数与游戏时间。

暂停还会降低主界面与浮窗的刷新频率。界面等捕获和在途识别全部结束后才显示“捕获与识别已停止”。这是针对暂停行为的修复，未据此保证实际 MuMu 运行时的鼠标卡顿已经完全解决。

修复 WGC 处理忙碌时直接丢弃新帧通知的问题：现在保留待处理通知，排空积压帧，只对最新帧做读回。本机合成窗口的同一检查中，修复前持续重绘 10 秒仅完成一帧，修复后首次与恢复均可完成连续帧采集。

V5.7.1 发布页面提供带依赖的运行包，解压后运行 BeanStudio.exe 即可。历史源码与原版构建包见 HISTORY.md；旧版只用于回溯，不含本版的暂停和帧池修复。

用于 MuMu 等 Windows 游戏窗口的豆子与游戏时间识别工具。V5.7.2 保留 V5.7.1 的暂停和帧池修复，C# / WinForms，自绘圆角界面和逐像素透明置顶浮窗，所有识别在本机进行。

## 功能与计时规则

- 自动寻找左右两排豆子，选择监测侧；支持蓝、金、紫色及暗豆轮廓。
- 初次读到豆数建立基准；确认减少，重置完整 15 秒冷却；计时中再次减少也重置 15 秒。
- 增长不触发计时。不确定画面、技能黑屏不会自动清除已有冷却。
- 已确认四空豆显示“对方已无豆”；持续失效时自动重新验证位置。
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

构建后运行根目录 BeanStudio.exe。保持 Tesseract.dll、x64、tessdata 和 clock-templates.txt 与 EXE 同目录。仓库主分支保留源码与文档，运行包放在 Releases；不公开原始游戏截图、个人诊断和本地校准。

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

本地 V5.7.1 的编译产物已通过这 12 项检查，以及 20 项暂停、恢复和真实 WGC 合成窗口检查，详见验证结果.txt。可在普通 Windows 桌面会话中复现后者：

```powershell
$report = Join-Path $PWD 'artifacts\tests\pause'
Start-Process -FilePath .\BeanStudio.exe -ArgumentList @('--pause-test', ('"{0}"' -f $report)) -WindowStyle Hidden -Wait
Get-Content (Join-Path $report '暂停回归验证.txt') -Encoding UTF8
```

该检查只创建和捕获自己的动态测试窗口，不操作 MuMu；短暂显示合成测试窗。它验证帧采集和暂停生命周期，不测量真实游戏鼠标延迟。

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
| docs/DESIGN.md | 检测、计时、捕获、线程与性能的设计依据和取舍 |
| CHANGELOG.md / HISTORY.md | 版本变化、历史包下载与已知缺陷 |
| licenses/ | 运行包第三方组件的完整许可与来源 |

项目代码采用 [MIT](LICENSE)。下载的第三方依赖保持各自许可证。项目与 MuMu、游戏厂商及 OBS 无隶属关系。
