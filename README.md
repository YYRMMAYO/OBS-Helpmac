<div align="center">

<img src="src/OBS_Helper.MacOS/Assets/appicon.png" width="88" alt="OBS 排障助手图标">

# OBS 排障助手 · macOS 版

**面向直播新手的 OBS Studio 排障工具**
**纯离线 · 隐私优先 · Avalonia 原生桌面应用**

[![Build macOS](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-macos.yml/badge.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-macos.yml)
[![平台](https://img.shields.io/badge/Platform-macOS_12%2B-000000.svg)]()
[![框架](https://img.shields.io/badge/Framework-Avalonia_11_%2F_.NET_10-7c3aed.svg)]()
[![版本](https://img.shields.io/badge/Release-2.0.0-38bdf8.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/releases)
[![离线优先](https://img.shields.io/badge/offline--first-2ea44f.svg)]()
[![许可](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[Windows 版（WPF）](https://github.com/YYRMMAYO/OBS_Helper) · **macOS 版（本仓库）**

[![下载 macOS 版](https://img.shields.io/badge/⬇_下载-v2.0.0_DMG-007AFF.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/releases/latest)

</div>

---

## 这是什么？

**OBS 排障助手**帮助直播新手解决 OBS Studio 的常见问题：黑屏、卡顿、音画不同步、推流失败、直播间搭建……

- **不联网也能用**：212+ 条分步排障方案、日志分析规则、场景模板全部内嵌在应用里
- **连上 OBS 更强**：通过 obs-websocket 远程切换场景、录制 / 推流 / 虚拟摄像头、实时监控预警
- **隐私优先**：默认零网络请求；密码与 API Key 存入 **macOS 系统钥匙串**，绝不明文落盘

> v2.0.0 起采用 **Avalonia UI 单进程原生架构**（替代早期 Tauri + Web 方案），功能与 Windows WPF 版完全对齐。

---

## 功能总览

### 学习与排障

| 功能 | 说明 |
|---|---|
| **问题库** | 9 大分类 · 212+ 条分步解决方案，每条含典型症状 / 成因分析 / 操作步骤 / 官方链接 |
| **全文搜索** | 关键词检索症状、原因与步骤；支持分类筛选 |
| **收藏与进度** | 收藏常用方案，解决步骤可标记完成进度 |
| **搭建清单** | 从安装 OBS 到正式开播的 9 步清单（按 macOS 权限模型重写），进度本地保存 |

### 智能诊断

| 功能 | 说明 |
|---|---|
| **一键诊断** | 环境扫描（OBS 进程 / 磁盘 / 系统）+ 配置体检 + 本地规则引擎归因，全程离线 |
| **AI 助手** | 用大白话描述现象，自动匹配最可能的问题并给出步骤；本地引擎默认，可选接入 OpenAI 兼容云端大模型 |
| **日志分析** | 读取 OBS 日志 → 先脱敏 → 特征规则扫描（丢帧、编码器回退、编码过载等） |

### 直播控制

| 功能 | 说明 |
|---|---|
| **控制台** | 连接 obs-websocket 5.x：切换场景、开始 / 停止录制、推流、虚拟摄像头 |
| **实时监控** | 输出帧率、帧渲染耗时、渲染跳帧率、网络拥塞、磁盘剩余空间；异常自动预警并关联排障方案 |
| **场景模板** | 内置直播间布局模板：已连接 OBS 一键在线落地（先自动备份）；未连接时导出标准场景集合 JSON |
| **工具箱** | 日志目录直达、最新日志一键分析、配置打包备份、从备份导入、彻底重置（先备份再移出，永不硬删） |

### macOS 专属适配

- 机密存储走系统钥匙串（`security` CLI）
- 插件页按 `~/Library/Application Support/obs-studio/plugins` 适配，含 Gatekeeper / Apple Silicon 兼容提示
- 搭建指引覆盖屏幕录制 / 麦克风权限授予、VideoToolbox 硬件编码等 macOS 特有话题
- 在 Finder 中显示配置目录、导出到「下载」文件夹

---

## 设计语言

参考 Apple HIG 的清新风格：

- `#F5F5F7` 窗体底色 + 白色卡片 + 发丝线描边
- systemBlue 强调色，浅色 / 深色主题随系统
- SF 风格字体阶梯与半透明侧栏、圆角胶囊导航
- 全部图标为矢量线条绘制——无位图、无 emoji

---

## 与 Windows 版功能对照

| 功能 | Windows (WPF) | macOS (Avalonia) |
|---|:---:|:---:|
| 问题库 / 搜索 / 收藏 | ✅ | ✅ |
| AI 助手（本地 + 云端） | ✅ | ✅ |
| 一键诊断 / 日志分析 | ✅ | ✅ |
| 场景模板落地 / 导出 | ✅ | ✅ |
| 插件管理 | ✅ | ✅（目录按 macOS 适配） |
| 工具箱（备份 / 重置） | ✅ | ✅ |
| 控制台远程操作 | ✅ | ✅ |
| 实时监控预警 | ✅ | ✅ |
| 搭建清单 / 指引 | ✅ | ✅（内容按 macOS 重写） |
| 机密加密存储 | DPAPI | 系统钥匙串 |
| 托盘常驻 / 迷你小窗 / 全局热键 | ✅ | 规划中（接口已预留） |

---

## 下载与安装

1. 前往 [Releases](https://github.com/YYRMMAYO/OBS-Helpmac/releases/latest)，按芯片下载：
   - **Apple Silicon**（M1 / M2 / M3 / M4）：`OBS_Helper-x.y.z-macOS-arm64.dmg`
   - **Intel**：`OBS_Helper-x.y.z-macOS-x64.dmg`
2. 打开 DMG，将应用拖入「应用程序」文件夹
3. 首次打开若提示无法验证开发者：右键点击应用 → 「打开」，或到 **系统设置 → 隐私与安全性** 点击「仍要打开」

> 当前为 ad-hoc 签名。

---

## 从源码构建

```bash
git clone https://github.com/YYRMMAYO/OBS-Helpmac.git
cd OBS-Helpmac

# 需要 .NET 10 SDK
dotnet publish src/OBS_Helper.MacOS/OBS_Helper.MacOS.csproj \
  -c Release -r osx-arm64 --self-contained

# 产物位于
# src/OBS_Helper.MacOS/bin/Release/net10.0/osx-arm64/publish/
```

推送或打 `v*` 标签后，GitHub Actions 会自动完成：

- `osx-arm64` / `osx-x64` 双架构自包含发布
- 组装 `.app` bundle（Info.plist + icns 图标 + ad-hoc 签名）
- `hdiutil` 打包 DMG 并附加到 GitHub Release

---

## 项目结构

```
OBS-Helpmac/
├── src/OBS_Helper.MacOS/          # Avalonia 应用主工程
│   ├── Assets/                    # 内嵌资源（问题库 / 场景模板 JSON、图标）
│   ├── Infrastructure/            # 键值持久化（替代 localStorage）
│   ├── Models/                    # 领域模型（Problem / Obs 协议 / 场景模板）
│   ├── Services/                  # 业务服务层（与 Windows 版同源）
│   │   ├── Ai/                    # 本地 / 云端诊断引擎与编排器
│   │   ├── Host/                  # HostBridge 桌面能力原生实现
│   │   ├── Obs/                   # obs-websocket 客户端 / 连接 / 监控 / 日志分析
│   │   └── ObsConfig/             # OBS 配置管理 / 场景模板服务
│   ├── Views/                     # 主窗口 + 12 个功能页
│   ├── App.axaml                  # 清新 macOS 设计系统（颜色 / 字体 / 控件样式）
│   └── Program.cs
├── packaging/                     # macOS 打包物料（Info.plist / AppIcon.icns）
├── .github/workflows/build-macos.yml
├── _archive/                      # 归档：旧 Tauri + Blazor 迁移方案（仅参考）
└── README.md
```

---

## 隐私承诺

| 数据 | 存放位置 | 是否联网 |
|---|---|---|
| 问题库 / 模板 / 指引 | 应用内嵌资源 | 否 |
| 收藏 / 步骤进度 / 外观偏好 | `~/Library/Application Support/OBS_Helper/store.json` | 否 |
| OBS 密码 / AI API Key | **macOS 系统钥匙串** | 否 |
| 云端 AI 请求 | — | 仅在你主动切换云端引擎并保存 Key 后发起，且强制 https |

---

## 路线图

- [ ] 菜单栏托盘常驻 + 全局热键（接口已预留）
- [ ] 迷你控制小窗
- [ ] Developer ID 签名与公证
- [ ] 场景自动切换（前台应用感知）

---

## 反馈与贡献

欢迎提交 [Issue](https://github.com/YYRMMAYO/OBS-Helpmac/issues) 反馈问题或建议。

## 许可

[MIT](LICENSE) © 2026 OBS Helper
