# 本地事务存储阶段进展

已实现不可变 blob、SHA256/长度校验、staged/active/previous 代际目录、激活 journal 和接受标记；8 项 Mono 行为检查通过。覆盖同长度损坏、暂存中断、提交未接受后恢复、接受后重启与损坏依赖禁止提交。

尚未接入客户端。当前 FileStream.Flush 不代表 Android 目录项已 fsync；真实掉电耐久性须补 JNI/POSIX 同步并真机验证。内存切换、协议解析、下载、依赖收集及补丁仍待实现。
