# VMMicControl

通过 Voicemeeter Remote API 控制麦克风通道开关的小工具，带系统托盘 UI。

## 功能

- 自动识别 Voicemeeter 的麦克风通道（Strip），点击卡片即可静音 / 取消静音
- 窗口关闭时最小化到系统托盘，不退出进程；双击托盘图标恢复窗口
- 支持全局快捷键切换静音
- 设置自动记忆（保存上次状态与配置）

## 编译

不需要 Visual Studio，也不需要 .NET SDK，使用系统自带的 .NET Framework 编译器即可：

```powershell
.\build.ps1
```

生成的 exe 位于 `bin\VMMicControl.exe`。

## 图标

程序图标 `app.ico`（多分辨率 256/48/32/16）在编译时通过 `build.ps1` 的 `/win32icon` 嵌入 exe，窗口标题栏与文件资源管理器中都会显示它；托盘图标另用手绘的麦克风 / 静音状态图标（与界面同风格）。

## 运行

直接运行 `bin\VMMicControl.exe`，需本机已安装并运行 Voicemeeter（Voicemeeter / Banana / Potato 均可）。
