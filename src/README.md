# 桌面源码

当前 V5.8.1 增加 `RecoveryDisplay.cs`、`Overlay581Tests.cs`、`Drag581Tests.cs` 和 `CaptureStatus581Tests.cs`，更新双主数字、设置 V1→V2 迁移、拖动展示及状态锁读取。先看 [V5.8.1 交接](../docs/NEXT_MODEL_V581.md)。以下保留 V5.8 模块介绍。

这里是基于 V5.7.3 继续制作的独立桌面 V5.8 C# 源码，保留开始 / 结束音效、四空豆、锁定位置恢复与暂停规则，新增快捷键、四风格浮窗、性能图表和新帧处理优化。源码已同步到主分支及 v5.8.1 标签。详细交接见 [NEXT_MODEL_V58.md](../docs/NEXT_MODEL_V58.md)，本次结果见 [V58_VERIFICATION.md](../docs/V58_VERIFICATION.md)。

- BeanTimer.cs：主入口、豆子判定、监测与触发；后台重扫、来源 / 暂停事务、失效源清理、新帧 / 像素缓存和高精度计时请求释放。关闭来源保留旧截止时间与触发记录，使用 version 丢弃在途旧结果。
- AutoLocator.cs / DarkGemShape.cs：豆子区域与暗豆轮廓；采样点改为静态常量，移除热路径临时分配，判定门槛与评分不变。
- WindowCapture.cs / WindowFrames.cs：Windows Graphics Capture 和兼容捕获；新帧序号 / 时间戳、60 / 20 读回上限及暂停单次扫描。
- GameClock.cs / ClockWorker.cs：数字识别、连续确认与恢复点计算。
- GlassUI.cs / Overlay.cs / ClockPanel.cs：主界面、四种逐像素透明浮窗、绘制缓存和区域校准。
- Hotkeys.cs / DesktopSettings.cs / DesktopSettingsPanel.cs：RegisterHotKey 注册生命周期、冲突回滚、本机独立 XML 配置与设置草稿；没有键盘 hook。
- ProcessPerformance.cs / PerformancePanel.cs：本程序 CPU / GPU / 工作集采样与有界曲线，约 1 Hz，重复启用不重复唤醒。
- DesktopSettingsTests.cs / ProcessPerformanceTests.cs / Overlay58Tests.cs / Capture58Tests.cs：新增模块回归，`--capture58-test` 接报告 txt 文件，其余相关 58 参数接目录。
- Studio58Tests.cs：主窗 / 设置布局、真实窗口 RegisterHotKey 与 WM_HOTKEY 消息接线和真实进程性能样本渲染。
- Resize58Tests.cs：合成窗口的实际 WGC 放大重定位、重扫、暂停单次截图和旧冷却保留检查。
- PauseTests.cs：合成窗口的真实捕获停止、恢复、暂停期间计时及按钮交互检查。
- EmptyBeanTests.cs / BeanRelocationTests.cs：四空豆样本与遮挡、计数状态、显示恢复，以及真实自建窗口同尺寸位置切换检查；样本由运行参数提供，不内嵌用户截图。
- SoundTests.cs：音效触发、结束去重、清除取消和相关设置检查；实际媒体兼容性以本机播放验证为准。

应用入口为 MuMuBeans.Program；其他 Main 方法属于历史检查工具，由 build.ps1 明确选择应用入口。
