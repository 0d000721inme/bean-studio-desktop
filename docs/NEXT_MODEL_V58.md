# 下一模型交接 · Windows V5.8

> 历史记录：本页描述 V5.8 本地构建阶段，当前主分支为 V5.8.1；请先看 [本版交接](NEXT_MODEL_V581.md) 和 [发布记录](PUBLICATION.md)。

本地 V5.8 以原 V5.7.3 为基线，继续使用 C# / .NET Framework / WinForms。用户本次要求安装并使用 taste-skill 改造既有桌面 UI，增加快捷键、四种浮窗、资源曲线，并在不明显增加延迟的前提下降低重复工作。没有迁移到 Web / Electron，也没有训练新的通用识别模型。**V5.8 尚未上传 GitHub**；旧版本发布链接仅用于历史参考。保留本项目 MIT 和第三方许可，不改许可证。

## 新模块与整合接口

| 文件 | 职责 / 主要接口 |
| --- | --- |
| `src/DesktopSettings.cs` | `DesktopSettings.Copy()` 草稿；`DesktopSettingsStore.Load/Save/Validate`；`OverlayStyle { Focus, Digits, Ribbon, Classic }` |
| `src/Hotkeys.cs` | `HotkeyManager.TryApply/Suspend/Resume/ProcessMessage/Dispose`，`Status/StatusChanged`；`HotkeyChord`、`DesktopHotkeyAction.ToggleOverlay/Rescan` |
| `src/DesktopSettingsPanel.cs` | 草稿对话框，保存委托 `Func<DesktopSettings,string>`：空串表示成功，错误串保持设置窗打开；`ResultSettings` 仅保存成功后产生 |
| `src/ProcessPerformance.cs` | `ProcessPerformance.SetEnabled/GetSnapshot/Dispose`；当前进程采样及有界历史，GPU 使用 PDH 计数器 |
| `src/PerformancePanel.cs` | 本程序 CPU / GPU / 工作集趋势；`SetSnapshot`，鼠标或方向键查看历史 |
| `src/GlassUI.cs` | 主窗 `ApplyVisualSettings` / `SaveDesktopSettings` / `OpenDesktopSettings`；在 `WndProc` 转交快捷键消息；重新扫描只提交后台请求 |
| `src/Overlay.cs` | `ApplySettings`、四种实际布局、缩放、背景强度、整秒显示；绘制 / 呈现缓存 |
| `src/BeanTimer.cs` | `Engine.RequestScan/TryTakeScanPreview`、`sourceGate` 来源 / 暂停事务、失效源清理、新帧与分类缓存、高精度计时请求生命周期；`HudHost` 与 CLI 入口 |
| `src/WindowFrames.cs` / `WindowCapture.cs` | WGC 序号、捕获时间戳、读回上限、`CaptureSince`；暂停时只允许显式一次兼容截图 |

设置独立保存在 `%LOCALAPPDATA%\BeanStudio\desktop-settings.xml`，不会覆盖原 `sound-settings.xml`。默认全局快捷键是 **Ctrl + Alt + F8** 开关浮窗、**Ctrl + Alt + F9** 重扫。默认 Focus、大小 100%、背景强度 18%、整秒 / 恢复点 / 性能 / 定位预览开启。

注册使用 `RegisterHotKey + MOD_NOREPEAT`，没有键盘 hook。主窗句柄创建前注入已加载设置，设计测试 `MainForm(false)` 禁止注册默认组合。设置录入前 `Suspend`，对话框关闭后 `finally Resume`；`TryApply` 在暂停录入时仍临时尝试注册验证冲突，之后立即释放。冲突恢复旧绑定；XML 保存失败再次恢复旧设置。窗口句柄重建重新注册，关闭时注销。`StatusChanged` 失败提示不可丢失，不能只显示配置写着“已开启”。

## 识别与占用方面的实际变化

豆子与游戏时间消费者各自跟踪新 WGC 帧序号和时间戳；没有新帧不算一次未知观察、不重复确认，也不把旧时间读数刷新成“新鲜”。这修复重复消费同一捕获图片造成的误确认机制，不以放宽阈值提高表面识别率。

