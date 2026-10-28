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
