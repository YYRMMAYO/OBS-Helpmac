<div align="center">

# OBS 排障助手 · macOS 版

**面向直播新手的 OBS Studio 排障工具 — 纯离线 · 隐私优先 · Avalonia 原生桌面应用**

[![Build macOS](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-macos.yml/badge.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-macos.yml)
[![平台](https://img.shields.io/badge/Platform-macOS_12%2B-000000.svg)]()
[![框架](https://img.shields.io/badge/Framework-Avalonia_11_%2F_.NET_10-7c3aed.svg)]()
[![版本](https://img.shields.io/badge/Release-2.0.0-38bdf8.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/releases)
[![离线优先](https://img.shields.io/badge/offline--first-2ea44f.svg)]()
[![许可](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[Windows 版（WPF）](https://github.com/YYRMMAYO/OBS_Helper) · **macOS 版（本仓库）**

</div>

> **这是什么？** 面向直播新手的 OBS Studio 排障工具。知识库、排障指引、日志分析规则全部内嵌在应用里，**不联网也能查、能分析、能诊断**。连接 OBS 后还能远程切换场景、录制与推流。
>
> 本仓库是 **macOS 版（Avalonia UI + .NET 10）**，功能与 Windows 版（WPF）对齐，版本号 **v2.0.0**。

---

## 架构（v2.0.0 起为 Avalonia 单进程原生方案）

| 层 | 说明 |
|---|---|
| `src/OBS_Helper.MacOS` | Avalonia 11 应用：清新 macOS 风格设计系统 + 12 个功能页 |
| `src/OBS_Helper.MacOS/Services` | 与 Windows 版同源的 C# 服务层（问题库 / 日志分析 / obs-websocket / AI 引擎），已去除 Web 依赖 |
| `src/OBS_Helper.MacOS/Services/Host/HostBridge.cs` | 桌面能力原生实现：钥匙串机密存储、OBS 目录定位、配置备份/导入导出、AI 请求转发 |
| `packaging` | macOS 打包物料（Info.plist、AppIcon.icns），由 GitHub Actions 组装 .app 与 DMG |
| `_archive` | 归档的旧迁移方案（Tauri v2 + Blazor WASM），仅作参考 |

## 功能对齐（vs Windows WPF 版）

| 功能 | Windows | macOS (Avalonia) |
|---|---|---|
| 问题库（212+ 条分步方案） | 支持 | 支持（内嵌资源离线加载） |
| 全文搜索 + 分类浏览 + 收藏/进度 | 支持 | 支持 |
| AI 助手（本地引擎默认，云端可选） | 支持 | 支持 |
| 一键诊断（环境扫描 + 本地规则引擎） | 支持 | 支持 |
| 场景模板在线落地 / 导出 JSON | 支持 | 支持 |
| 插件管理 | 支持 | 支持（目录按 macOS 适配） |
| 工具箱（日志分析 / 备份 / 重置） | 支持 | 支持 |
| 控制台（场景切换 / 录制 / 推流 / 虚拟摄像头） | 支持 | 支持 |
| 实时监控（帧率 / 丢帧 / 磁盘预警） | 支持 | 支持 |
| 搭建清单 / 指引 | 支持 | 支持（步骤按 macOS 重写） |
| 设置（主题 / 连接 / AI 引擎 / 更新检查） | 支持 | 支持 |
| 机密存储 | DPAPI | **macOS 系统钥匙串**（security CLI） |
| 托盘常驻 / 迷你小窗 / 全局热键 | 支持 | 规划中（接口已预留） |

## 设计语言

参考 Apple HIG 的清新风格：`#F5F5F7` 窗体底色、白色卡片 + 发丝线描边、systemBlue 强调色、SF 风格字体阶梯、半透明侧栏与圆角胶囊导航；支持浅色 / 深色主题。全部图标为矢量线条（无位图、无 emoji）。

## 构建

### 本地（macOS）

```bash
dotnet publish src/OBS_Helper.MacOS/OBS_Helper.MacOS.csproj -c Release -r osx-arm64 --self-contained
# 产物在 src/OBS_Helper.MacOS/bin/Release/net10.0/osx-arm64/publish/
```

### CI（GitHub Actions）

推送或打 tag 后自动构建 `osx-arm64` 与 `osx-x64` 双架构：

- 组装 `.app` bundle（Info.plist + icns 图标 + ad-hoc 签名）
- `hdiutil` 生成 DMG
- tag 推送（`v*`）时自动附加到 GitHub Release

## 隐私

默认不发起任何网络请求；UI、问题库、模板数据全部随应用分发。API Key 与 OBS 密码保存在 **macOS 系统钥匙串**，绝不明文落盘。

## 许可

MIT — 见 [LICENSE](LICENSE)。
