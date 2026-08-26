# OBS-Helpmac（macOS 版）安全与代码审查报告

- 审查范围：`OBS-Helpmac/src/OBS_Helper.MacOS` 全部源码（35 个 .cs、14 个 .axaml、csproj/app.manifest、Assets 结构）。未包含 `_archive`、`bin/obj` 及 Windows 版本。
- 架构概览：Avalonia 11.3.2 + net10.0 单体桌面应用。`App.axaml.cs` 手工组装单例服务容器（AppServices）：
  - 连接层：`ObsWebSocketClient`（obs-websocket 5.x 协议）→ `ObsConnectionService`（状态机/重连）→ `LiveMonitorService`
  - 能力层：`HostBridge`（进程/文件/钥匙串/HTTP 转发的原生实现）
  - 诊断层：`ObsLogAnalyzer` + `LogSanitizer` + `SystemHealthService` + `ObsConfigScanner` → `LocalDiagnosticEngine` / `CloudDiagnosticEngine` / `DiagnosticOrchestrator`
  - 业务层：`SceneTemplateService`、`ObsConfigService`（备份/导入/重置）、知识库三件套
- 总体评价：架构清晰、职责分层良好；鉴权算法实现正确；"机密不入 KV 存储""日志先脱敏再上云""写操作需确认"等安全设计意图明确。但**钥匙串通道实现存在真实漏洞**，另有 1 类会导致页面崩溃的 XAML 语法错误和多处健壮性问题。

---

## 一、高危（安全漏洞）

### H1. 钥匙串机密走命令行参数：明文暴露 + 转义方案失效 + 参数注入
位置：`Services/Host/HostBridge.cs:244-300`（`SetSecretAsync` / `GetSecretAsync` / `DeleteSecretAsync`）

```csharp
var escaped = value.Replace("'", @"'\''");
Run("/usr/bin/security", $"add-generic-password -U -s OBS_Helper -a '{key}' -w '{escaped}'");
```

三个叠加问题：

1. **进程列表泄露**：OBS 密码 / LLM API Key 以 argv 明文出现在 `-w '...'` 中，运行期间本机任意用户/进程可通过 `ps` 或活动监视器读到。
2. **shell 式转义在无 shell 场景下无效**：`ProcessStartInfo(UseShellExecute=false)` 不经过 shell，.NET 的参数解析只识别双引号与反斜杠，单引号是普通字符。因此：
   - 存储的 account 名实际是 `'obs_websocket_password'`（带引号字面量），读写两侧恰好一致所以"看起来能用"，实则污染数据；
   - 含单引号的密码会被写成带 `'\''` 序列的错误值 → **密码功能损坏**（连接 OBS 失败）；
   - 含双引号或空格的密码会被解析器错误切分，密码片段可能变成 `security` 的独立参数 → **参数注入**（例如把 `-w` 之后的值挪作他用）。
3. `GetSecretAsync` 返回值 `TrimEnd('\n')` 无法区分"密码本身以换行结尾"的边界情况（次要）。

**修复建议**：改调 `security add-generic-password ... -w`（不带值）后经 **stdin** 写入密文；或封装 Security.framework / 使用成熟的 keychain 库（如 `nsec.keychain`、`SecurityService` P/Invoke）。绝不让机密进 argv。

### H2. 路径校验使用无分隔符的 StartsWith 前缀匹配：可被同级目录名绕过
位置：`HostBridge.cs:359`（ReadObsLogAsync）、`:479`（ListObsConfigAsync）、`:516`（ReadObsConfigAsync）、`:684`（ImportObsConfigAsync zip 解压目标）

```csharp
if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
```

root = `~/Library/Application Support/obs-studio` 时，`~/Library/Application Support/obs-studio-backup/secrets.txt` 也能通过校验。后果：
- `ReadObsConfigAsync` / `ReadObsLogAsync` 可越界读取同级目录任意 `.txt/.log/.ini/.json`；
- `ImportObsConfigAsync` 的 zip 条目解包目标可落到同级目录（越界写）。

**修复建议**：
```csharp
var rel = Path.GetRelativePath(root, full);
if (rel.StartsWith("..") || Path.IsPathRooted(rel)) return null;
```

### H3. 云端 AI 转发缺少 SSRF/主机约束，与代码注释声明不符
位置：`HostBridge.AiChatAsync (:532-549)`、`CloudDiagnosticEngine.cs:15`

`AiChatAsync` 仅强制 `https`，不限制目标主机；而 API Key 以 Bearer 头附加到该请求。只要诱导用户把「AI 设置」中的接口地址改成任意 https 端点（钓鱼文档、伪造教程），Key 就会被发送过去。`CloudDiagnosticEngine.cs:15` 注释声称"宿主侧强制 https-only 且做了 SSRF 拦截"，实际并不存在拦截逻辑——注释与实现不一致本身就是审计陷阱。

**修复建议**：增加可选的主机白名单/黑名单（至少拒绝明显内网域与 IP 段的 https 主机），并修正注释。

