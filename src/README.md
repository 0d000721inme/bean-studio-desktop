# 桌面源码

这里保留独立桌面 V5.7.1 的完整 C# 源码，包括暂停修复与历史检查辅助类。

- BeanTimer.cs：主入口、豆子判定、监测与触发逻辑。
- AutoLocator.cs / DarkGemShape.cs：豆子区域与暗豆轮廓。
- WindowCapture.cs / WindowFrames.cs：Windows Graphics Capture 和兼容捕获。
- GameClock.cs / ClockWorker.cs：数字识别、连续确认与恢复点计算。
- GlassUI.cs / Overlay.cs / ClockPanel.cs：主界面、透明浮窗和区域校准。
- PauseTests.cs：合成窗口的真实捕获停止、恢复、暂停期间计时及按钮交互检查。

应用入口为 MuMuBeans.Program；其他 Main 方法属于历史检查工具，由 build.ps1 明确选择应用入口。
