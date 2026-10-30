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

详见 [部署与开发](docs/开发部署.md)、[翻译与字体](docs/翻译字体.md)。当前默认翻译表为 `{}`；并未完成全量汉化，也未进行设备界面/字体/内存验收。

默认构建已包含 Java JNI 桥接、桌面 Unity 入口、真实 Unity/Mono 库直载，以及本地模式跳过旧 Google 内购启动检查。游戏 API、调试环境地址与备用下载地址统一使用根配置；网络日志使用 `RTD_NET` 前缀，覆盖实际请求创建、完成、等待和异常。红米 K80 / Android 16 已进入游戏并完成注册、鉴权和登录通信，详见 [Android 16 启动修复](docs/Android16启动修复.md)。

历史实现依据见 [JNI 桥接与崩溃排查](docs/JNI桥接与崩溃排查.md)、[广告插件启动崩溃](docs/广告插件启动崩溃.md)，其中迁移时的构建状态由上述最新记录补充。

部署翻译映射 `inputs/localization/translation.json` 被Git忽略，正式词条由RTD_TRANSLATION项目统一维护。首次构建若文件缺失，自动从 `examples/translation.example.json` 初始化空对象；之后把翻译项目导出文件复制到该路径即可。

## APK 日志等级与设备文件

`rtd.config.json` 的 `logging.level` 控制重新打包后的托管 Unity 日志：`debug`（默认，全部）、`info`（普通日志及以上）、`warning`、`error`、`none`。当前 Unity Debug.Log 没有独立 debug 级别，因此 debug 与 info 输出相同。过滤覆盖游戏主程序集、firstpass 和三个 RTD 辅助程序集；不控制 Android 系统、Java、Unity 原生引擎日志。不生成日志文件，使用 adb logcat 查看。修改后运行 `./build.ps1` 并安装新 APK；构建报告记录 logging_level。

应用内部文件路径为 `/data/data/jp.co.acquire.RTD/files/`（通常对应 `/data/user/0/jp.co.acquire.RTD/files/`），不是 `/data/jp.co.acquire.RTD`。普通文件管理器受 Android 沙箱限制，无法访问。安装本身也不会把所有 APK 资源复制到该目录；文件通常在首次运行及下载后产生。翻译等内置资源保留在 APK 内；本仓库 resources/UnityCache 现随 APK 打包，启动时释放到应用缓存目录。

可尝试 `adb shell run-as jp.co.acquire.RTD ls -la files`。只有 debuggable APK 才支持 run-as；若返回 package not debuggable，需使用可调试构建，或在已 root 的测试设备使用 `adb shell su -c 'ls -la /data/data/jp.co.acquire.RTD/files'`。普通存储权限不会开放私有目录。导出单个文件可用 `cmd.exe /c "adb exec-out run-as jp.co.acquire.RTD cat files/文件名 > 导出文件名"`，避免旧版 PowerShell 重定向破坏二进制文件。外部应用目录也可检查 `/sdcard/Android/data/jp.co.acquire.RTD/files/`；Unity persistentDataPath 的实际位置取决于引擎与设备，不能仅凭内部目录为空判断保存失败。


## 内置下载资源

构建必须提供非空 `paths.resourceCache`（默认 `resources/UnityCache`），将 Shared 下全部文件加入 APK 的 assets/rtd-cache，构建末尾逐文件对比打包内容。生成的索引记录相对路径、字节数和 SHA256；构建报告记录资源数量和总字节数。

TitleMenu/Installer 的 Awake 在原始逻辑前调用 RTDEmbeddedCache.Ensure，通过 Android getCacheDir 定位 UnityCache，使用 Java 文件通道分块释放。不会复制用户存档。每个文件完成后写入以资源 SHA256 命名的完成标记，后续启动依据标记和长度跳过；中断无标记的文件重新释放。释放异常会抛出，不把未完成的释放标记成成功。当前实现不对已释放文件每次重新计算哈希；需要强制恢复相同长度的损坏文件时删除对应 .rtd-* 标记。

首次释放同步执行，启动画面可能停留较久；需要额外约 283 MB 可用空间。系统清理缓存后会重新释放。已有资源完整且与服务器索引一致时可避免资源包下载；登录、索引及游戏 API 仍访问服务器，缺失或新版资源仍可能下载。旧版引擎实际缓存识别、首次启动耗时和断网资源加载仍需真机验收。
