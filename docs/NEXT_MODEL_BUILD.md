# 下一模型交接与构建说明 · Windows PC 版

整理日期：2026-10-09。本文件用于把已有 PC 工程交给下一位维护者或模型复现。本次按用户追加要求上传已有 V5.7.2/V5.7.3 源码与原运行包，并把主分支更新为 V5.7.3；没有重新编译、运行测试或继续优化软件。发布的 EXE 是此前构建的原文件，不能把这次归档说成重新构建验证。

## 1. 先分清源码与版本

仓库：<https://github.com/0d000721inme/bean-studio-desktop>。本次交付的主分支应用源码是 **V5.7.3**：包含 V5.7.1 暂停/WGC 修复、V5.7.2 四空豆/重新定位修复和 V5.7.3 音效功能。历史基准提交 `c75e0b83bebb83af2e0792971ef98b499af35063` 对应原来的 V5.7.1，不是 V5.7.3。

| 内容 | 版本与内容 | 下一模型如何使用 |
| --- | --- | --- |
| 主分支 `src/`、`build.ps1`、`sounds/` | V5.7.3 完整源码与默认音效 | 克隆并按下文构建；记录克隆时实际提交号 |
| [V5.7.3 Release](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.3) | 本次归档已有源码包与原运行包 | 使用该版本资产；不与旧包混放 |
| [V5.7.2 Release](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.2) | 四空豆提示与持续失效重新定位，不含 V5.7.3 音效 | 需要这版时取相应源码/运行包 |
| [V5.7.1 Release](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/v5.7.1) | 暂停屏障与 WGC 新帧通知修复 | 历史基准，保留原包 |
| [历史 Release](https://github.com/0d000721inme/bean-studio-desktop/releases/tag/desktop-history-2026-10-06) | V1–V5.7 历史源码及原版 EXE/补丁归档 | 只用于回溯，不代表当前修复版 |

用户已要求结束这一阶段、不继续优化 APK，随后明确追加“本地新版也上传”。本次范围因此是已有版本归档、主分支同步及交接文档；不包含新修复或新编译。后续模型应先确认用户的新任务和指定版本，再做实现、构建或发布。

## 2. 最小阅读顺序

1. 本文件：确认版本、环境、可执行命令和未验证事项。
2. [README.md](../README.md)、[USAGE.md](../USAGE.md)：功能、首次操作和捕获限制。
3. [docs/DESIGN.md](DESIGN.md)：完整识别链路、状态机、线程、取舍与后续研究方向。
4. `build.ps1`、`scripts/fetch-dependencies.ps1`、`scripts/test.ps1`：构建与依赖的实际来源。
5. [验证结果.txt](../验证结果.txt)：当时已经执行的验证记录；不要把它当作当前机器刚运行的结果。
6. [CHANGELOG.md](../CHANGELOG.md)、[HISTORY.md](../HISTORY.md)：历史缺陷与版本演进。

通常无需通读所有历史测试或下载全部历史版本。按正在调查的捕获、豆子、时钟、计时或 UI 问题选取对应文件。

## 3. 构建环境

- 64 位 Windows 10 1903 或更新版本，或 Windows 11。
- .NET Framework 4.8 / 4.8.1；脚本实际调用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。
- Windows 自带 `System32\WinMetadata\*.winmd` 和对应 GAC WinRT 桥接程序集。
- Microsoft Visual C++ 2015–2022 **x64** 运行库，供原生 Tesseract / Leptonica 加载。
- Windows PowerShell 5.1 可直接执行；已有记录也验证过 PowerShell 7。Git 仅用于取仓库。
- 首次依赖恢复需要访问官方 NuGet 与 Tesseract 仓库；缓存完整后可离线恢复。

本项目是平铺 C# / WinForms 工程，不依赖 `.sln`、`.csproj` 或 `dotnet build`，不需要 Visual Studio、OBS、Node.js 或 Python。系统 Framework 编译器并不等于最新 C# SDK；增加源码时应兼容实际编译器，不直接引入新版语言语法。

## 4. 从当前主分支构建

在 Windows PowerShell 的工作目录执行以下命令。它们是供下一模型或维护者使用的复现步骤，本次整理没有执行：

```powershell
git clone https://github.com/0d000721inme/bean-studio-desktop.git
Set-Location -LiteralPath .\bean-studio-desktop
git rev-parse HEAD
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` 先恢复依赖，再收集 `src/` 根目录全部 `*.cs`，按名称排序，使用 `/main:MuMuBeans.Program /target:winexe /optimize+ /platform:x64` 编译到根目录 `BeanStudio.exe`。其他源码中的 `Main` 是历史测试入口，不能随意改掉指定应用入口。

若本目录四项 OCR 依赖已完整恢复，可跳过下载：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipDependencies
```

V5.7.2 与 V5.7.3 使用各自对应的源码提交和发布标签，未共用最新提交。需要固定复现版本时，可检出相应标签或取 Release 附带源码包，并核对发布清单；`c75e0b83bebb83af2e0792971ef98b499af35063` 只用于回溯 V5.7.1。主分支可能随后变化，应记录克隆时实际提交，不编造 V5.7.3 提交号。打包前记录源码提交、EXE 的 SHA-256、依赖清单和实际运行的检查；同源码重新编译不保证生成字节完全相同的 EXE。

### 官方依赖与离线缓存

`scripts/fetch-dependencies.ps1` 固定下载并核对 SHA-256：

- NuGet `Tesseract 5.2.0`；包内 `lib/net48/Tesseract.dll`、`x64/tesseract50.dll` 和 `x64/leptonica-1.82.0.dll`。
- 官方 `tessdata_fast` 4.1.0 标签对应固定提交的 `eng.traineddata`。
- 默认缓存目录为仓库根目录 `.cache/`；下载中使用 `.download`，成功核对后才替换缓存文件。

包版本 5.2.0 与原生 DLL 导出的 Tesseract 5.0.0 是不同层级的版本号，不要仅因二者不同就认定装错。详细文件哈希和原生组件信息见 [runtime-components.json](../runtime-components.json)。

完整缓存可这样离线恢复，然后构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\fetch-dependencies.ps1 -Offline
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipDependencies
```

`-Offline` 不是绕过校验；缓存缺失或哈希不符会失败。可使用脚本的 `-CacheDirectory` 指定已准备好的依赖缓存。

## 5. 运行包必须保留的文件

V5.7.3 编译后的 EXE 不能孤立移动。至少保持以下布局：

```text
BeanStudio.exe
Tesseract.dll
clock-templates.txt
x64/
  tesseract50.dll
  leptonica-1.82.0.dll
tessdata/
  eng.traineddata
sounds/
  start.wav
  end.wav
  README.md
LICENSE
THIRD_PARTY.md
LICENSE_SOURCES.md
licenses/
```

README、USAGE、版本记录和验证记录建议一起保留。根目录 `.gitignore` 排除 EXE、DLL、下载缓存、校准、错误日志与诊断图片，所以新克隆的仓库没有这些运行依赖是正常的。

当前主分支和 V5.7.3 源码包都有 `src/SoundAlerts.cs`、`SoundPanel.cs`、`SoundTests.cs` 以及 `sounds/`。默认两段 PCM WAV 是项目原创合成音效，按本项目 MIT 提供，不能漏掉。自定义 WAV、MP3、FLAC、M4A、AAC、MP4、M4V 由 Windows `MediaPlayer` 读取，视频只播放音轨，不需要额外 FFmpeg；扩展名不保证所有内部编码都支持。V5.7.2 不含这组音效代码，不能只拷贝新版 EXE 或混合两个版本的源码/资产。

## 6. 检查入口及已有证据

### 当前主分支：12 项时钟状态检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

已有 EXE 时：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -SkipBuild
```

脚本实际运行 `BeanStudio.exe --clock57-test --tracker <报告路径>`，检查进程退出码、12 条 `PASS`、无 `FAIL` 且 `FAILURES=0`。报告在 `artifacts/tests/clock57-tracker.txt`。这组检查测试连续确认、跳变拒绝、时效和恢复点，不衡量真实截图识别准确率，不启动 MuMu。

### 当前主分支：暂停与真实 WGC 合成窗口检查

在正常、可交互的 Windows 桌面会话中执行；测试会短暂显示自己的动态窗口，不能用无桌面的服务环境代替：

```powershell
$pauseReport = Join-Path $PWD 'artifacts\tests\pause'
$pauseProcess = Start-Process -FilePath .\BeanStudio.exe -WorkingDirectory $PWD `
    -ArgumentList @('--pause-test', ('"{0}"' -f $pauseReport)) `
    -WindowStyle Hidden -Wait -PassThru
$pauseProcess.Refresh()
Get-Content -LiteralPath (Join-Path $pauseReport '暂停回归验证.txt') -Encoding UTF8
if ($pauseProcess.ExitCode -ne 0) { throw '暂停检查失败，请查看报告。' }
```

留存的 2026-10-05 V5.7.1 本地记录为 **20 项暂停/WGC 检查通过 + 12 项时钟状态检查通过**。前者包含实际 GPU 读回、非空位图、暂停时采样计数不增长、恢复、新帧通知竞态、按钮及已有计时保持；源是自建窗口，不是游戏。

`.github/workflows/windows.yml` 只有手动 `workflow_dispatch`，在 Windows runner 上构建并运行 12 项时钟检查。它没有自动验证真实 MuMu、全部历史图片或完整暂停合成窗口链路，也不能替代发布包验证。

### V5.7.3 留存检查与专项入口

V5.7.3 的 2026-10-06 留存记录包含音效事件/配置 33、实际媒体后端 36、生产计时到播放器 12、音效交互 10、音效尺寸 4、四空豆 82、暂停/WGC 20、主界面绘制/交互 8、游戏时钟状态 12 等专项通过，详见根目录 `验证结果.txt`。它们是此前该版本的结果，未在本次上传重跑。媒体检查使用 0% 音量，确实检查后端打开、时间推进与完成事件，没有人工听感或声卡端到端延迟测量。

当前 V5.7.3 的 `BeanTimer.cs` 提供以下专项入口；`<...>` 是需要替换的参数，不是仓库已附样本：

| 参数序列（在 `BeanStudio.exe` 后） | 参数与报告 |
| --- | --- |
| `--sound-test <结果目录>` | 音效事件/配置；写 `音效事件与配置验证.txt` |
| `--sound-engine-test <结果目录>` | 真实 Counter/Engine/后台音效到播放器，需等待实际 15 秒；写 `音效实际计时链路验证.txt` |
| `--sound-media-test <媒体fixture目录> <结果目录>` | 需 `sample.wav/mp3/flac/m4a/mp4`、损坏 `corrupt.mp3` 和无音轨 `silent.mp4`；写 `音效格式播放验证.txt`，另有 `媒体状态记录.txt` |
| `--sound-ui-test <结果目录>` | 音效面板交互；写 `音效交互验证.txt` |
| `--empty-bean-test <四空豆样本.png> <结果目录>` | 需要四空豆/另一侧亮豆图片；写 `四空豆专项验证.txt` |
| `--bean-relocation-test <旧布局空豆.png> <新布局空豆.png> <结果目录>` | 两图尺寸相同、左侧四空豆位置不同；创建自有动态窗验证；写 `豆子自动重定位验证.txt` |

例如，仅运行不需要原始游戏图片的音效事件检查：

```powershell
$soundReport = Join-Path $PWD 'artifacts\tests\sound'
$soundProcess = Start-Process -FilePath .\BeanStudio.exe -WorkingDirectory $PWD `
    -ArgumentList @('--sound-test', ('"{0}"' -f $soundReport)) `
    -WindowStyle Hidden -Wait -PassThru
$soundProcess.Refresh()
Get-Content -LiteralPath (Join-Path $soundReport '音效事件与配置验证.txt') -Encoding UTF8
if ($soundProcess.ExitCode -ne 0) { throw '音效事件检查失败，请查看报告。' }
```

`--sound-ui-test` 直接调用 `SoundPanel.Verify`，失败会抛异常，成功报告是逐项 PASS，没有统一的 `FAILURES=0` 汇总。应检查进程状态、报告实际生成及全部预期项，不把缺失报告当成通过。其它带汇总的专项应核对报告中 `FAILURES=0` 和预期结果。部分检查需要另行准备合法图片或媒体 fixture；未公开的用户样本不能臆造为仓库现有文件。不要把这些 V5.7.3 参数直接发给 V5.7.1 EXE。

历史样本可能参与过模板制作，合成图、状态测试、UI 检查和媒体解码测试都不等于多局实战准确率。不得把不同版本的通过数量相加成“100% 准确”。

## 7. 文件地图与关键实现

| 文件 | 首要阅读内容 |
| --- | --- |
| `src/BeanTimer.cs` | `Program` 应用入口；`Detector` 四格识别；`Counter` 单调时钟计时；`Engine` 后台豆数线程和快照；主界面接线 |
| `src/AutoLocator.cs` | 左右四格定位、血条辅助、等距菱形候选、归一化区域 |
| `src/DarkGemShape.cs` | 暗豆轮廓与颜色有效性；区分空豆和未知 |
| `src/WindowCapture.cs` | 捕获协调器、暂停屏障、会话代际、兼容屏幕截图 |
| `src/WindowFrames.cs` | WGC、D3D11 帧池、最新帧排空、GPU→CPU 读回、缓冲释放 |
| `src/GameClock.cs` | 数字组件和完整布局、24×40 二值模板、Tesseract 备用路径 |
| `src/ClockWorker.cs` | `ClockTracker`、独立低优先级 OCR 线程、时效、校准与诊断 |
| `src/GlassUI.cs`、`Overlay.cs`、`ClockPanel.cs` | 自绘主 UI、216×112 逐像素透明浮窗、时间区域校准 |
| `src/Clock57Tests.cs`、`PauseTests.cs` | 当前可直接复现的状态检查和合成窗口生命周期检查 |
| `src/EmptyBeanTests.cs`、`BeanRelocationTests.cs` | 四空豆和同尺寸 HUD 布局变化后的自动重定位检查 |
| `src/SoundAlerts.cs`、`SoundPanel.cs`、`SoundTests.cs` | 权威截止时间的开始/结束事件、Windows MediaPlayer、设置持久化、媒体/交互检查 |
| `sounds/` | 随程序原始的两段原创 PCM WAV 及格式/更换说明 |
| `clock-templates.txt` | 人工标注数字的二值轮廓；不是新训练的通用 AI 模型 |

WinForms 自绘 UI 与悬浮窗不是 Web 页面，也不是系统原生 Liquid Glass。浮窗有左右按钮、拖动和双击返回，不能为了视觉效果直接把整窗设成鼠标穿透。

## 8. 必须保持的产品规则

- 第一次确认豆数只建立基准。确认减少后设置完整 **15.000 秒**截止时间；冷却中再次减少同样重置完整 15 秒。
- 增长只更新基准，不触发或重置。连续同一数量不能反复触发。
- 连续样本确认与时间门槛共同作用：至少三个有效样本；建立基准/减少至少 40 ms，增长至少 120 ms。短暂假增长再回落会造成倒计时跳回 14 秒，应调查误识别而不是改 UI 减秒。
- 黑屏、遮挡、裁切或未知帧不清除已有冷却。连续未知约 0.7 秒忘记豆数基准，恢复后重新建立，不补算未知期间消耗。
- `Counter` 使用 `Stopwatch`，显示 `max(0, deadline - now)`；不可用 UI 定时器每次减固定值替代权威截止时间。
- 清晰四空豆是有效 0，未知不是 0。V5.7.3 已包含 V5.7.2 的“对方已无豆”优先文字：只有监测侧持续确认 0 才显示，增长恢复后还原数字，最后一豆减少仍设置 15 秒底层截止时间。自动模式位置持续失效约 0.45 秒后限频重新验证，只有四格全有效才换框；换框忘记数量基准而保留已有截止时间，失败不把黑屏猜成 0。
- 暂停要停捕获和豆子/时钟识别，保留已有冷却。只有会话释放且两个识别线程空闲后，才显示“捕获与识别已停止”。暂停中的切侧或校准不能复活后台采集；恢复后建立新基准。
- 游戏时间只接受 0–60；低于 10 秒要读完整小数布局。没有中央时钟的训练场不猜成 60，不阻止豆子和现实冷却的独立逻辑。
- 少豆时用新鲜确认游戏时间减 15，固定恢复点；中途补读用当前游戏时间减剩余现实冷却，只补算一次并明确标为估算。技能可能暂停游戏时间，不能用现实经过时间伪造游戏读数。
- V5.7.3 音效跟随同一个权威截止时间：减少播放开始音，当前轮自然到期仅一次结束音；增长/未知不响，清除取消待结束音，暂停保留。其媒体文件只记录本机路径，0% 静音；Windows 内部编码支持影响可播放性。

## 9. 未解决事项与诊断顺序

**用户曾明确报告，MuMu 实际游玩中的鼠标卡顿，点击暂停后仍卡。** V5.7.1 修复了源码中可证明的暂停仍采集和新帧通知问题，并通过自建窗口检查；没有真实 MuMu 鼠标端到端对照测量，因此没有保证根治该症状。不能把“预览卡”当成用户原来的报告。

WGC 优先捕获目标窗口，普通外部窗口覆盖通常不改变其源内容；兼容路径 `Graphics.CopyFromScreen` 依赖无遮挡屏幕。最小化、源停止渲染、受保护画面、驱动及游戏内部技能/菜单仍会失效。当前 WGC 把整个窗口读回 CPU，然后裁 ROI；小区域识别不等于小区域传输，高分辨率 GPU 读回、复制、锁等待和透明浮窗绘制仍有开销。

继续研究时先定位故障层级：

1. 在用户授权的相同场景，比较监测中、**暂停完成后**和完全退出程序的实际游戏响应。记录捕获后端与分段耗时；不以主界面是否展示预览推断后台负载。
2. 豆数未知：看实际裁图是否包含完整四格；区分取帧过期、定位失效、满豆流光、紫色/暗槽和连续确认，别统一放宽所有阈值。
3. 时间未知：先确认训练场是否本来没有时钟，再检查裁图是否包含小数末尾、OCR 依赖加载、完整布局与读数时效。未确认不是缺陷的唯一分类，也不能直接猜数字。
4. 倒计时回跳：核对 `Counter.Triggers`、原始/确认豆数与截止时间，看是否出现 `3→4→3` 假消费；计时重置是确认事件，不是小数显示器乱跳。
5. 长期资源：现有图片队列有上限并有 `Dispose` 路径，但没有多小时 MuMu 与多驱动压力证据，不能宣称不存在内存或 GDI/原生资源泄漏。

正常监测不持续保存临时截图或上传画面。主动诊断、校准、依赖恢复和启动失败才写相应文件。V5.7.3 音效设置另保存在 `%LOCALAPPDATA%\BeanStudio\sound-settings.xml`。公开交接中不得夹带用户原图、个人诊断、校准、私密媒体路径、账号或凭据；需要样本时由用户另行授权提供。

当前实现没有保证零误识别、一直小于 200 ms 的实战延迟、所有编码播放或所有机器无卡顿。GPU 先裁 ROI、按源帧节奏调度和专用模型训练是 [DESIGN.md 后续研究方向](DESIGN.md) 的研究选项，不是交付能力。

## 10. 许可与项目边界

本 PC 仓库原代码维持 [MIT](../LICENSE)，第三方组件维持各自许可，见 [THIRD_PARTY.md](../THIRD_PARTY.md)、[LICENSE_SOURCES.md](../LICENSE_SOURCES.md) 与 `licenses/`。不要把安卓资料包的非商业研究许可套到这里，也不要重写 MIT 原文。历史第三方二进制是否可随包分发，应按实际 NOTICE 与来源核对，不能只看项目自身 MIT。

这是独立图像识别工具，不注入游戏、不读取游戏进程内存、不模拟操作，也非游戏、MuMu 或 OBS 官方产品。项目拥有者结束本轮开发不等于程序已完美；下一模型接手时应继续保留已有失败和验证边界，不能把 iOS/安卓的实验结果当作 PC 已验证结果。
