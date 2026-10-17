# 下一模型构建与交接入口 · Windows V5.8.1

更新日期：2026-10-17。当前主分支与 `v5.8.1` 标签对应已完成的 PC V5.8.1，包括 V5.8 的快捷键、四种浮窗、性能图表、新帧处理，以及 V5.8.1 的双主数字恢复点、小数默认和拖动响应修复。本次只整理发布状态、上传现成源码和原构建，没有继续修改功能或重新编译。

1. [V5.8.1 交接](NEXT_MODEL_V581.md)：新增模块、配置迁移、拖动响应、不可改变的计时规则和局限。
2. [V5.8.1 使用](V581_USAGE.md)：现实冷却与游戏恢复点、估算、零豆和快捷键。
3. [V5.8.1 验证](V581_VERIFICATION.md)：实际 CLI、报告、合成测试与实战结论的边界。
4. [V5.8 基础模块](NEXT_MODEL_V58.md)：热键、设置事务、性能、捕获序号与生命周期。
5. [发布记录](PUBLICATION.md) / [发布清单](publication-manifest.json)：源码和原运行包对应关系、校验值。

## 构建

64 位 Windows 10 1903+ 或 Windows 11，.NET Framework 4.8 / 4.8.1、Windows WinRT 元数据、OCR 所需 Microsoft Visual C++ x64 运行库。

```powershell
git clone https://github.com/0d000721inme/bean-studio-desktop.git
cd bean-studio-desktop
git checkout v5.8.1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test58.ps1 -SkipBuild
```

依赖恢复从固定官方地址下载并校验 SHA-256。已有依赖时构建可加 `-SkipDependencies`。测试脚本保留旧文件名，但默认检查本次变更路径；GUI/WGC 检查必须串行，在可显示自身测试窗口的 Windows 桌面执行。测试不启动 MuMu。未自行执行的检查不要声称通过。

运行发布页完整 ZIP 时保留 EXE、Tesseract.dll、x64、tessdata、sounds 和 clock-templates.txt。源码包不带运行依赖；不要与旧版目录混用。

## 后续维护

恢复点是固定游戏时间值，不是第二条现实倒计时。确认减少重置完整 15 秒，增长不触发，黑屏不清零，未知/估算不得伪装确认。不要暂停识别来伪装拖动流畅。本版修复了 UI 重绘和状态锁等待路径，没有测量 MuMu 实战流畅度或证明所有设备低于 200 ms。

PC 自有代码维持 MIT，第三方依赖维持各自通知；用户截图、个人诊断、校准、自定义媒体与凭据不得随发布上传。旧 V5.7.3 交接可从该版本标签下的同名文件查看。
