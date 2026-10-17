# V5.8.1 构建与验证

日期：2026-10-17。Windows x64，`.NET Framework` 系统 C# 编译器，`build.ps1 -SkipDependencies` 完整构建成功，无警告。最终 `BeanStudio.exe` SHA-256：

```text
7e98c0ae25dd7091da5dc7b126b848c115e3cb81f324c5126dac5ea210ef5650
```

本次没有启动或操作 MuMu，也未关闭用户仍在运行的旧 V5.8。测试只使用程序自己的窗口、代码合成图、状态机与显式锁等待；没有把实际游戏识别率或真实拖动帧率写成已验证。

## 最终 EXE 实际检查

| 范围 | 通过 | 证据 |
| --- | ---: | --- |
| 设置与快捷键，包括 V1→V2 显示迁移 | 34 | [settings](checks581/settings.txt) |
| 原浮窗回归，按新尺寸更新预期、显式启用整秒用例 | 54 | [overlay](checks581/overlay.txt) |
| 双主数字、解析、四风格、零豆恢复点与拖动表面 | 48 | [overlay581](checks581/overlay581.txt) |
| 捕获状态读锁隔离 | 4 | [capture-status](checks581/capture-status.txt) |
| 主窗双数字、移动消息、计时与取消/捕获丢失 | 26 | [drag](checks581/drag.txt) |
| 主窗布局、真实资源曲线、实际快捷键注册/路由 | 197 | [studio](checks581/studio.txt) |
| 设置窗口及草稿交互 | 8 | [settings-ui](checks581/settings-ui.txt) |
| 暂停、恢复、真实 WGC 与 WinMM 生命周期 | 28 | [pause](checks581/pause.txt) |
| 游戏时钟状态机与恢复点 | 12 | [clock](checks581/clock.txt) |
| 真实 WGC 合成窗放大、重新定位与源关闭 | 25 | [resize](checks581/resize.txt) |
| 原菜单、浮窗左右、缩放与实际界面绘制 | 8 | [ui](checks581/ui.txt) |

上述 CLI 都在最终 EXE 上运行。GUI/WGC 检查分段串行执行。所有实际用例通过，无产品断言失败；汇总脚本首次分别把 drag 数量预填为 27、Studio 预填为 195，实际报告为 26 和 197，均为 `FAILURES=0`。已按实际数量修正脚本，没有删除用例或把脚本的数量不匹配当成产品修复。Studio 数增加是新增两个主要标签在四次布局遍历中产生的检查。

没有重复执行未受本次改动影响的 GPU 采样算法、OCR 图片准确率、所有音频编码兼容检查。V5.8 的相关历史证据仍在 [历史验证](V58_VERIFICATION.md)，不能将其哈希与本版混用。

## 已查明的拖动问题

旧主窗每约 50 ms 无条件使 Badge、豆点、冷却轨道失效，每四个 Tick 克隆/重画 ROI。若可见数据未变，这些工作仍然发生。对原 V5.8 和本版分别编译后，以同样 100 次稳定状态 Tick 比较：

| 非必要失效次数 | V5.8 | V5.8.1 |
| --- | ---: | ---: |
| Badge | 100 | 0 |
| 豆点 | 100 | 0 |
| 冷却轨道 | 100 | 0 |
| ROI 预览 | 25 | 0 |

旧版本机平均 Tick 调用约 0.101 ms，本版最终测试约 0.021 ms。**这只是稳定状态的 UI 调用耗时，实际绘制/游戏/硬件竞争不全在这一计时内，不能换算成实战帧率改善。** [旧对照报告](checks581/drag-baseline.txt)

第二条确定的阻塞链为 `GlassUI.Tick → WindowCapture.Status → capture gate → 兼容截图 CopyFromScreen`。原界面只读状态也必须等截图锁。修复仅把不可变状态文字以 `volatile string` 发布，状态 getter 不再拿该锁；捕获和暂停生命周期保护不变。

同一探针人为持锁 250 ms，原编译版 getter 读取约 **250.264 ms**；最终版约 **0.002 ms**，文字保持一致。另一个内置回归连续读 1000 次也不等锁，且捕获读回计数为 0。探针没有调用 Main、WGC 或任何用户窗口。它证明锁隔离，不能说明用户显卡实际发生截图阻塞的频率。[旧探针](checks581/capture-status-before.txt) · [新探针](checks581/capture-status-after.txt)

主窗移动模式仅延后预览克隆与曲线绘制，主要数字仍由 WinForms Timer 更新。最终自身窗口 40 次实际 `SetWindowPos` 消息加消息循环持续约 597 ms，最大单次移动调用约 1.090 ms；原 deadline 差值 0，触发次数不增、Pause 不变，退出后 150 ms 检查到图表更新恢复。监测源在此 UI 测试中故意缺席，不能说这是 MuMu 连续监测下的拖动压力测试。

## 展示检查

主窗 1040×760、1240×860、1440×960 的双主数字区域与字号一致、互不重叠。近零红色、小数、`≈59.99` 极值、未知、回合不足和零豆恢复点都进入专项断言。主窗近似符号放在标题而不是挤占数字宽度；引擎原恢复点不改值。

浮窗四风格在 80% 和 140% 下命中区域一致。50 次位置变化不重建 Bitmap；只在正在拖动时限约 10 Hz 表面提交，松手或丢失鼠标捕获立即补最新数字，常态刷新不被节流。真实 `HudHost.UpdateHud` 零豆接线仍保留恢复点。

已查看最终主窗/紧凑主窗、主窗估算与紧急状态，以及实际浮窗 Focus、Classic、未知、零豆、回合不足 PNG。独立测试也检查四种风格及 80% 极值。所有画面是合成 UI 预览，不是实战截图或占用率承诺。

## 复现与边界

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipDependencies
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test58.ps1 -SkipBuild
# 只检查本次路径时，在 PowerShell 内：
.\scripts\test58.ps1 -SkipBuild -Suite settings,overlay581,capture-status,drag
```

沿用旧脚本文件名以保留入口，本版默认选择与本次改动相关的检查。源码包首次构建需不加 `-SkipDependencies` 以恢复固定依赖。WGC 检查需要能呈现自身合成窗口的 Windows 桌面；不要并行运行 GUI 检查，也不要因环境不支持就伪造通过。

WGC 整窗 GPU 读回与 MuMu 渲染仍可能争用硬件资源，本版没有改读回率或降低识别采样来掩盖卡顿。真实游戏体感、鼠标输入到屏幕延迟、长期内存泄漏及所有驱动组合仍未测量。原 15 秒现实期限、增长不触发、黑屏不清零、游戏暂停不外推、估算保留标记等规则不变。运行新版前请自行退出旧版，避免两份监测程序同时消耗资源。
