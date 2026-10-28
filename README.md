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
| resources/UnityCache | 复制的完整原始热更新缓存，仅输入存档，不自动推设备 |
| build | 从原 APK 提取的程序集、编译中间文件、开发 keystore |
| dist | 签名 APK、构建报告 |
| tools | 本机工具/Mono.Cecil 二进制，不进 Git |

详见 [部署与开发](docs/开发部署.md)、[翻译与字体](docs/翻译字体.md)。当前默认翻译表为 `{}`；并未完成全量汉化，也未进行设备界面/字体/内存验收。

默认构建已包含 Java JNI 桥接、桌面 Unity 入口、真实 Unity/Mono 库直载，以及本地模式跳过旧 Google 内购启动检查。游戏 API、调试环境地址与备用下载地址统一使用根配置；网络日志使用 `RTD_NET` 前缀，覆盖实际请求创建、完成、等待和异常。红米 K80 / Android 16 已进入游戏并完成注册、鉴权和登录通信，详见 [Android 16 启动修复](docs/Android16启动修复.md)。

历史实现依据见 [JNI 桥接与崩溃排查](docs/JNI桥接与崩溃排查.md)、[广告插件启动崩溃](docs/广告插件启动崩溃.md)，其中迁移时的构建状态由上述最新记录补充。

部署翻译映射 `inputs/localization/translation.json` 被Git忽略，正式词条由RTD_TRANSLATION项目统一维护。首次构建若文件缺失，自动从 `examples/translation.example.json` 初始化空对象；之后把翻译项目导出文件复制到该路径即可。