---

## 二、中危

### M1.【语法错误，必现崩溃】XAML 重复属性元素 `<ItemsControl.ItemsPanel>`
以下 5 个页面的 ItemsControl 内 **同一属性元素写了两次**，Avalonia XAML 解析器会在导航到该页时抛 `XamlParseException`（"property already set"），页面直接不可用：

| 文件 | 行号 |
|---|---|
| `Views/ToolboxPage.axaml` | 50 |
| `Views/SearchPage.axaml`（StepList） | 59 |
| `Views/MonitorPage.axaml` | 32 |
| `Views/DiagnosticPage.axaml` | 21 |
| `Views/GuidePage.axaml` | 9 |

形如：
```xml
<ItemsControl x:Name="...">
  <ItemsControl.ItemsPanel>...</ItemsControl.ItemsPanel>
  <ItemsControl.ItemsPanel>...</ItemsControl.ItemsPanel>   <!-- 重复 -->
```
疑为批量替换脚本事故。删除其中一个即可修复。**这 5 个页面目前应全部无法打开，建议优先验证。**

### M2. 机密经钥匙串 CLI 读写的健壮性问题连带
见 H1。补充：`Run()`（`HostBridge.cs:206-227`）设置了 `RedirectStandardError=true` 却从不读取 stderr——当 `security` 输出较多错误信息时管道缓冲区写满会导致子进程死锁，直到 8 秒超时被 Kill，所有钥匙串操作表现为随机 8 秒卡顿后失败。应改为异步同时排空 stdout/stderr。

### M3. WebSocket 握手超时路径泄漏连接
位置：`Services/Obs/ObsWebSocketClient.cs:76-86`
`ConnectAsync` 在 15 秒超时时抛出异常，但不调用 `DisposeSocketAsync()`，TCP/WebSocket 连接与接收循环保持存活，直到下一次连接/断开才回收。反复超时（如端口被防火墙 DROP）会累积半开连接与后台任务。

### M4. 重连定时器 CTS 生命周期竞争
位置：`Services/Obs/ObsConnectionService.cs:144-190`
`ScheduleReconnect` 开头 `CancelReconnect()` 会 `Dispose()` 上一个 CTS，而上一轮的后台任务可能仍在 `Task.Delay(1000, token)` 中使用该 token → 可能抛 `ObjectDisposedException` 被 catch(OperationCanceledException) 漏掉而成为未观察异常。

### M5. store.json 非原子写入，损坏即全部静默丢失
位置：`Infrastructure/KeyValueStore.cs:62-72`
`File.WriteAllText` 直接覆盖目标文件；进程在写入中途崩溃/断电会产生半个 JSON，下次启动 catch 后 `_map = new()` ——主题、OBS 地址、收藏、步骤进度**全部静默清零**。应写临时文件再 `File.Move(overwrite:true)`。

### M6. LiveMonitorService 共享集合无并发保护
位置：`Services/Obs/LiveMonitorService.cs:79-81, 103-109`
`_alerts` / `_samples` 由后台轮询线程每 2 秒修改，UI 定时器（MonitorPage 每 1 秒）通过 `mon.Alerts.Take(20)` 直接枚举同一 List，无锁快照 → 可能抛 `InvalidOperationException`（集合已修改）或读到撕裂状态。

### M7. 日志脱敏盲区（隐私外泄面）
位置：`Services/Obs/LogSanitizer.cs`
脱敏后的日志全文会被发送到云端 AI（`BuildUserPrompt`），因此以下盲区属于实际外泄路径：
1. **IPv6 地址完全不处理**——公网 IPv6（常见于 macOS 网络日志）原样上传；
2. `LongToken` 白名单按**子串**匹配（`token.Contains("Intel"/"AMD"/"Microsoft")`），一个包含这些词的 24+ 位真实密钥不会被抹除；
3. `StreamUrl` 保留主机名，推流平台分配的**主机名本身**有时含用户/房间标识（如 `xxx-12345.live.xxx.com`），属低概率残留。

### M8. 非 macOS 回退分支将机密明文落盘
位置：`HostBridge.cs:255`（`_store.Set("secret:" + key, value)`）
与类注释"机密不进明文存储"矛盾。当前产物面向 macOS 影响有限，但同一份代码若在 Linux/Windows 运行即成事实上的明文存储。建议改为显式拒绝而非静默降级。

---

## 三、低危 / 功能缺陷 / 代码质量

