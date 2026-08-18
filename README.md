# OBS 排障助手（macOS 版）

> OBS 直播排障助手 —— 一款离线优先的本地排障工具，覆盖黑屏、卡顿、音画不同步、推流失败、直播间搭建等高频问题的分步解决方案，并支持一键连接 OBS、实时监控、日志分析、配置备份/恢复与场景模板。

本仓库为 **macOS 版**（Tauri v2 + Blazor WebAssembly），功能与 Windows 版对齐（**v1.5.0**）。Windows 版（WPF 原生）维护在另一个仓库。

---

## ✨ 功能一览

| 模块 | 说明 |
|---|---|
| 📖 排障知识库 | 内置分类问题库与 Markdown 排障指引，完全离线可用 |
| 🔍 搜索 / 问答 | 按关键词检索问题，离线问答助手 |
| 🩺 智能诊断 | 读取系统信息（GPU / 系统版本 / OBS 进程）、OBS 日志与配置，本地规则引擎定位问题 |
| 🎛️ OBS 控制台 | 通过 obs-websocket 5.x 连接 OBS，切换场景、控制录制 / 推流 / 虚拟摄像头、音频管理 |
| 📊 系统监控 | CPU / 内存 / 网络 / 磁盘 1 秒实时曲线 + OBS 渲染帧率与丢帧 |
| 🧩 场景模板 | 内置直播间模板，一键落地到 OBS，或导出标准场景集合 JSON |
| 📜 日志分析 | 读取 OBS 日志（脱敏 + 特征规则扫描），一键体检配置 |
| 🗂️ 配置管理 | 备份 / 导出 / 导入（ZIP，含密钥脱敏与 Zip Slip 防护）、轻度 / 彻底重置（永不硬删，可回滚） |
| 🖥️ 系统托盘 | 菜单栏图标控制录制 / 推流 / 虚拟摄像头 / 小窗，左键单击显示主窗口 |
| ⌨️ 全局快捷键 | `Ctrl+Alt+R` 录制 · `Ctrl+Alt+S` 推流 · `Ctrl+Alt+C` 虚拟摄像头 · `Ctrl+Alt+M` 小窗 · `Ctrl+Alt+O` 主窗口（主窗口隐藏时同样生效） |
| 🪟 迷你小窗 | 无边框置顶的录制 / 推流 / 虚拟摄像头快捷面板，可拖拽、位置记忆 |
| 🔄 场景自动切换 | 前台切到指定应用时自动切换 OBS 场景（macOS 按应用名匹配，规则可编辑） |
| ⏱️ 定时停止 | 给录制 / 推流设置倒计时，到点自动停止 |
| ⚙️ 设置中心 | AI 诊断引擎、托盘与快捷键、外观无障碍、检查更新 |

---

## 🤖 AI 诊断引擎（重要说明）

Mac 版的 AI 引擎**仅提供两种模式**，且**刻意移除了任何国内免费大模型 API（如智谱 / 质谱 Zhipu 等）**：

- **本地离线（Local）**：规则引擎 + 知识库，零外网、零密钥，开箱即用。
- **云端大模型（Cloud）**：通用 **OpenAI 兼容** 接口（默认 `gpt-4o-mini`）。用户自行填写兼容服务的 `Base URL`（如 OpenAI / OpenRouter / Anthropic 等海外服务）与 API Key。

> 密钥经 **macOS 系统钥匙串（Keychain）** 加密保存，前端不接触密钥原文；请求强制 HTTPS 并启用 SSRF 拦截。未手动开启云端模式时，应用**默认不发起任何网络请求**。

---

## 🛠 技术栈

| 层 | 技术 |
|---|---|
| 桌面壳 | [Tauri v2](https://tauri.app/)（Rust），宿主命令集中在 `src/host.rs`，托盘 / 全局热键 / 小窗 / 单实例见 `src/main.rs` |
| 前端 | Blazor WebAssembly（.NET 10），随包发布，默认零外网请求 |
| OBS 通信 | obs-websocket 5.x（仅本机 127.0.0.1） |
| 机密存储 | macOS 系统钥匙串（Keychain） |

**安全模型**：宿主命令全部走白名单；读文件限定目录 + `canonicalize` 防穿越；AI 请求强制 https + SSRF 拦截；推流密钥不出壳；配置导入前强制自动备份。

---

## 📂 目录结构

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

---

## 📥 下载与安装

1. 前往本仓库 **Releases** 页面，下载对应架构的 `.dmg`（Apple Silicon `aarch64` 或 Intel `x86_64`）。
2. 打开 `.dmg`，将「OBS 排障助手」拖入 `应用程序` 文件夹。
3. 首次运行若提示「无法验证开发者」，请在 **系统设置 → 隐私与安全性** 中点击「仍要打开」。
4. 如需连接 OBS，请在 OBS 中开启 **工具 → WebSocket 服务器设置**，随后在助手「控制台」中填入地址与密码（仅本机 127.0.0.1）。

> GitHub Releases：在仓库主页点击右侧 **Releases**，或访问 `https://github.com/YYRMMAYO/OBS-Helpmac/releases`。

---

## 🔧 构建

### 方式一：GitHub Actions（推荐）

推送 `main` 分支即自动在 `macos-14`（Apple Silicon）与 `macos-13`（Intel）构建机上产出 `.app` 与 `.dmg`，可在 Actions 页面下载产物；也可打 tag 后在 Releases 发布。

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

如需签名/公证：设置 `MAC_SIGN_IDENTITY` 与 `MAC_NOTARY_KEYCHAIN_PROFILE` 后执行 `./build-mac.sh`。

---

## 🔒 隐私

- 默认不发起任何网络请求：UI、知识库、模板数据全部随包发布
- 仅当在「设置 → AI 诊断引擎」手动开启云端大模型后，才会由桌面壳代发 HTTPS 请求
- API Key 与 OBS 密码经系统钥匙串加密保存，前端不接触密钥原文
- 更新检查仅查询本仓库的 GitHub tags（可手动触发）

---

## 📄 License

[MIT](LICENSE) © 2026 OBS Helper
