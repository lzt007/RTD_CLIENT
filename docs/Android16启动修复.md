# Android 16 启动修复（2026-10-28）

实测设备：红米 K80，Android 16 / OS3.0.305.0.WOMCNXM。

默认构建已固化三个独立环节：

- 原有 BridgePatch / GzipBridge 与 no-social 构建链继续提供 Java 桥接及社交初始化绕过。
- android_launcher.py 将旧广告 Activity 的 MAIN 启动过滤器移至 com.prime31.UnityPlayerNativeActivity，保留其原始配置和广告 Activity 的其他过滤器；桌面启动无需 ADB 指定入口。
- 将 assets/libs 中的真实 Unity（包含输入修复）和 Mono 库同时写入 lib/armeabi-v7a，替代旧加载器外壳。对照实验中外壳在 JNI_OnLoad 调用链发生 SIGSEGV，替换后进入 Unity 公告界面与 GameState_LoadingBundle。此结果不等于已确定外壳内部具体缺陷。

最终 dist/rtd-client.apk 已正常签名、覆盖安装并经 launcher 冷启动，进程保持运行，截图位于 build/rtd-startup.png，运行日志位于 build/android16-runtime.log。构建自带的托管契约、zipalign、apksigner 检查通过。未完成登录、战斗与存档全流程验收。

系统报告应用私有目录为 /data/user/0/jp.co.acquire.RTD；普通文件管理器看不到该目录不能判定安装失败。首次 ADB 安装须允许 USB 安装；旧 targetSdk 安装使用 --bypass-low-target-sdk-block。

本机 Android Build Tools 位于 tools/android-sdk（被 Git 忽略），构建支持在默认 SDK 不存在时发现该目录。迁移机器仍需准备 SDK。

此前两份迁移文档描述的是修复前状态；本页记录本次新增构建行为及设备证据。

## 2026-10-29：资源下载后的“通信中”卡住

完整 ADB 启动日志显示：common-text 请求已完成，随后进入 `Billing : NEW_BILLING`、`Supply Product Start`、`Coroutin_Init`、`Success : Init`，标题状态最终停在 410。此时没有游戏 API 请求发出。旧 Google 内购检查与游戏请求共用“连接服务器”弹框，故界面文案不能证明 API 服务在等待响应。

本地模式在 `LocalPlatformPatch` 将 TitleMenu.Update 最初两个进入内购状态 400 的入口改为后续标题状态 200。保留正常资源、注册、鉴权及游戏通信流程，不生成购买回执、广告奖励或货币。构建要求原版六处状态 400 写入形状一致，修补后校验剩余四处；形状变化立即失败。

修复后手机实际发出 `create_user`、`auth_user`、`get_level_master`、`order_quest`，ADB 记录请求发往配置的 18766 端口并完成、解压。随后服务端记录 `get_units`、`get_friends`、`get_presents`、`save_local_flags`、`save_quest_new_flags`、`login` 返回成功码 4096。截图进入教学战斗，用户确认“这次正常了”。这轮证据确认启动与实际 API 链恢复，不宣称完整游戏内容验收。

`EndpointPatcher` 额外替换调试菜单中五套旧环境的 API/common-text 地址及备用 QRPG 资源根，共 16 处。正常 API 两个入口此前已替换；这些遗漏的备用地址不是本次启动卡住的已证明原因。支持页面、商店链接等非游戏协议地址保留，并在 `dist/endpoint-audit.json` 列出方法位置供复核。

## 固定构建与诊断流程

根目录执行 `./build.ps1` 即可从锁定原 APK 生成包含上述修复的 `dist/rtd-client.apk`。无需从手机回拷补丁文件或使用诊断专用 APK。沿用 `build/development.keystore` 可覆盖安装并保留数据；换机时同时迁移 ignored 的原 APK、工具、资源、翻译和签名密钥。

构建将 `RTDNetworkTrace.cs` 编入 RTDFlow.dll，以同签名包装替换五处 `WWW(string, byte[], Hashtable)` 创建点，覆盖协程 MoveNext。检查要求五处包装调用存在且没有遗漏的同类原始创建点。日志包含请求编号、去掉查询参数的 URL、请求字节数、完成耗时、错误及每 15 秒一次的等待提示；不打印请求正文、Cookie 或 UUID。监控不改变请求超时、重试、响应解压或 DTO。`IsEnd` 等逐帧轮询不加日志。

可使用已配置的 adb 抓取：

```powershell
adb logcat -v threadtime 'Unity:I' 'AndroidRuntime:E' '*:S' | Select-String 'RTD_NET|FATAL EXCEPTION'
```

本次完整日志在 ignored 的 `build/network-adb-live.log`，仅网络追踪摘录在 `build/network-trace.log`，构建日志在 `build/network-fix-build.log`。完整原始游戏日志可能包含原客户端打印的数据，排障分享优先使用追踪摘录。