| # | 位置 | 问题 |
|---|---|---|
| L1 | `ToolboxPage.axaml.cs:41` | `OnAnalyzeLog` 开头把 `AnalyzeBtn.IsEnabled` 设为 `true`（应为 `false`），分析期间按钮未禁用，可重复点击 |
| L2 | `ConsolePage.axaml.cs:103-111` | 连接前就持久化 Host/Port（失败配置也入库）；连接失败时 `SetPasswordAsync(null, remember:true)` 会顺手删掉已保存的正确密码 |
| L3 | `HostBridge.CheckAppUpdateAsync:817` | 取 GitHub tags[0] 当"最新版"，tags 不保证按版本排序，更新检查结果可能错误 |
| L4 | `App.axaml.cs:35` | `_ = Services.InitializeAsync()` 吞掉所有异常：自动连接失败无任何日志/UI 提示；项目整体缺少日志设施，所有 `catch {}` 均不可观测 |
| L5 | `SettingsPage.axaml.cs:30-33` | 每次 Loaded 都重复订阅 CheckBox 事件（页面缓存但 Loaded 可多次触发），保存动作被多次执行 |
| L6 | `ObsSettingsService.BuildUrl:28` | 固定 `ws://`。Host 允许任意主机名/IP，非回环目标时控制通道明文传输，LAN 内可被嗅探/劫持（握手是挑战-响应，密码本体不泄露，但会话可被接管）。建议非回环地址时给出警告 |
| L7 | `ObsConfigService.ResetFullAsync` / `HostBridge.ResetObsConfigFullAsync:731` | 未检查 OBS 是否正在运行即 `Directory.Move` 整个配置目录；OBS 运行中重置会产生新旧状态混杂。UI 有二次确认但无运行检测（备份/导入同理只有文字提醒） |
| L8 | `HostBridge.ImportObsConfigAsync:686` | overwrite 模式无条件覆盖 obs-studio 目录内同名文件，zip 内容未经任何 schema 校验即落盘（结合 H2 可越界写） |
| L9 | `MonitorPage.axaml.cs:22-31` | `System.Timers.Timer` 从不 Dispose（页面销毁后泄漏）；`(LogSeverity)a.Severity` 冗余强转 |
| L10 | `SceneTemplateService.ApplyAsync:131` | `SetCurrentSceneTransitionDuration` 可能收到 ≤0 的毫秒值（模板数据缺省时） |
| L11 | `GuidePage.axaml:36` | 文案病句且危险："先删除 ~/Library/Application Support/obs-studio 之外不影响媒体文件"——表述不通，且照字面理解可能诱导用户误删场景集合 |
| L12 | `Errors/ErrorCodes.cs` | 全部为 Windows/WebView2 文案，macOS 版未引用，纯遗留死代码且描述误导 |
| L13 | `Services/Markdown/MarkdownRenderer.cs` | 全项目无引用（死代码）；且其输出 HTML 在 Avalonia 中没有消费方 |
| L14 | `HostBridge.cs:854-897` | 托盘/小窗/前台应用等一批 no-op 接口（`ToggleMiniWindowAsync`、`GetForegroundAppAsync` 等）无调用方，属从 Web/Windows 版搬运的兼容残留 |
| L15 | `HostBridge.cs:676,739` | `PackObsConfigAsync(null!, ...)` 用 `null!` 绕过可空注解；`ImportObsConfigAsync` 内同步阻塞 `.GetAwaiter().GetResult()`（当前实现无害，模式危险） |
| L16 | `csproj` | `BuiltInComInteropSupport=true` 对 Avalonia/macOS 非必要；TargetFramework 为 net10.0（预览期框架），对外发布需评估运行时可用性 |
| L17 | `ObsWebSocketClient.ConnectAsync:80` | `Task.WhenAny(identified, Task.Delay(Timeout.Infinite, linked.Token))` 每次调用泄漏一个无限时器任务（轻微） |

---

## 四、做得好的部分（无需修改）

- `ObsAuth`：obs-websocket 5.x 鉴权算法实现与规范一致，纯函数可测。
- `ObsConnectionSettings` 刻意不含密码字段；`Sanitize` 收敛了非法主机/端口，阻断了 URL 注入。
- 「先脱敏再分析/上传」的顺序设计正确（`ObsLogAnalyzer` 逐行 Sanitize 后才进入规则匹配和云端 prompt）。
- `OpenExternalAsync` 校验 http/https 才放行系统浏览器；`ExportTemplateAsync` 用 `Path.GetFileName` 截断路径。
- 云端引擎的工具集全部只读（快照/知识库检索），模型无法触达写操作；密钥仅以"键名"流转的设计合理。
- zip 解压对 `../` 经 `GetFullPath` 归一化后有基本防护（仅差 H2 的兄弟目录补丁）。

---

## 五、修复优先级建议

1. **P0**：M1（5 个页面必崩的 XAML 重复节点）——一行删除即可恢复功能。
2. **P0**：H1（钥匙串通道重构：stdin 写入 / 去 shell 式转义 / 排空 stderr 即 M2 一并解决）。
3. **P1**：H2（路径校验改为 GetRelativePath 方案）、M3/M4/M5/M6（稳定性）、H3（SSRF 约束 + 修正注释）。
4. **P2**：M7（IPv6 脱敏、白名单改全词匹配）、L1-L11。
5. **P3**：清理 L12-L17 死代码与遗留文案。
