# RTD_CLIENT 客户端独立维护项目

从项目内原 APK 重建兼容客户端，保留原生输入修复、既有兼容链、restore-cache 与启动公告，提供显示层翻译和可选位图中文字体入口。本项目不安装 APK，不控制模拟器，不启动或修改服务端。

## 快速构建

1. 安装 Python 3.10+、JDK 17+、Android SDK Build Tools（验证环境为 36.1.0）与 .NET SDK 10；本地副本已带 ignored 的 tools/dotnet 和 tools/cecil。
2. 检查根目录 `rtd.config.json`。所有相对路径基于项目根目录；工具可写绝对路径。Python 留空从 PATH 自动发现；Android SDK/JDK 留空从 ANDROID_HOME/JAVA_HOME 自动发现；空 JDK 配置也支持从 PATH 的 java.exe 定位 JDK 根目录（必须同时有 keytool.exe）。当前机器也可在 tools.python 填 Python310 的实际安装路径。
3. 在 PowerShell 执行 `./build.ps1`，或用指定 Python 执行 `python ./scripts/build.py`。
4. 结果为 `dist/rtd-client.apk`，报告为 `dist/build-report.json`，地址审计为 `dist/endpoint-audit.json`。脚本实际执行 zipalign、创建本地开发证书、apksigner 签名和验证；失败立即停止。

默认 APK 输入为 `inputs/original/game.apk`，原件 SHA256 为 `1e95f5a91c6d3ead09c7f5462ace6238deda87392621f07758c5dc7439d784ec`。输入、完整 UnityCache、工具二进制、生成字体、密钥和产物均留在本机且被 Git 忽略。迁移时请复制这些 ignored 目录，单独 git clone 不会拥有原游戏资源。无需任何历史候选 APK。

## 目录

| 路径 | 内容 |
| --- | --- |
| src | 可维护 C# 插件、Cecil 补丁器与测试 |
| scripts | Python 构建、原生输入算法、字体工具 |
| inputs/original | 原始 APK，必须自行保留，不进 Git |
| inputs/localization | UTF-8 翻译 JSON；可选字体 PNG/TSV |
| resources/UnityCache | 完整原始热更新缓存，打包内置并在首次启动释放 |
| build | 从原 APK 提取的程序集、编译中间文件、开发 keystore |
| dist | 签名 APK、构建报告 |
| tools | 本机工具/Mono.Cecil 二进制，不进 Git |
| diagnostics/event-points | 保持游戏主程序集不变的活动积分只读观察包 |
| diagnostics/settlement | 独立通关阶段诊断候选，不属于默认 APK 构建 |
| docs/protocol | 已脱敏的协议、事务和网络边界归档 |

详见 [部署与开发](docs/开发部署.md)、[翻译与字体](docs/翻译字体.md)。仓库的初始化翻译示例为 `{}`，不代表本机部署词条为空；当前翻译与字体状态见下方简体版打包说明，仍不等于全量运行时文字及设备显示验收。

默认构建已包含 Java JNI 桥接、桌面 Unity 入口、真实 Unity/Mono 库直载，以及本地模式跳过旧 Google 内购启动检查。游戏 API、调试环境地址与备用下载地址统一使用根配置；网络日志使用 `RTD_NET` 前缀，覆盖实际请求创建、完成、等待和异常。红米 K80 / Android 16 已进入游戏并完成注册、鉴权和登录通信，详见 [Android 16 启动修复](docs/Android16启动修复.md)。

历史实现依据见 [JNI 桥接与崩溃排查](docs/JNI桥接与崩溃排查.md)、[广告插件启动崩溃](docs/广告插件启动崩溃.md)，其中迁移时的构建状态由上述最新记录补充。

部署翻译映射 `inputs/localization/translation.json` 被Git忽略，正式词条由RTD_TRANSLATION项目统一维护。首次构建若文件缺失，自动从 `examples/translation.example.json` 初始化空对象；之后把翻译项目导出文件复制到该路径即可。

## APK 日志等级与设备文件

