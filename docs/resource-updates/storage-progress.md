# 本地事务存储阶段进展

46 项核心检查通过：传输期限 4、存储 14、协议 14、计划器 8、事务整合 6。运行 `python3 scripts/test_resource_updates.py`。包括坏下载与准备失败不激活、取消恢复、完整历史闭包回退、未接受提交恢复、接受后清理失败，以及目录切换各边界的显式回滚。

所有核心源码使用原 APK 裁剪库编译通过。原库缺少标准文件移动、目录删除、SHA256 与常见编码 API；RTD_LEGACY 分支使用 Android Java 文件操作、MessageDigest 与显式十六进制编码。文件同步采用 FileDescriptor.sync，目录同步由构建补丁将 NativeOpen/NativeSync/NativeClose 转为 libc P/Invoke；未补丁时明确抛错。真实设备已完成下载、哈希、落盘与整批清单提交。

当前已接入所有语言默认 APK 构建。真实设备验证同 CAB 不同内容的预制体切换、音频缓存刷新与关卡正常进入；阶段性失败已验证整体恢复旧清单和内存。最新故障与设备覆盖边界见 [验收记录](acceptance.md)，不能用纯存储检查代替全部设备验收。
