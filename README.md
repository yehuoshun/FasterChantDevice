# 300高速咏唱装置 2.0

> 300 英雄快捷喊话 & 自动嘲讽工具 — 手残党的终极解决方案  
> 技术栈：C# / .NET 8 / WPF / Windows 10+

[![Build](https://github.com/yehuoshun/FasterChantDevice/actions/workflows/build.yml/badge.svg)](https://github.com/yehuoshun/FasterChantDevice/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

## 📖 简介

打 300 英雄时手速跟不上嘴速？键盘只有两只手不够用？

**300高速咏唱装置**帮你解决：

- 🎮 **主动喊话**：F1 呼出悬浮面板，按数字键秒发骚话
- 🤖 **被动嘲讽**：OCR 自动检测击杀/死亡/助攻，自动发送对应台词
- 🔥 **连发模式**：多行台词按间隔逐条发送，刷屏不费力
- 🎯 **零校准**：按游戏窗口比例自动定位，无需手动调坐标

基于 [FasterChantDevice (Anran-233)](https://github.com/Anran-233/FasterChantDevice) 理念重写。

## ✨ 功能

### 主动模式 — 快捷喊话

```
F1 呼出半透明穿透面板
  ├─ 主面板：显示 10 个分组（0-9）
  ├─ 按数字键 → 展开该分组的发言列表
  └─ 按数字键 → 发送选中发言
      ├─ 连发 ON：逐行依次发出
      └─ 连发 OFF：关闭面板
```

- 悬浮面板 `WS_EX_TRANSPARENT` 穿透点击，不抢游戏焦点
- 分组名和发言内容完全自定义

### 被动模式 — 条件触发

| 事件 | 触发方式 | 说明 |
|------|----------|------|
| 🟢 开局 | K/D/A 归零 | 自动检测新对局开始 |
| ⚔️ 击杀 | K/D/A kill+1 | 自动发送击杀台词 |
| 💀 死亡 | K/D/A death+1 | 自动发送死亡台词 |
| 🤝 助攻 | K/D/A assist+1 | 自动发送助攻台词 |
| 🗣️ 骚话 | F2 手动 / 定时自动 | 可选手动、定时、或两者 |

- 骚话冷却：战斗事件后 5 秒内不触发，避免尴尬
- 手动骚话 10 秒冷却，防刷屏

### OCR 三引擎

| 引擎 | 目标区域 | 频率 | 可靠性 |
|------|----------|------|--------|
| K/D/A 数字识别 | 右上角 HUD | 500ms | 95%+，不受皮肤影响 |
| 击杀播报文字 | 中上弹出区 | 事件触发 | 受皮肤影响 |
| 像素变化检测 | 播报区域 | 持续 | 100%，零字体依赖 |

## 📦 安装

### 方式一：下载 Release（推荐）

从 [Releases](https://github.com/yehuoshun/FasterChantDevice/releases) 下载最新版本：

- `FasterChantDevice.exe` — 单文件自包含版，无需安装 .NET
- `FasterChantDevice-portable-x64.zip` — 便携版，解压即用

### 方式二：自行编译

```bash
git clone https://github.com/yehuoshun/FasterChantDevice.git
cd FasterChantDevice/src/FasterChantDevice
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 🎮 使用

### 快捷键

| 按键 | 功能 |
|------|------|
| `F1` | 打开/关闭悬浮喊话面板 |
| `F2` | 手动发送骚话（10s 冷却） |
| `数字键 0-9` | 面板中选取分组/发言 |
| `Esc` | 关闭面板 |

### 首次使用

1. 启动程序（系统托盘可见图标）
2. 右键托盘 → **英雄编辑** → 创建英雄方案
3. 填入开局/击杀/死亡/助攻/骚话台词
4. 配置快捷发言分组
5. 启动 300 英雄 → 自动检测游戏窗口 → 开始使用

### 托盘菜单

- **英雄编辑**：编辑当前英雄方案
- **设置**：调整检测参数
- **退出**：关闭程序

## 📝 英雄方案格式

方案文件存放在 `%LocalAppData%\FasterChantDevice\heroes\{英雄名}.json`

```json
{
  "name": "卫宫",
  "triggers": {
    "game_start": ["来了来了", "这把稳了"],
    "kill": ["不过如此", "下一个"],
    "death": ["我的我的", "..."],
    "assist": ["好配合"],
    "taunt": {
      "boxes": [
        ["你搁这刮痧呢？", "就这？"],
        ["吾之生涯一片无悔", "这便是我的全部了"]
      ]
    }
  },
  "panels": [
    { "name": "集合", "lines": ["来这里", "集合打团", "别分散", "跟上"] },
    { "name": "撤退", "lines": ["撤！", "别打了快跑"] }
  ]
}
```

- `triggers` — 条件触发台词，每个事件支持多行（随机选一行）
- `taunt.boxes` — 骚话组，先随机选一组，再从组内选台词
- `panels` — 快捷发言分组，F1 面板数字键展开

## ⚙️ 全局设置

配置文件：`%LocalAppData%\FasterChantDevice\settings.json`

```json
{
  "trigger_key": "F1",
  "taunt_key": "F2",
  "burst_mode": true,
  "burst_interval_ms": 1000,
  "taunt_mode": "both",
  "taunt_interval_s": 30,
  "taunt_cooldown_s": 5,
  "game_window_class": "300Heroes",
  "ocr_kda_region": {
    "x_ratio": 0.80, "y_ratio": 0.02,
    "w_ratio": 0.18, "h_ratio": 0.10
  },
  "ocr_broadcast_region": {
    "x_ratio": 0.25, "y_ratio": 0.05,
    "w_ratio": 0.50, "h_ratio": 0.12
  }
}
```

| 参数 | 说明 |
|------|------|
| `burst_mode` | 连发开关 |
| `burst_interval_ms` | 连发间隔（毫秒） |
| `taunt_mode` | 骚话模式：`manual` / `timer` / `both` |
| `taunt_interval_s` | 定时骚话间隔（秒） |
| `taunt_cooldown_s` | 战斗后骚话冷却（秒） |
| `ocr_*_region` | OCR 检测区域（窗口比例，0~1） |
| `debug_mode` | 调试模式开关（`true`/`false`） |
| `debug_log_level` | 日志级别：`Trace` / `Debug` / `Info` / `Warning` / `Error` |

## 🔧 调试模式

## 🔧 调试模式

启用 `debug_mode: true` 后：

- 所有运行事件写入 `%LocalAppData%\FasterChantDevice\debug.log`
- 托盘图标显示 `[DEBUG]` 标识
- `Ctrl+Shift+D` 打开诊断窗口，实时查看：
  - 游戏窗口检测状态（HWND、位置、前台）
  - KDA OCR 原始/解析值
  - 击杀播报文字
  - 事件触发记录
  - 像素变化检测
  - 滚动日志（按级别着色）
- 诊断窗口可一键截图 OCR 区域保存到 `screenshots/` 目录

## 🏗️ 架构

```
单进程多线程
├── UI 线程（WPF）
├── 键盘钩子线程（WH_KEYBOARD_LL，回调 <1ms）
├── OCR 检测线程（游戏中激活，每 500ms）
├── 看门狗线程（500ms 检查钩子存活）
└── 热键备胎（RegisterHotKey 兜底）
```

```
src/FasterChantDevice/
├── Models/              # 数据模型
│   ├── HeroScheme.cs    # 英雄方案（触发器 + 面板）
│   └── AppSettings.cs   # 全局设置
├── Services/            # 核心服务
│   ├── KeyboardHookService.cs  # 全局键盘钩子 + 看门狗
│   ├── GameEventService.cs     # OCR 事件检测主循环
│   ├── OcrEngineService.cs     # Windows.Media.Ocr 封装
│   ├── InputSimulationService.cs # SendInput 模拟按键
│   ├── OverlayService.cs       # 悬浮穿透面板管理
│   ├── OverlayWindow.*         # 悬浮面板 WPF 窗口
│   ├── SchemeManager.cs        # JSON 方案读写
│   ├── DebugLogger.cs          # 调试日志（文件 + 事件）
│   └── DebugService.cs         # 运行时诊断状态
├── ViewModels/          # MVVM
│   └── HeroEditorViewModel.cs
├── Views/               # WPF 窗口
│   ├── HeroEditorWindow.*
│   └── DebugWindow.*           # 调试诊断窗口
└── App.xaml             # 应用入口
```

## ❓ FAQ

**Q: OCR 检测不准，事件不触发？**

- 确认游戏窗口标题**包含**「300英雄」
- 确认游戏在**前台**运行（切到桌面时检测暂停）
- 检查 `ocr_kda_region` 比例是否正确覆盖右上角 K/D/A 区域

**Q: 悬浮面板位置不对？**

高 DPI 显示器已适配，如仍有偏差可调整 `settings.json` 中的窗口定位。

**Q: 如何排查 OCR 不触发的问题？**

1. 设置 `debug_mode: true`
2. 重启程序 → `Ctrl+Shift+D` 打开诊断窗口
3. 进入游戏 → 观察 KDA OCR 读数是否正确
4. 点击 📸 截图按钮，检查截图区域是否覆盖到 KDA 数字
5. 查看 `debug.log` 中的 `[OCR]` 和 `[GameEvent]` 日志

**Q: 会被封号吗？**

本工具仅模拟键盘输入 + 屏幕截图（OCR），不注入游戏进程、不修改游戏内存。使用 Windows 公开 API。但任何第三方工具均有理论风险，请自行评估。

## 📄 许可

MIT License

## 🙏 致谢

- [FasterChantDevice (Anran-233)](https://github.com/Anran-233/FasterChantDevice) — 原始理念
- [Windows.Media.Ocr](https://docs.microsoft.com/en-us/uwp/api/windows.media.ocr) — 内置 OCR 引擎