`rtd.config.json` 的 `logging.level` 控制重新打包后的托管 Unity 日志：`debug`（默认，全部）、`info`（普通日志及以上）、`warning`、`error`、`none`。当前 Unity Debug.Log 没有独立 debug 级别，因此 debug 与 info 输出相同。过滤覆盖游戏主程序集、firstpass 和三个 RTD 辅助程序集；不控制 Android 系统、Java、Unity 原生引擎日志。不生成日志文件，使用 adb logcat 查看。修改后运行 `./build.ps1` 并安装新 APK；构建报告记录 logging_level。

应用内部文件路径为 `/data/data/jp.co.acquire.RTD/files/`（通常对应 `/data/user/0/jp.co.acquire.RTD/files/`），不是 `/data/jp.co.acquire.RTD`。普通文件管理器受 Android 沙箱限制，无法访问。安装本身也不会把所有 APK 资源复制到该目录；文件通常在首次运行及下载后产生。翻译等内置资源保留在 APK 内；本仓库 resources/UnityCache 现随 APK 打包，启动时释放到应用缓存目录。

可尝试 `adb shell run-as jp.co.acquire.RTD ls -la files`。只有 debuggable APK 才支持 run-as；若返回 package not debuggable，需使用可调试构建，或在已 root 的测试设备使用 `adb shell su -c 'ls -la /data/data/jp.co.acquire.RTD/files'`。普通存储权限不会开放私有目录。导出单个文件可用 `cmd.exe /c "adb exec-out run-as jp.co.acquire.RTD cat files/文件名 > 导出文件名"`，避免旧版 PowerShell 重定向破坏二进制文件。外部应用目录也可检查 `/sdcard/Android/data/jp.co.acquire.RTD/files/`；Unity persistentDataPath 的实际位置取决于引擎与设备，不能仅凭内部目录为空判断保存失败。

## 内置下载资源

构建必须提供非空 `paths.resourceCache`（默认 `resources/UnityCache`），将 Shared 下全部文件加入 APK 的 assets/rtd-cache，构建末尾逐文件对比打包内容。生成的索引记录相对路径、字节数和 SHA256；构建报告记录资源数量和总字节数。

AssetBundleLoader/TitleMenu/Installer 的 Awake 在原始逻辑前调用 RTDEmbeddedCache.Ensure，通过 Unity Application.persistentDataPath 定位 UnityCache，使用 Java 文件通道分块释放。不会复制用户存档。每个文件完成后写入以资源 SHA256 命名的完成标记，后续启动依据标记和长度跳过；中断无标记的文件重新释放。释放异常会抛出，不把未完成的释放标记成成功。当前实现不对已释放文件每次重新计算哈希；需要强制恢复相同长度的损坏文件时删除对应 .rtd-* 标记。

首次释放同步执行，需要额外约 283 MB 可用空间。释放缓存时刷新 __info 第二行的最后使用 Unix 时间戳，避免原备份的 2014 年时间被 Unity 缓存过期逻辑淘汰；之后启动保留已释放载荷并刷新元数据。系统清理缓存后会重新释放。登录、公告及游戏 API 仍访问服务器，服务器以后新增的资源不属于现有内置资源范围。

2026-10-31 已在连接手机验证：清除全部 180 个已知资源文件及完成标记后，只从 APK 重新释放，76 个启动资源全部通过 Caching.IsVersionCached（0 miss），唯一的 LoadFromCacheOrDownload 调用也命中缓存。验证时开启未命中即抛错的保护，阻止网络回退；普通重启再次 76/76 命中，复制 0 文件。原日志 Download Start_0 是原始程序对首个音频资源的固定分支名称，不代表网络下载。详见 docs/内置资源免下载验证.md。

调试维护：在内部 files/ 创建 rtd-reseed-cache 标记，可在下次启动前删除全部已知缓存条目并重新释放（保留账号）；成功后标记自动删除。创建 rtd-assert-cache 可禁止缓存未命中时退回网络下载，验证后手动删除。两个标记默认均不存在。验证日志可用 python scripts/verify_cache_runtime.py build/cache-reseed-runtime.txt build/cache-restart-runtime.txt 检查。

