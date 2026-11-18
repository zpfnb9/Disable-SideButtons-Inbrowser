# Disable-SideButtons-Inbrowser

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)](#)
[![Size](https://img.shields.io/badge/size-12%20KB-brightgreen.svg)](#)

**中文** · [English](#english)

> **关掉浏览器里鼠标侧键的「前进 / 后退」。**
> 填了一半的表单、看了一半的文档，不会再因为碰一下鼠标侧键就没了。
>
> **Turns off the mouse side buttons (Back / Forward) in your browser.**
> Half-filled forms and long documents survive an accidental brush against the side buttons.

只对你指定的浏览器生效，Office、游戏、资源管理器里的侧键照常可用。
Windows 10/11 · 单文件 12 KB · 免安装免驱动 · 无需管理员权限 · 不联网 · 无托盘图标

**快速开始** → [下载 exe](https://github.com/zpfnb9/Disable-SideGestures/releases) · [30 秒安装](#安装) · [它解决什么问题](#它解决什么问题)

---

## 它解决什么问题

鼠标左侧那两个侧键（XButton1 / XButton2）在浏览器里的默认行为是「后退 / 前进」。

问题在于它**没有确认、没有提示、状态不可恢复**：

- 在线填表填到一半，侧键一碰 → 退回上一页，表单内容全丢
- 在线文档 / 后台系统，退回再前进 ≠ 回到原状态，滚动位置和筛选条件都没了
- 双屏办公时鼠标停在另一个屏幕的浏览器上，误触一样会后退

而 Edge / Chrome 至今**没有**关闭这个行为的开关：

| 你可能会试的办法 | 结果 |
|---|---|
| 浏览器设置里找开关 | 没有这个选项 |
| 组策略 / 注册表（`DisableForwardAndBackButtons` 之类） | 不存在这个策略，无效 |
| 装浏览器扩展 | Chromium 不把侧键事件交给扩展，拦不到 |
| 用鼠标厂商软件（Razer Synapse / Logitech Options）改键 | 有效，但是**全局**的，游戏和快捷键一起废掉，还依赖厂商软件常驻 |
| 装第三方键鼠映射软件 | 能用，但为了一个开关装一套常驻软件有点重 |

Disable-SideGestures 换个思路：**在系统把侧键事件交给浏览器之前就拦下来**，并且只拦你指定的那个浏览器。

## 工作原理

一个 `WH_MOUSE_LL` 低级鼠标钩子。回调里做两道判定，命中任意一条就把事件吞掉（返回 `1`，不再传给后面的钩子和窗口）：

| 判定 | 场景 | 日志里的 reason |
|---|---|---|
| 前台窗口属于目标进程 | 正常在浏览器里工作 | `foreground` |
| 光标下的顶层窗口属于目标进程 | 双屏：浏览器在另一块屏 / 后台，鼠标悬停在它上面时误触 | `under-cursor` |

第二条容易忽略但很关键：鼠标按键消息是投递给**光标下**的窗口的，所以浏览器不在前台时误触同样会触发导航 —— 只判断前台窗口是不够的。

其他实现细节：

- 目标进程名默认 `msedge`，`--only` 可指定多个（`msedge,chrome,brave` 等）。
- 进程名带缓存，钩子回调里不做重活，不拖慢鼠标。
- 日志默认写到 `%LOCALAPPDATA%\DisableSideGestures\guard.log`，超过 512 KB 自动轮转。
- 单实例运行（互斥体保护），重复启动不会装两个钩子。

## 支持哪些浏览器

拦截依据是**进程名**，与浏览器品牌无关：Edge、Chrome、Brave、Vivaldi、Opera 都是 Chromium 内核，侧键走的是同一个 `XBUTTON` 事件。

| 浏览器 | 进程名 |
|---|---|
| Microsoft Edge | `msedge` |
| Google Chrome | `chrome` |
| Brave | `brave` |
| Vivaldi | `vivaldi` |
| Opera | `opera` |
| Chromium | `chromium` |

默认只屏蔽 `msedge`。默认值刻意不扩大范围 —— 工具应该只做你要求的那一件事。

## 安装

需要 Windows 10/11 + .NET Framework 4.x（系统自带）。

### 方式一：脚本安装（推荐）

```powershell
git clone https://github.com/zpfnb9/Disable-SideGestures.git
cd Disable-SideGestures
.\build.cmd
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Targets msedge,chrome
```

`install.ps1` 会把程序复制到 `%LOCALAPPDATA%\DisableSideGestures\`，在「启动」文件夹里创建快捷方式（带 `--only` 参数），并立刻启动。**全程不需要管理员权限。**

### 方式二：手动

1. 从 [Releases](https://github.com/zpfnb9/Disable-SideGestures/releases) 下载 `DisableSideGestures.exe`（或自己运行 `build.cmd` 编译）。
2. 放到一个固定目录，例如 `%LOCALAPPDATA%\DisableSideGestures\`。
3. `Win+R` → 输入 `shell:startup` → 回车，给 exe 建一个快捷方式放进这个启动文件夹。
4. 双击运行一次。

## 卸载

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

或者手动三步：任务管理器结束 `DisableSideGestures.exe` → 删掉 `shell:startup` 里的快捷方式 → 删掉 `%LOCALAPPDATA%\DisableSideGestures\`。

只想**临时**关掉（下次开机恢复）：任务管理器里结束 `DisableSideGestures.exe` 即可。

## 命令行参数

```text
DisableSideGestures.exe [--only <a,b>] [--log <path>] [--nolog] [--debug] [--version] [--help]

  --only <a,b>     target process names, comma separated (default: msedge)
  --log <path>     write the interception log to a specific file
  --nolog          do not write a log file
  --debug          log every side-button event with the resolved names
  --version        print version
```

## 验证它真的在工作

仓库里带了一个测试工具 `tools\XButtonHarness.cs`（编译产物 `dist\XButtonHarness.exe`），它既能**注入**侧键事件，也能开窗口**统计**收到的侧键事件，于是可以在不碰真实浏览器的前提下做严格的对照实验：

```powershell
# 把测试工具复制一份改名成 msedge.exe，模拟生产环境的目标进程
copy dist\XButtonHarness.exe dist\msedge.exe

# 1. 开一个测试窗口
.\dist\msedge.exe recv recv.log 60 RECV-T 60 60

# 2. 启动拦截器
.\dist\DisableSideGestures.exe --log guard.log

# 3. 注入一次侧键：应被吞掉，recv.log 不新增记录
.\dist\XButtonHarness.exe send 1 RECV-T 1

# 4. 停掉拦截器再注入一次：recv.log 应新增 2 条（按下 + 抬起）
```

`guard.log` 里应出现：

```text
started v2.0.0 targets=msedge
blocked msg=0x020B reason=foreground
blocked msg=0x020C reason=foreground
```

`sendat` 用于测试 `under-cursor` 分支：它只把光标移到目标窗口上，**不**改变前台窗口。
`--debug` 会记录每一个侧键事件以及它解析到的前台 / 光标下进程名，排查「侧键没生效」时最有用。

## 排查

日志：`%LOCALAPPDATA%\DisableSideGestures\guard.log`

| 现象 | 原因 |
|---|---|
| 按侧键仍然后退，日志里**有** `blocked` | 事件确实被拦了但页面还是动了 —— 极罕见，提 issue 附日志 |
| 按侧键仍然后退，日志里**没有** `blocked` | 侧键没有发出 XButton 事件。多半是鼠标厂商软件（Razer Synapse、Logitech Options 等）把侧键映射成了别的键（例如 `Alt+←`），需要在厂商软件里改回默认 |
| 日志里只有 `started` | 正常，说明还没拦截过任何事件 |

## 已知限制

- 只处理 `XBUTTON1/2`。侧键若被厂商软件映射成键盘快捷键，本工具无能为力。
- 需要进程常驻（约 4 MB 内存）。程序退出后立即恢复原状。
- 同一会话内单实例运行；要换目标就重跑 `install.ps1 -Targets ...`。
- 32 位系统会用 `Framework` 下的 `csc.exe`，`build.cmd` 已做回退。

## License

[MIT](LICENSE)

---

# English

## What problem does it solve?

The two buttons on the left side of a mouse (`XButton1` / `XButton2`) are bound to **Back / Forward** in every major browser.

There is no confirmation, no undo, and the state is not recoverable:

- A half-filled web form is gone after a single accidental press.
- Going forward again does **not** restore scroll position, filters or form state.
- On a dual-monitor setup, a press while the cursor happens to rest over the browser window navigates it just the same.

Edge and Chrome have no setting for this:

| What you might try | Result |
|---|---|
| Browser settings | No such option |
| Group policy / registry (`DisableForwardAndBackButtons` and similar) | No such policy exists |
| A browser extension | Chromium never exposes side-button events to extensions |
| Vendor software (Razer Synapse, Logitech Options) remapping | Works, but **globally** — kills the buttons in games and shortcuts too, and requires the vendor app to stay resident |
| A third-party key-mapper | Works, but a resident suite is a lot of machinery for one switch |

Disable-SideGestures takes a different route: **swallow the side-button events before the system hands them to the browser**, and only for the browsers you name.

## How it works

A `WH_MOUSE_LL` low-level mouse hook. The callback applies two rules; if either matches, the event is swallowed (returns `1`, never reaching later hooks or the window):

| Rule | Scenario | `reason` in the log |
|---|---|---|
| The foreground window belongs to a target process | Normal browsing | `foreground` |
| The top-level window under the cursor belongs to a target | Dual monitor / background window: the browser is not focused but the cursor rests over it | `under-cursor` |

The second rule matters more than it looks: mouse button messages are delivered to the window **under the cursor**, so a background browser window can still navigate. Checking only the foreground window is not enough.

Other details:

- Target process names default to `msedge`; `--only` accepts several (`msedge,chrome,brave`).
- Process names are cached, so the hook callback stays cheap and never delays the mouse.
- The log goes to `%LOCALAPPDATA%\DisableSideGestures\guard.log` and rotates at 512 KB.
- Single instance (named mutex), so you never end up with two hooks.

## Supported browsers

Matching is by **process name**, not by brand: Edge, Chrome, Brave, Vivaldi and Opera are all Chromium, and their side buttons all arrive as the same `XBUTTON` event.

| Browser | Process name |
|---|---|
| Microsoft Edge | `msedge` |
| Google Chrome | `chrome` |
| Brave | `brave` |
| Vivaldi | `vivaldi` |
| Opera | `opera` |
| Chromium | `chromium` |

Only `msedge` is targeted by default. The default is deliberately narrow — a tool should do exactly what it was asked to do.

## Install

Windows 10/11 with .NET Framework 4.x (bundled with the OS).

### Option 1: script (recommended)

```powershell
git clone https://github.com/zpfnb9/Disable-SideGestures.git
cd Disable-SideGestures
.\build.cmd
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Targets msedge,chrome
```

`install.ps1` copies the binary to `%LOCALAPPDATA%\DisableSideGestures\`, creates a Startup shortcut (with the `--only` arguments) and launches it. **No administrator rights at any point.**

### Option 2: manual

1. Download `DisableSideGestures.exe` from [Releases](https://github.com/zpfnb9/Disable-SideGestures/releases) (or run `build.cmd` yourself).
2. Put it in a stable folder, e.g. `%LOCALAPPDATA%\DisableSideGestures\`.
3. `Win+R` → `shell:startup` → put a shortcut to the exe in that folder.
4. Run it once.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

Or manually: end `DisableSideGestures.exe` in Task Manager → delete the shortcut from `shell:startup` → delete `%LOCALAPPDATA%\DisableSideGestures\`.

To disable it temporarily (it comes back at the next logon): just end `DisableSideGestures.exe` in Task Manager.

## Command line

```text
DisableSideGestures.exe [--only <a,b>] [--log <path>] [--nolog] [--debug] [--version] [--help]

  --only <a,b>     target process names, comma separated (default: msedge)
  --log <path>     write the interception log to a specific file
  --nolog          do not write a log file
  --debug          log every side-button event with the resolved names
  --version        print version
```

## Verify it actually works

The repo ships a test tool, `tools\XButtonHarness.cs` (built to `dist\XButtonHarness.exe`), that can both **inject** side-button events and open a window that **counts** the ones it receives. That makes a strict A/B test possible without touching a real browser:

```powershell
# Rename a copy of the harness to msedge.exe so it looks like the production target
copy dist\XButtonHarness.exe dist\msedge.exe

# 1. Open a test window
.\dist\msedge.exe recv recv.log 60 RECV-T 60 60

# 2. Start the guard
.\dist\DisableSideGestures.exe --log guard.log

# 3. Inject one side-button press: it must be swallowed, recv.log stays unchanged
.\dist\XButtonHarness.exe send 1 RECV-T 1

# 4. Stop the guard, inject again: recv.log should gain 2 lines (down + up)
```

`guard.log` should contain:

```text
started v2.0.0 targets=msedge
blocked msg=0x020B reason=foreground
blocked msg=0x020C reason=foreground
```

`sendat` exercises the `under-cursor` rule: it moves the cursor over the target window **without** changing the foreground window.
`--debug` logs every side-button event together with the resolved foreground / under-cursor process names — the first thing to reach for when "it doesn't work".

## Troubleshooting

Log: `%LOCALAPPDATA%\DisableSideGestures\guard.log`

| Symptom | Cause |
|---|---|
| Still navigates, and the log **does** show `blocked` | The event was swallowed yet the page moved — extremely rare; open an issue with the log |
| Still navigates, and the log does **not** show `blocked` | The button never produced an `XButton` event. Most likely vendor software (Razer Synapse, Logitech Options …) remapped it to something else (e.g. `Alt+←`); restore the default mapping there |
| The log only shows `started` | Normal — nothing has been intercepted yet |

## Known limitations

- Only `XBUTTON1/2` are handled. If the side buttons are remapped to keyboard shortcuts by vendor software, this tool cannot help.
- The process must stay running (~4 MB of RAM). Quitting it restores the original behaviour immediately.
- Single instance per session; change targets by re-running `install.ps1 -Targets ...`.
- On 32-bit Windows `build.cmd` falls back to the `Framework` (x86) compiler.

## License

[MIT](LICENSE)

No admin rights, no drivers, no network access, ~12 KB, one process, no tray icon. Other applications keep their side buttons.