相同局部像素通过 `PixelMap.SamePixels` 精确比较复用已有分类 / 成功时间识别结果；新帧仍参与状态确认，像素变化后重新识别。`AutoLocator` 和 `DarkGemShape` 的采样点 / 偏移改成静态常量，移除候选循环临时数组；保持原采样顺序、评分算式和检测门槛。

监测 WGC 读回最多约 60 次/秒，非监测定位预览约 20 次；这是上限，不是实际游戏 FPS 或端到端延迟。重扫在 ROI 工作线程执行，不调用 MuMu 前台切换。窗口尺寸变化会使旧位置失效并重新定位；主动 F9 重扫保留原冷却，重新建立数量基准。暂停中的重扫只做一次需要无遮挡的兼容屏幕截图，不能借重扫悄悄恢复连续 WGC。

`sourceGate` 串行化来源切换、暂停 / 恢复和关闭的外部副作用，避免旧配置在新配置之后重新开启 WGC / 时间线程。`gate` 保护快照和计时状态，不在其内部等待 WGC 释放。失效 HWND 通过 `ClearLostSource` 清除来源、扫描框、局部缓存与数量基准，停止 WGC / 游戏时间读取并释放本程序精度请求；**保留原 deadline 和 Triggers**，不把关闭窗口当成新回合作废计时。增加 `version` 拒绝在途旧结果提交，`RequestScan` 拒绝失效来源，等待用户重新选择有效 MuMu。

监测才请求 1 ms 系统计时精度；停止监测、暂停和退出释放本程序自己的 `timeBeginPeriod` 请求。15 秒截止时间始终用 `Stopwatch`，不依赖 UI tick。HUD 按实际显示内容缓存表面；默认整秒连同紧急颜色量化，减少百分之一秒数字及颜色持续变化，隐藏时不重复绘制。

资源图表统计 **当前 Bean Studio 进程**，约 1 Hz、有界 60 点，关闭图表或主窗不显示时停用额外采样。`SetEnabled(true)` 重复调用保持幂等，不重复唤醒采样；避免主界面布局 / 尺寸变化提高采样频率。CPU 按逻辑核心归一化；内存为工作集，可显示其占物理内存比例；GPU 取当前进程最忙 GPU Engine。PDH 无数据 / 不支持时显示不可用，不假装 0%，不能将这些曲线解释为 MuMu 或整机负载。

## 不能改变的用户规则

首次确认只建基准；确认减少设置完整 **15.000 现实秒**，再次减少重新设置完整 15 秒；增长不触发。技能黑屏、遮挡、未知帧不清除旧冷却。所选侧连续确认 0 豆显示“对方已无豆”，恢复增长后还原数字；最后一豆仍生成底层冷却及音效事件。

游戏时间只接受 0–60 秒及低于 10 秒的小数完整布局；少豆时固定 `游戏时间−15`，中途补读按剩余现实冷却估算并标 `≈`。不猜 60、不按现实时间补造暂停中的游戏时钟。开始 / 到期音效、独立开关、音量及自定义媒体路径沿用 V5.7.3；暂停不取消旧到期音，手动清空会取消该轮。

## 构建与相关 CLI

仍使用 64 位 Windows 10 1903+ / Windows 11、.NET Framework 4.8 / 4.8.1 及 OCR 所需 Visual C++ x64 运行库。运行本目录 `build.ps1`，准备好依赖时加 `-SkipDependencies`。保留 `Tesseract.dll`、`x64`、`tessdata`、`sounds` 和 `clock-templates.txt`。不要把从 GitHub 克隆的旧主分支当成本地 V5.8。

以下参数都从根目录 `BeanStudio.exe` 调用。**只有 `--capture58-test` 的第二项是输出报告 txt 文件，其他下列 58 入口接目录。** 不要将 capture58 的目录传入当作文件，也不要用这个调用错误判断回归失败。

