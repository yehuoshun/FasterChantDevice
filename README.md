# 300高速咏唱装置 2.0

> 基于 [FasterChantDevice](https://github.com/Anran-233/FasterChantDevice) 理念重写的 300 英雄快捷喊话工具  
> 技术栈：C# / .NET 8 / WPF / Windows

## 功能

### 主动模式
- **F1** 呼出半透明穿透面板（10 个命名分组，1-0 数字键选择）
- 连发模式：文本框逐行依次发送（间隔可配）
- 非连发模式：随机选一行发送
- 面板支持空闲/最新分组

### 被动模式（条件触发）
- OCR 自动检测击杀/死亡/助攻/开局事件 → 自动发送对应台词
- 骚话：手动 F2 + 定时自动发送（两者可配置）
- 骚话冷却：击杀/死亡/助攻后 N 秒内不触发

### OCR 检测
- 主引擎：K/D/A 计数区（右上角固定 HUD，不受皮肤影响）
- 辅引擎：击杀播报区文字识别
- 兜底：像素变化检测
- 零校准：按窗口比例自动定位

## 运行要求

- Windows 10/11
- .NET 8 Runtime（自包含版本无需安装）

## 开发

```bash
# 构建
dotnet build

# 发布自包含单文件
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 目录结构

```
src/FasterChantDevice/
├── Models/          # 数据模型（HeroScheme, AppSettings）
├── Services/        # 核心服务（键盘钩子, OCR检测, 输入模拟, 方案管理）
├── ViewModels/      # MVVM ViewModels
├── Views/           # WPF 窗口（英雄编辑, 设置）
└── App.xaml         # 入口
```

## 设计文档

详见 [DESIGN.md](DESIGN.md)
