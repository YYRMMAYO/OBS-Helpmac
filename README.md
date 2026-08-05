# OBS 排障助手（macOS 版）

OBS 直播排障助手 — 一款离线优先的本地排障工具，覆盖黑屏、卡顿、音画不同步、推流失败、直播间搭建等高频问题的分步解决方案，并支持一键连接 OBS、实时监控、日志分析、配置备份/恢复与场景模板。

本仓库为 **macOS 版**（Tauri v2 + Blazor WebAssembly），功能与 Windows 版对齐（v1.4.x）。Windows 版（WPF 原生）维护在另一个仓库。

## 功能

- 📖 **排障指引 / 知识库**：内置分类问题库与 Markdown 排障指引，完全离线可用
- 🔍 **搜索 / 问答**：按关键词检索问题，离线问答助手
- 🩺 **智能诊断**：读取系统信息（GPU / 系统版本 / OBS 进程）、OBS 日志与配置，本地规则引擎定位问题
- 🎛️ **OBS 控制台**：通过 obs-websocket 5.x 连接 OBS，切换场景、控制录制/推流/虚拟摄像头、音频管理
- 📊 **系统监控**：CPU / 内存 / 网络 / 磁盘 1 秒实时曲线 + OBS 渲染帧率与丢帧
- 🧩 **场景模板**：6 套内置直播间模板，一键落地到 OBS，或导出标准场景集合 JSON
- 📜 **日志分析**：读取 OBS 日志（脱敏 + 31 条特征规则扫描），一键体检配置
- 🗂️ **OBS 配置管理**：备份 / 导出 / 导入（ZIP，含密钥脱敏与 Zip Slip 防护）、轻度 / 彻底重置（永不硬删，可回滚）
- ⚙️ **设置**：AI 诊断引擎（本地离线 / 云端大模型，密钥经系统钥匙串加密保存）、外观无障碍、检查更新

## 技术栈

| 层 | 技术 |
|---|---|
| 桌面壳 | [Tauri v2](https://tauri.app/)（Rust），宿主命令集中在 `src/host.rs` |
| 前端 | Blazor WebAssembly（.NET 10），随包发布，默认零外网请求 |
| OBS 通信 | obs-websocket 5.x（仅本机 127.0.0.1） |
| 机密存储 | macOS 系统钥匙串（Keychain） |

安全模型：宿主命令全部走白名单；读文件限定目录 + canonicalize 防穿越；AI 请求强制 https + SSRF 拦截；推流密钥不出壳；配置导入前强制自动备份。

## 目录结构

```
OBS_Helper.Mac/            # macOS 桌面壳（Tauri v2）
  src-tauri/
    src/host.rs            # 宿主命令白名单（唯一业务模块）
    tauri.conf.json        # 应用配置（frontendDist 指向 Client 发布产物）
    build-mac.sh           # macOS 本地构建脚本（dotnet publish → tauri icon → tauri build）
OBS_Helper.Client/         # 共享前端（Blazor WASM，Mac 版实际使用的 UI）
  Pages/                   # 页面
  Services/                # 服务（宿主桥接 / OBS 控制 / 日志分析 / AI 诊断…）
  wwwroot/data/            # 知识库与场景模板数据
.github/workflows/         # CI：macOS 双架构构建（aarch64 + x86_64）
```

## 构建

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

## 隐私

- 默认不发起任何网络请求：UI、知识库、模板数据全部随包发布
- 仅当在「设置 → AI 诊断引擎」手动开启云端大模型后，才会由桌面壳代发 HTTPS 请求
- API Key 与 OBS 密码经系统钥匙串加密保存，前端不接触密钥原文
- 更新检查仅查询本仓库的 GitHub tags（可手动触发）

## License

[MIT](LICENSE) © 2026 OBS Helper