APK 调试：rtd.config.json 的 android.debuggable 当前为 true，重新打包后允许 adb shell run-as jp.co.acquire.RTD 访问应用私有目录；设为 false 可关闭。需要安装新 APK 后才生效。

## 日文与繁体中文构建

`./build.ps1 -Language ja` 构建无翻译、无替换字体的日文包 `dist/rtd-client-ja.apk`（默认）。`./build.ps1 -Language zh-TW` 构建繁中包 `dist/rtd-client-zh-TW.apk`。Python 等价参数为 `--language ja` 或 `--language zh-TW`。报告按语言保存为 build-report-ja.json / build-report-zh-TW.json。

早期繁中包嵌入台服 738 条通过格式与字形校验的译文及 5418 字形、33 像素系统位图字库；该数量已被后续9693条成果整合更新，不代表当前本地构建输入。来源为 RTD_TRANSLATION/analysis/tw-apk；未导入冲突、格式不兼容或缺字候选。场景对象跨版本未建立可靠对应，未按 ID 猜配。未覆盖服务端主表全部剧情。日文包忽略设备外置翻译；繁中包保留外置覆盖机制。尚待设备显示验收。

## 2026-11-01 繁體翻譯成果整合

已由 handoff/RTD-translation-complete-20261101.zip 導入9693條有效繁體譯文，同步RTD_TRANSLATION，全部通過字形、占位符與NGUI標籤校驗。繁中包沿用台服5418字形字庫。handoff/已加入Git忽略，未追蹤。交付勘誤清單中的59條既有譯文建議依保留既有譯文要求尚未套用，詳見RTD_TRANSLATION/metadata/legacy-translation-errata.csv。靜態打包不代表真機字形、長文本排版或全部運行時文字覆蓋已驗收。

## 简体版打包

`./build.ps1 -Language zh-CN` 或 `python scripts/build.py --language zh-CN`，输出 `dist/rtd-client-zh-CN.apk`，报告 `dist/build-report-zh-CN.json`。当前本地翻译与字体为9693条简体译文及6015码点幼圆加粗字库（64像素，4096×8192）。中文构建使用paths指定的实际资源，zh-TW参数不会自动转换当前简体内容；重新构建繁体版需先恢复对应资源。

## 协议归档与通关诊断（2026-11-06）

- [协议索引](docs/protocol/INDEX.md)及[网络边界](docs/protocol/NETWORK_SURFACE.md)保存协议与静态定位依据；历史端点已脱敏，归档不能当作当前生产配置或全接口验收结果。
- [通关阶段诊断候选](diagnostics/settlement/README.md)是独立Mono.Cecil工具，按固定程序集SHA256限制输入，插入阶段标记并回读校验原指令、跳转与异常区域。2026-11-06已通过C#编译检查；既有结构验证不等于Android实机验证。
- 该候选未接入默认构建，也未据此发布新APK。资源存在、下载成功及模型显示正常不能证明通关或释放阶段不再闪退；当前仍保留这一未解决状态。
- 活动开放与解锁由目标服务端运营配置、原前置及活动积分共同决定。服务端当前修复与生产资源审计见RTD_SERVER项目，不能用客户端本地缓存或开发数据库代替生产开放列表。

活动积分观察工具见 [event-points 构建与边界说明](diagnostics/event-points/README.md)。Linux 注入时显式解析输入目录中的 Unity 运行库；观察包构建校验不替代通关、到账和重新登录后的设备验收。

## 正式资源版本校验

默认构建已接入启动、关卡入口及独立单包加载前的批量版本校验。服务端提供完整依赖快照，客户端先完整下载并校验，再整体切换清单及内存缓存；失败使用已接受的完整旧版本。此逻辑不需要测试开关。真实设备已验证同 CAB 不同内容替换、音频缓存刷新、损坏依赖整批回退、重启不重复下载和关卡加载；完整关卡结算及硬件故障覆盖边界见验收记录。

实现边界、接口与证据见 [接入说明](docs/resource-updates/runtime-integration.md) 和 [验收记录](docs/resource-updates/acceptance.md)。实际服务端地址、资源索引与签名配置只保存在 ignored 本地文件中。
