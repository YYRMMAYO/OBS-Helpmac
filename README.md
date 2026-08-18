<div align="center">

# OBS 排障助手 · macOS 版

**面向直播新手的 OBS Studio 排障工具 —— 纯离线 · 隐私优先 · 原生 macOS 桌面应用**

[![CI](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-mac.yml/badge.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/actions/workflows/build-mac.yml)
[![平台](https://img.shields.io/badge/Platform-macOS_11%2B-000000.svg)]()
[![框架](https://img.shields.io/badge/Framework-Tauri_v2_%2F_.NET_10-ff4154.svg)]()
[![版本](https://img.shields.io/badge/Release-1.5.0-38bdf8.svg)](https://github.com/YYRMMAYO/OBS-Helpmac/releases)
[![离线优先](https://img.shields.io/badge/offline--first-2ea44f.svg)]()
[![许可](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[Windows 版（WPF 原生）](https://github.com/YYRMMAYO/OBS_Helper) · **macOS 版（本仓库）**

</div>

> **这是什么？** 面向直播新手的 OBS Studio 排障工具。知识库、排障指引、日志分析规则全部内嵌在应用里，**不联网也能查、能分析、能控制 OBS**。连上 OBS 后还能远程切换场景、录制与推流，做一键体检。
>
> 本仓库是 **macOS 版**（Tauri v2 + Blazor WebAssembly），功能与 Windows 版（[YYRMMAYO/OBS_Helper](https://github.com/YYRMMAYO/OBS_Helper)）对齐，版本号同步到 **v1.5.0**。

---

## 亮点

| | |
|---|---|
| **知识库完全离线** | 内置分类问题库与 Markdown 排障指引，黑屏 / 卡顿 / 音画不同步 / 推流失败 / 直播间搭建等高频问题分步解决，断网也能用 |
| **本地智能诊断，免费** | 读取系统信息（GPU / 系统版本 / OBS 进程）、OBS 日志与配置，由**本地规则引擎**定位问题，**零费用、纯离线** |
| **远程控制 OBS** | 通过 obs-websocket 5.x 连接 OBS：切换场景、录制 / 推流 / 虚拟摄像头、音频管理、定时停止——菜单栏、全局热键、迷你小窗三种入口随时可用 |
| **全局快捷键** | `Ctrl+Option+R` 录制 · `Ctrl+Option+S` 推流 · `Ctrl+Option+C` 虚拟摄像头 · `Ctrl+Option+M` 小窗 · `Ctrl+Option+O` 显隐主窗口（主窗口隐藏时同样生效），全部可改键或停用 |
| **深度日志分析** | 离线解析 OBS 日志：**脱敏 + 特征规则扫描**，一键体检配置，分析前先脱敏 |
| **场景模板一键落地** | 内置直播间模板，连上 OBS 一键生成场景与来源；未连接时可导出为标准场景集合 JSON |
| **隐私优先** | 默认不发起任何网络请求；UI、知识库、模板数据全部随包发布。API Key 与 OBS 密码经 **macOS 系统钥匙串**加密保存，前端不接触密钥原文 |
| **零外部依赖** | 原生 Tauri v2（Rust）壳 + Blazor WebAssembly 前端，自包含 .app / .dmg，体积小、启动快 |

## 功能

### 学习与排查

- **排障指引 / 知识库** — 内置分类问题库与 Markdown 排障指引，完全离线可用
- **搜索 / 问答** — 按关键词检索问题，离线问答助手，用大白话描述现象自动匹配最可能的问题

### 智能诊断

- **智能诊断** — 读取系统信息、OBS 日志与配置，由**本地规则引擎**定位问题（免费、纯离线）
- **日志分析** — 读取 OBS 日志（脱敏 + 特征规则扫描），一键体检配置

### 控制 OBS

- **OBS 控制台** — 通过 obs-websocket 5.x 连接 OBS：切换场景、元素显隐、音频静音与音量、录制 / 推流 / 虚拟摄像头、实时统计、**定时停止录制 / 推流**
- **菜单栏（系统托盘）** — 菜单栏图标直接控制录制 / 推流 / 虚拟摄像头 / 迷你小窗；左键单击显示主窗口
- **迷你小窗** — 无边框置顶的录制 / 推流 / 虚拟摄像头快捷面板，可拖拽、位置记忆；托盘菜单、控制台或全局热键随时呼出
- **全局快捷键** — 系统级快捷键（默认 `Ctrl+Option+R/S/C/M/O`），全部可在设置页改键或停用
- **场景自动切换** — 按前台应用自动切换 OBS 场景（macOS 按应用名匹配），规则可编辑

### 保持健康 & 快速开播

- **系统监控** — CPU / 内存 / 网络 / 磁盘 1 秒实时曲线 + OBS 渲染帧率与丢帧联动
- **场景模板** — 内置直播间模板，一键落地到 OBS，或导出标准场景集合 JSON
- **OBS 配置管理** — 配置目录检测、备份 / 导出（ZIP，默认脱敏不含推流密钥）、导入（覆盖 / 合并，自动预备份）、轻度 / 彻底重置（永不硬删，可回滚）
- **外观** — 浅色 / 深色 / 跟随系统

## 关于 AI 诊断引擎（macOS 版说明）

macOS 版提供**两类**诊断引擎：

| 引擎 | 原理 | 成本 |
| --- | --- | --- |
| **本地规则引擎**（默认，推荐） | 确定性离线规则匹配，与日志分析同源 | 免费、纯离线 |
| **云端大模型** | OpenAI 兼容接口，接入**你自己的** API Key（经系统钥匙串加密保存在本机） | 你的 API 费用 |

> ⚠️ **与 Windows 版的区别**：Windows 版内置两条免费 AI 通道（智谱 / Pollinations），开箱即用、免注册。macOS 版**不内置任何免费 AI 通道**，仅提供本地规则引擎 + 你自带的云端 Key。这是为了在 Mac 上保持最小依赖与最简发布。需要开箱即用的免费 AI，请使用 Windows 版。

云端失败时**自动回退本地引擎**，并在结果中说明原因。只有在你**主动**在「设置 → AI 诊断引擎」开启云端大模型并填入 Key 后，才会由桌面壳代发 HTTPS 请求。

## 隐私与安全

所有数据只存在本机：

- **偏好**（外观、收藏、步骤进度、连接设置、热键键位、自动切换规则、托盘行为）→ 应用沙盒内的 `prefs.json`——明文 JSON，**均不含任何凭据**
- **OBS 密码与 AI API Key** → **macOS 系统钥匙串（Keychain）**加密保存；前端不接触密钥原文，桌面壳代发请求前做 SSRF 拦截

默认不发起任何网络请求：UI、知识库、模板数据全部随包发布。仅当手动开启云端大模型后才会联网；请求前先脱敏。OBS 配置备份 / 导出默认不含推流密钥（可勾选包含），密码与 Token 自动脱敏。

**安全模型**：宿主命令全部走白名单；读文件限定目录 + `canonicalize` 防穿越；AI 请求强制 HTTPS + SSRF 拦截；推流密钥不出壳；配置导入前强制自动备份；ZIP 导入带 Zip Slip / 压缩炸弹 / 危险扩展名整包拒绝；备份永不硬删（移入回收站，可回滚）。

## 安装与更新

- **GitHub Releases** — 从 [Releases 页面](https://github.com/YYRMMAYO/OBS-Helpmac/releases) 下载 `.dmg`（同时提供 Apple Silicon 与 Intel 双架构），拖入「应用程序」即可
- **应用内更新** — 「检查更新」对比本仓库最新 tag，仅当有更高版本时才提示；检查失败不影响正常使用

> 支持 macOS 11（Big Sur）及以上。需要本机运行 OBS Studio 并通过 obs-websocket 5.x 连接。

## 构建

### 方式一：GitHub Actions（推荐，免本地环境）

推送 `main` 分支即自动在 `macos-14`（Apple Silicon）与 `macos-13`（Intel）构建机上产出 `.app` 与 `.dmg`，可在 Actions 页面下载产物；打 tag 后在 Releases 发布。

### 方式二：macOS 本机构建

前置：Xcode 命令行工具、Rust（stable）、.NET 10 SDK、Node.js。

```bash
# 1) 发布前端（产出 OBS_Helper.Client/bin/Release/net10.0/publish/wwwroot）
dotnet publish OBS_Helper.Client/OBS_Helper.Client.csproj -c Release

# 2) 一键构建（等价 build-mac.sh 流程）
cd OBS_Helper.Mac/src-tauri
npm install --no-save @tauri-apps/cli
npx tauri icon icons/app-icon.png     # 首次构建需生成图标
npx tauri build
```

产物位于 `OBS_Helper.Mac/src-tauri/target/release/bundle/`（`dmg/` 与 `macos/`）。

如需签名 / 公证：设置 `MAC_SIGN_IDENTITY` 与 `MAC_NOTARY_KEYCHAIN_PROFILE` 后执行 `./build-mac.sh`。

## 工程结构

```
OBS_Helper.Mac/            # macOS 桌面壳（Tauri v2）
  src-tauri/
    src/host.rs            # 宿主命令白名单（业务命令 + Shell 控制层）
    src/main.rs            # 窗口 / 托盘 / 全局热键 / 小窗 / 单实例
    tauri.conf.json        # 应用配置（frontendDist 指向 Client 发布产物）
    build-mac.sh           # macOS 本地构建脚本（dotnet publish → tauri icon → tauri build）
OBS_Helper.Client/         # 共享前端（Blazor WASM，Mac 版实际使用的 UI）
  Pages/                   # 页面（含 MiniPanel.razor 小窗）
  Services/                # 服务（宿主桥接 / OBS 控制 / Shell 控制 / 日志分析 / AI 诊断…）
  wwwroot/data/            # 知识库与场景模板数据
.github/workflows/         # CI：macOS 双架构构建（aarch64 + x86_64）+ GitHub Release
```

### 双端宿主协议

前端统一走 `window.obsHelperHost.invoke(cmd, payloadJson)`；macOS 版落在 `src/host.rs` 的 `dispatch()`，命令全集与 Windows 版保持同构（字段名 camelCase、时间戳 Unix 毫秒、目录 / 扩展名白名单、大小上限均一致）。

## 许可

MIT，见 [LICENSE](LICENSE)。© 2026 OBS Helper
