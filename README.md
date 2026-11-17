# EdgeSideGuard

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)](#)

**在 Microsoft Edge 里关掉鼠标侧键的「前进 / 后退」，专治误触导致的网页丢失。**

只作用于 Edge，Office、游戏、资源管理器里的侧键照常可用。单文件、约 11 KB、零依赖、无需管理员权限、不装驱动、不联网、不显示托盘图标。

```
误触鼠标侧键  ──▶  Windows 输入队列  ──▶  Edge 收到 WM_XBUTTONUP  ──▶  页面后退  💥
                                        ▲
                          EdgeSideGuard 在这里把事件吞掉  ✋
```

---

## 为什么需要它

在 Edge 里填表单、看文档、写在线表格时，很容易碰到鼠标左侧的两个侧键（XButton1 / XButton2），浏览器会立刻后退或前进一页 —— 页面状态、未提交的输入、滚动位置全部丢失，而且**后退再前进通常也回不到原来的状态**。

而 Edge / Chromium 至今**没有**关闭这个行为的开关：

| 方案 | 结果 |
|---|---|
| Edge 设置（含 `edge://settings`、鼠标手势） | 没有侧键相关选项 |
| 组策略 / 注册表（含 `DisableForwardAndBackButtons` 之类传言） | 无此策略，不生效 |
| 浏览器扩展 | Chromium 不把侧键事件暴露给扩展，拦不到 |
| 鼠标厂商软件（Razer Synapse / Logitech Options）改键 | 有效，但是**全局**的，游戏和快捷键一起废掉，还依赖厂商软件常驻 |
| 改鼠标驱动 / 装第三方键鼠映射软件 | 能实现，但装驱动/常驻软件偏重 |

EdgeSideGuard 走的是另一条路：**在系统把侧键事件交给 Edge 之前就拦下来**，并且只对 Edge 生效。

## 工作原理

核心是一个 `WH_MOUSE_LL` 低级鼠标钩子。钩子回调里做两道判定，命中任意一条就把事件吞掉（返回 `1`，不再传给后面的钩子和窗口）：

| 判定 | 场景 | 日志里的 reason |
|---|---|---|
| 前台窗口属于目标进程 | 正常在 Edge 里办公 | `foreground` |
| 光标下的顶层窗口属于目标进程 | 双屏：Edge 在另一块屏 / 后台，鼠标悬停在它上面时误触 | `under-cursor` |

第二条很重要：鼠标按键消息是投递给**光标下**的窗口的，所以 Edge 不在前台时误触同样会触发导航 —— 只判断前台窗口是不够的。

其他细节：

- 目标进程名默认 `msedge`，可用 `--only` 改成任意程序（比如 `msedge, chrome`）。
- 进程名做了缓存，钩子回调里不做重活，不会拖慢鼠标。
- 日志默认写到 `%LOCALAPPDATA%\EdgeSideGuard\guard.log`，超过 512 KB 自动轮转。

## 安装

需要 Windows 10/11 和 .NET Framework 4.x（系统自带）。

### 方式一：脚本安装（推荐）

```powershell
git clone https://github.com/zpfnb9/EdgeSideGuard.git
cd EdgeSideGuard
.\build.cmd
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

`install.ps1` 会把程序复制到 `%LOCALAPPDATA%\EdgeSideGuard\`，在「启动」文件夹里创建快捷方式，并立刻启动它。**不需要管理员权限。**

### 方式二：手动

1. 从 [Releases](https://github.com/zpfnb9/EdgeSideGuard/releases) 下载 `EdgeSideGuard.exe`（或自己运行 `build.cmd` 编译）。
2. 把它放到一个固定目录（例如 `%LOCALAPPDATA%\EdgeSideGuard\`）。
3. `Win+R` → 输入 `shell:startup` → 回车，把该 exe 的快捷方式放进去。
4. 双击运行一次。

## 卸载

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

或者手动三步：任务管理器结束 `EdgeSideGuard.exe` → 删掉 `shell:startup` 里的快捷方式 → 删掉 `%LOCALAPPDATA%\EdgeSideGuard\`。

只想**临时**关掉（下次开机恢复）：任务管理器里结束 `EdgeSideGuard.exe` 即可。

## 命令行参数

```text
EdgeSideGuard.exe [--only <a,b>] [--log <path>] [--nolog] [--version] [--help]

  --only <a,b>     target process names, comma separated (default: msedge)
  --log <path>     write interception log to a specific file
  --nolog          do not write a log file
  --version        print version
```

## 验证它真的在工作

仓库里带了一个测试工具 `tools\XButtonHarness.cs`（编译产物 `dist\XButtonHarness.exe`），它既能**注入**侧键事件，也能开一个窗口**统计**收到的侧键事件，于是可以在没有浏览器的情况下做严格的对照实验：

```powershell
# 1. 开一个测试窗口（把测试工具复制一份改名成 msedge.exe，模拟生产环境的目标进程）
copy dist\XButtonHarness.exe dist\msedge.exe
.\dist\msedge.exe recv recv.log 60 RECV-T 60 60

# 2. 启动拦截器（只针对 msedge）
.\dist\EdgeSideGuard.exe --log guard.log

# 3. 注入一次侧键：事件应该被吞掉，recv.log 不应新增记录
.\dist\XButtonHarness.exe send 1 RECV-T 1

# 4. 停掉拦截器再注入一次：recv.log 应新增 2 条（按下 + 抬起）
```

`guard.log` 里应出现：

```text
started v1.0.0 targets=msedge
blocked msg=0x020B reason=foreground
blocked msg=0x020C reason=foreground
```

`sendat` 用于测试 `under-cursor` 分支：它只把光标移到目标窗口上，**不**改变前台窗口。

> 在真实 Edge 上验证：打开一个有两个历史记录的页面，按侧键 —— 拦截器运行时页面应停在原地，`guard.log` 里应出现 `blocked`。

## 构建

零依赖，只用系统自带的 .NET Framework 编译器：

```bat
build.cmd
```

产物在 `dist\`：`EdgeSideGuard.exe`（约 11 KB）和 `XButtonHarness.exe`。

## 排查

日志在这里：`%LOCALAPPDATA%\EdgeSideGuard\guard.log`

| 现象 | 原因 |
|---|---|
| 按侧键仍然后退，日志里**有** `blocked` | 说明事件被拦了但页面还是动了 —— 极罕见，可能是另一个进程在转发输入，提 issue 附日志 |
| 按侧键仍然后退，日志里**没有** `blocked` | 侧键没有发出 XButton 事件。多半是鼠标厂商软件（Razer Synapse、Logitech Options 等）把侧键映射成了别的键（例如 `Alt+←`），需要在厂商软件里改回默认 |
| 日志里只有 `started`，没有别的 | 正常，说明还没拦截过任何事件 |

## 已知限制

- 只处理 `XBUTTON1/2`。如果侧键被厂商软件映射成键盘快捷键，本工具无能为力（但那种情况也不会触发浏览器后退，除非正好映射成 `Alt+←`）。
- 需要进程常驻（约 4 MB 内存）。程序退出后立即恢复原状。
- 同一会话内单实例运行（有互斥体保护）。
- 32 位系统上需用 `Framework` 而不是 `Framework64` 的 `csc.exe`，`build.cmd` 已做回退。

## License

[MIT](LICENSE)

---

## English

**Disables the mouse side buttons (Back / Forward) in Microsoft Edge only.**

Edge (and Chromium) has no built-in switch for this, and policy/registry/extension workarounds do not exist. EdgeSideGuard installs a `WH_MOUSE_LL` low-level mouse hook and swallows `XBUTTON1` / `XBUTTON2` events before Edge sees them. It matches the target process by name (default `msedge`) using two rules: the target owns the foreground window, **or** the target's top-level window is under the cursor (so a background/second-monitor Edge window is covered too).

```powershell
.\build.cmd
powershell -ExecutionPolicy Bypass -File .\install.ps1   # installs to %LOCALAPPDATA%, adds a Startup shortcut
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 # removes everything
```

No admin rights, no drivers, no network access, ~11 KB, single process, no tray icon. Other applications keep their side buttons. MIT licensed.
