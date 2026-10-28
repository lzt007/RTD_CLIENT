# 旧原生 JNI 路径崩溃与 Java 桥接

2026-10-28 从 RTD_FAKE 迁移整理。历史实验发生于 2026-10-23 的 API28 模拟器、ARMv7 旧 Unity 客户端环境。本页说明已有兼容方案及证据边界，不表示本项目的新签名 APK 已通过设备验收。

## 结论与适用范围

已确认的故障是旧原生取字符串及文件读取路径在该环境中异常。当前方案通过 Unity 的 AndroidJavaObject/AndroidJavaClass 调用 APK 已有 Java 类，绕开不稳定的旧原生 JNI 调用路径；没有修改 `JNI_OnLoad` 的机器码，也没有证明 `JNI_OnLoad` 未调用就是根因。

原 APK 身份为 SHA-256 `1e95f5a91c6d3ead09c7f5462ace6238deda87392621f07758c5dc7439d784ec`。当前构建锁定此输入，不能把现有补丁直接套用到其他版本。

## 历史定位过程

1. 启动跟踪最初停在 `SocialManager.Awake`，其中同步调用 `FacebookAndroid.init` 和 `TwitterAndroid.init`。同时跳过这两个调用后，常驻对象初始化完成、标题状态推进。该实验确认的是这组社交初始化阻塞，尚未分别确定哪个 SDK 是直接阻塞者。
2. 推进后日志在 `SaveDataManager.DeleteOldData()` 附近结束并出现 signal 11。仅凭最后一条日志不能归因于删除存档：静态代码中的 `DeleteOldData_240` 为空，标题下一步会调用 `SaveDataManager.Load`。
3. 两级调用跟踪进一步定位为 `SaveDataManager.Load → GetString → MySharedPreferencesCall.getString → 原生 _getString` 后退出，未执行后续 `Marshal.PtrToStringAuto` 或 `_BufferRelease`。
4. 历史 Ghidra 分析显示 `_getString` 从 `JNI_OnLoad` 缓存的 JavaVM 获取线程环境，创建 Java 字符串、调用缓存对象并取得字符串字符指针。但 `JNI_OnLoad` 的反编译参数恢复不准确，不能把伪代码中的空指针常量当作现场寄存器值。
5. 对照实验在读取前通过 Unity 调用 `java.lang.System.loadLibrary("MySharedPreferences")`，仍未越过 `_getString`，进程退出。因此仅显式加载库不是已验证的修复方案。
6. 改为 Java 偏好设置桥接后继续推进；`AndroidUtilCall.LoadFileDataBinary` 的旧原生路径也异常，随后改为已有 Java 文件读取，并对不存在的私有文件提前返回 null，避开旧 Unity 对 Java null 字节数组的封送崩溃。

当时没有取得足以确定崩溃指令的寄存器/原生栈，Java 异常、缓存引用失效与 ARM 翻译兼容性等可能性未被完整排除。不要把桥接有效扩大为已证明某一底层根因。

## 本项目中的实现

主要实现见 [BridgePatch.cs](../src/TracePatcher/BridgePatch.cs)，构建入口见 [build.py](../scripts/build.py)。迁移核对时，BridgePatch.cs 与旧项目对应源码字节一致。

| 范围 | 当前处理 |
| --- | --- |
| 偏好设置对象 | 通过 `UnityPlayer.currentActivity` 创建并缓存 `jp.co.acquire.MySharedPreferences` Java 对象 |
| 偏好设置方法 | 将公开包装路径切换为 Java 方法调用，保留调用参数和返回类型；不是一律返回默认值的空实现 |
| `getString` 默认值 | 先用 `Contains` 判断键是否存在；缺失返回调用者原默认值（包括 null）；键存在但默认参数为 null 时传空字符串，规避旧 Unity JNI 签名解析对 null 字符串的限制 |
| 文件读取 | 调用 `jp.co.acquire.AndroidUtil.LoadFileDataBinary`，将字节按 UTF-8 解码；先检查 `/data/data/jp.co.acquire.RTD/files/` 下文件是否存在，缺失返回 null |
| 文件写入 | 将文本按 UTF-8 编码后调用已有 Java `SaveFileDataBinary`，保留 Java 私有文件路径语义 |

`_getString`、`_BufferRelease` 本身未被改写成 Java 桥接；公开 `getString` 包装方法已避开它们。文件路径仍包含固定包名，支持新包名或新 Android 存储环境前需重新审计。

[TracePatcher/Program.cs](../src/TracePatcher/Program.cs) 调用 `BridgePatch.Apply`；构建传入 `no-social` 跳过上述 Facebook/Twitter 初始化。另有 [GzipBridge.cs](../src/TracePatcher/GzipBridge.cs) 在 local-api 模式下把压缩/解压包装调用转到已有 Java `org.example.ScriptBridge.GZipArchive`。后续文件写入与 gzip 兼容也已保留，但不能将其全部说成 2026-10-23 取字符串实验直接证明的内容。

## 已有验证与复核方法

历史兼容版本两次从标题 Loading 进入欢迎对话，其中一次关闭诊断日志。原记录还包含欢迎 OK 点击进入昵称界面的复核。诊断期间使用 root 临时替换安装包、finally 恢复原包并校验 SHA；当时的派生包签名失效，不能等同于本项目正常开发签名产物的安装验收。

当前可通过根目录 `build.ps1` 重建，具体步骤见 [开发部署](开发部署.md)。静态构建与测试范围见 [验证记录](验证记录.md)；它们不代替设备上的持久化兼容测试。进一步设备验收应覆盖已有偏好设置的具名文件、字符串/布尔/整数值、缺失键与 null 默认值、私有文件不存在/读写/重启，以及实际登录和 gzip 请求路径。应保留原数据并比较前后结果，不通过清空存档或统一返回默认值掩盖故障。

输入兼容、场景布局修补、社交初始化绕过和广告入口绕过属于独立环节。该 JNI 桥接不代表其他偶发原生 SIG11 或图形 ANR 已根治。广告入口问题见 [广告插件启动崩溃](广告插件启动崩溃.md)。

## 迁移来源与历史证据位置

本页迁移了旧项目以下文档中与 JNI/启动链相关的内容，并按当前源码补充实现入口：

- `RTD_FAKE/analysis/android/preferences-conclusion.md`：取字符串跟踪、显式加载库失败实验与根因限制。
- `RTD_FAKE/analysis/android/startup-conclusion.md`：社交初始化阻塞及 DeleteOldData 误归因的澄清。
- `RTD_FAKE/analysis/android/loading-fix.md`：桥接方案、欢迎界面验证及当时的限制。

原始日志和截图仍在旧项目，包括 `prefs-native-failure.txt`、`startup-trace-first-logcat.txt`、`startup-trace-social-block.txt`、`startup-trace-logcat.txt`、`loading-fixed-welcome-logcat.txt`、`loading-fixed-welcome.png`、`loading-fixed-quiet.png`；反编译资料为 `analysis/ghidra/prefs.txt` 和 `InspectPrefs.java`。本次迁移文档内容，没有复制这些原始证据，也没有执行新一轮设备实验。历史 startup-trace 文件曾被后续测试更新，不能假定现存同名文件仍逐字对应最早一次实验。
