# ADB 工具箱（AdbGui）

把常用 adb 操作变成「点一下就完事」的 Windows 桌面小工具：装/卸应用、截屏、录屏、推拉文件、实时日志、无线调试、按键、设备信息等。

- **单文件 exe**，不需要 .NET SDK / Android Studio —— 用系统自带的 .NET Framework 编译器就能编译
- 启动自动在 PATH、`ANDROID_HOME`、`%LOCALAPPDATA%\Android\Sdk\platform-tools`、`C:\platform-tools`、`C:\adb` 等位置查找 `adb.exe`，找不到时手动指定一次即可（会记住）
- 多设备下拉切换，自动刷新设备状态（已连接 / 未授权 / 离线）
- 命令串行执行（队列 + 可随时停止），输出为彩色日志，可过滤与保存

> 命令参考以 Android 官方文档为准：<https://developer.android.google.cn/tools/adb?hl=zh-cn>

## 编译与运行

需要一台装了 .NET Framework 4.x 的 Windows（Win7 SP1+，一般系统自带）。

```powershell
# 在项目根目录执行，会调用系统 C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe 编译
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

编译产物：`bin\AdbGui.exe`（x64、单文件、自带深色主题图标，无需任何运行时安装）。

**首次使用**：
1. 手机开启「开发者选项 → USB 调试」，用数据线连电脑，在弹窗里点「允许」。
2. 工具会自动找到 `adb.exe`；如果它在非标准目录，点右上角「选择 adb」手动指向 `platform-tools\adb.exe`。
3. 左上角下拉框应出现设备；点「刷新设备」可手动刷新。

> 参考文档「查询已连接设备」：`adb devices -l`

## 功能与对应 adb 命令

界面上的每个按钮，背后都是一条标准 adb 命令（多设备时自动加 `-s <序列号>`）。

| 界面按钮 | 实际执行的命令 | 文档依据 |
| --- | --- | --- |
| 刷新设备 | `adb devices -l` | 查询已连接设备 |
| 安装应用 | `adb install -r [-g] 文件` | 安装 APK（`-g` 自动授予运行时权限） |
| 软件管理 → 卸载 | `adb shell pm uninstall 包名` | 软件包管理器 (pm) |
| 软件管理 → 强制停止 | `adb shell am force-stop 包名` | Activity 管理器 (am) |
| 软件管理 → 清除数据 | `adb shell pm clear 包名` | pm |
| 软件管理 → 启动 | `adb shell monkey -p 包名 -c android.intent.category.LAUNCHER 1` | am |
| 重启 / Bootloader / Recovery / 关机 | `adb reboot \| reboot bootloader \| reboot recovery \| reboot -p` | 重启相关 |
| 软重启（重启框架） | `adb shell stop` + `adb shell start`（仅重启 Android 框架，不断电/不重启内核） | 框架重启 |
| 开启无线调试 | `adb tcpip 5555` | 旧版无线（需 USB 初始） |
| 连接设备 | `adb connect <IP>:5555` | 无线调试 |
| 断开无线 | `adb disconnect` | 无线调试 |
| 按键 | `adb shell input keyevent KEYCODE_*` | 按键输入（Home/Back/音量等） |
| 输入文字 | `adb shell input text "..."` | input 工具 |
| 设备信息 | `adb shell getprop ro.*` / `dumpsys battery` / `wm size` / `wm density` / `settings get system peak_refresh_rate` / `dumpsys display`（刷新率） | shell 命令 |
| 截屏 | `adb shell screencap -p /sdcard/...` + `adb pull` → 工具内展示，关闭后自动删除临时文件 | 截屏 |
| 实时日志 | `adb logcat -v time` | Logcat |
| 实时 FPS | `adb shell dumpsys window`（前台包名）+ `adb shell dumpsys gfxinfo <包名>`（累计帧数差分） | gfxinfo 帧统计 |
| 自定义命令 | 直接原样执行你输入的 adb 参数 | shell / 各命令 |

## 无线调试（Wi-Fi 连接）

两种玩法，工具都支持：

1. **旧版（Android 10 及更低，需先 USB 连一次）**：在工具里点「无线连接 ▾ → 开启无线调试 (tcpip 5555)」，拔掉数据线，再点「连接设备…」输入手机 IP（如 `192.168.1.23:5555`）。
2. **新版（Android 11+ 的无线调试配对）**：手机「开发者选项 → 无线调试 → 使用配对码配对设备」，记下 IP、端口、配对码，在自定义命令框输入 `pair <IP>:<端口>` 并按提示输入配对码，之后 `adb connect`。

> 工具箱基于官方「无线调试」流程；配对相关命令（`adb pair`）可直接在底部命令框执行。手机与电脑需在同一 Wi-Fi。

## 实时日志

点工具条「实时日志」即开始 `adb logcat -v time` 流式输出（多设备自动 `-s`）。右上角「日志过滤」框输入关键字只保留相关行；点「停止日志」结束，点「清空 / 保存日志」整理结果。

## 常见问题

- **检测不到设备**：确认手机已开启 USB 调试，且弹出的「允许 USB 调试」已点允许（勾选「一律允许」更省事）。换根线/换 USB 口也常有用。
- **offline / 未授权**：重新插拔并在手机上确认授权；必要时 `adb kill-server` 后刷新。
- **找不到 adb.exe**：点右上角「选择 adb」手动指向 `platform-tools\adb.exe`，路径会记住。
- **安装失败**：`already exists` 多为版本冲突（先卸载或用更高版本 APK）；`insufficient storage` 是空间不足；`incompatible` 多为 CPU 架构/系统版本不符。
- **input text 中文无效**：取决于手机输入法，建议改用英文或数字；中文可借助其他输入方案。

> 更多排错见官方文档，例如 `adb server-status`、`adb kill-server`。

## 源码结构

| 文件 | 说明 |
| --- | --- |
| `build.ps1` | 调用系统 csc.exe 编译，生成 `bin\AdbGui.exe`（含自绘图标） |
| `src/Adb.cs` | adb.exe 定位、进程执行（无窗口 / UTF-8 / 超时 / 可取消）、设置存储 |
| `src/Theme.cs` | 深色主题与自绘控件（圆角按钮、卡片、状态点、输入框、菜单、图标） |
| `src/Dialogs.cs` | 通用输入窗、可搜索列表窗（应用管理用） |
| `src/MainForm.cs` | 界面布局、设备列表解析与刷新、菜单 |
| `src/MainForm.Exec.cs` | 串行命令队列、彩色输出、命令拼装与转义 |
| `src/MainForm.Actions.cs` | 各项具体操作（安装/截屏/录屏/推拉/无线/信息/日志） |
| `src/Program.cs` | 入口（单实例互斥 + 崩溃日志 `%APPDATA%\AdbGui\crash.log`） |

## 许可与参考

- 工具本身按 MIT 思路可自由使用；adb 及 Android 名称、Logcat 等属于 Google / Android 相关商标，请遵循其许可。
- 命令语义与示例参考 Android 官方《Android 调试桥 (adb)》中文文档：<https://developer.android.google.cn/tools/adb?hl=zh-cn>