| 命令参数 | 输出 / 范围 |
| --- | --- |
| `--settings58-test <报告目录>` | XML、绑定验证、冲突回滚、暂停录入、句柄与注销 |
| `--performance58-test <报告目录>` | CPU / GPU 聚合、有界历史、不可用状态和资源释放 |
| `--overlay58-test <报告目录>` | 四风格 / 大小 / 背景极值、命中区域、数字与绘制缓存 |
| `--capture58-test <报告文件.txt>` | 合成像素缓存、Detector 等价及新帧状态规则 |
| `--studio58-test <报告目录>` | 主窗布局、真实窗口 RegisterHotKey / WM_HOTKEY 路由、设置窗口渲染 |
| `--render-settings58 <输出目录>` | 设置窗口默认 / 最小 / 错误截图及草稿交互检查 |
| `--resize58-test <报告目录>` | 已注册；合成窗口实际 WGC 放大 / 重扫、旧计时保留、暂停单次扫描、来源关闭及在途结果拒绝 |
| `--pause-test <报告目录>` | 自建动态窗口真实 WGC、暂停 / 恢复、后台计数屏障与 timer 请求释放 |
| `--clock57-test --tracker <报告文件.txt>` | 原时钟连续确认及恢复点状态检查，无用户图片 |
| `--sound-test <报告目录>` | 原音效事件、配置与完整截止时刻 |
| `--sound-engine-test <报告目录>` | 原引擎计时与音效实际链路 |
| `--sound-ui-test <报告目录>` / `--render-sounds <输出目录>` | 原音效设置交互 / 渲染 |
| `--ui-test <报告目录>` | 原主窗交互和实际 WGC 绘制，须单独顺序执行 |

Windows PowerShell 启动 WinExe 检查时使用 `Start-Process -WindowStyle Hidden -Wait` 并检查退出码和报告。带合成测试窗口的检查仍可能短暂显示自己的窗口。**GUI / WGC 测试必须串行执行**，并行窗口会争抢焦点、遮挡截图或关掉菜单；未完成时不要先宣布通过。

## 最终产物验证与边界

root 最终 EXE 构建成功、无编译警告。最终重跑 performance **28**、Studio **189**、设置 UI **8**、Resize **25**（无 SKIP）、Pause **28**、capture **19**、旧 UI **8** 项通过。此前整合构建的 settings **30**、overlay **54**、clock tracker **12**、Sound **33 + 12** 通过，之后这些检查覆盖的代码段未再改变。Studio 包括真实窗口注册、WM_HOTKEY 消息路由和真实进程性能数据图表渲染；消息路由测试不等于实际玩家长按物理键的所有输入环境。

capture58 第一次因传入目录而不是报告 txt 文件导致报告写入失败，修正调用后 **19** 项通过。旧 UI 交互第一次与其他 GUI 检查并行，受焦点 / 菜单干扰失败；隔离顺序执行后 **8** 项通过。保留这些边界，不将环境争抢误写成产品已永久修复的 Bug。

受控旧 / 新定位比较 **12** 次、暗点 `Score` **12,000** 次保持完全一致；单次合成定位分配从 **521,059,608** 降至 **2,646,160 bytes**。这是特定合成输入与比较环境的分配测量，不是整机内存、长期泄漏测试或 MuMu 实战性能结论。

Resize58 实际 WGC 正向检查已 **25 PASS、无 SKIP**，入口已注册。root 最终单次记录为重扫 API 返回 **0.418 ms**、合成 4→3 绘制到触发 **78.81 ms**（确认阶段 **52.02 ms**）、合成窗口 960→1280 绘制到有效重定位 **132.20 ms**。这是特定本机受控窗口和单次测量，不能当作 MuMu 实战延迟或延迟上限。

最终数量、失败修复和构建信息以 [V5.8 验证汇总](V58_VERIFICATION.md) 为准；不要把先前一轮 performance 27 / Studio 188 数量当成最终结果。没有测量真实 MuMu 多局独立准确率、鼠标卡顿改善比例、GPU 占用改善比例或 200 ms 端到端延迟。

后续先阅读最新汇总和失败记录，再决定是否改算法。不要重新上传用户原始截图、诊断、本机配置或媒体；不要把合成回归提升写成实战准确率，也不要擅自改为注入 / 游戏内存读取方案。
