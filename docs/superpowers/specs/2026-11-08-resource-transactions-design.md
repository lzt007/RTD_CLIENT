# 正式资源批次校验与事务更新设计

状态：范围分析完成；待开发前审阅。本文不表示运行时方案已通过真机验证。

## 目标与边界

正式客户端每次开始资源加载操作前校验服务端版本；进入关卡前将该关卡完整资源集合合并为一次批量请求。版本相同直接加载，变化或缺失则下载。无测试开关、无更新弹窗；复用原加载界面。更新成功后重新加载受影响的内存资源，失败整批使用完整旧快照。不存在完整旧快照时走原加载失败处理，不伪造可回退状态。

一次加载操作固定一个服务端快照，不能在批次中途追逐新发布版本。同一操作中的重复查询合并，下一次操作必须重新联网校验。覆盖标题/基础包、关卡及其他可达 AssetBundle 加载入口；APK 内 Resources 资源不在服务端更新范围。普通游戏 API、战斗规则、存档、翻译和结算逻辑不改。

## 已确认事实与依据

- `src/RTDEmbeddedCache.cs` 包装 IsVersionCached 和 LoadFromCacheOrDownload；方法同步返回 WWW，不能承担异步前置批量校验。
- `src/TracePatcher/Program.cs` 已替换上述调用并注入 Ensure；`scripts/build.py` 编译 RTDFlow 并接入这些补丁。
- 服务端 `rtd/resources.py` 当前按文件名查原包及修复资源策略，没有快照/版本协议，也没有 ETag；修改它时须保留公告和既有下载路由。
- `../RTD_SERVER/docs/archive/protocol/RESOURCE_LOADING_RECHECK_20261028.md` 记录基础包与 GameManager 关卡敌人协程加载路径。
- `../RTD_SERVER/docs/archive/protocol/ORIGINAL_CACHE_IDENTITY_20261028.md` 确认此引擎缓存键为文件名与非零整数版本，忽略 URL 目录与 query；不能靠更换目录隔离缓存。
- `../RTD_SERVER/docs/resource-audit/dependencies.py` 明确角色武器/技能效果等递归依赖未穷尽，不能把现有审计表直接当完整依赖清单。
- 结算/释放阶段仍有独立未解决闪退。不能将更新功能验收代替结算问题修复，也不能引入批量 Unload(true) 绕过资源所有权。

## 方案取舍

单资源 HEAD 校验无法给依赖集提供同一快照；整份启动清单不能满足每次加载前校验。采用批量 resolve、不可变资源发布、客户端批次事务。此选择依据用户明确要求提出，尚非已验证的引擎实现。

## 协议与服务端

新增 POST /resources/v1/resolve，输入 protocol=1、operationId、roots、knownSnapshot 和本地资源版本映射。输出 snapshotId、closureDigest、完整依赖闭包 resources；每项含 id、sha256、size、cacheVersion、downloadPath、dependencies。返回集合必须完整，不能只返回 changed，客户端据此判断旧批次是否足够。无变化仍返回一次小型响应，后续可增加闭包摘要压缩，首版不依赖复杂协商。

资源 id 使用规范逻辑路径，不能仅用 basename 合并不同资源；cacheVersion 为同文件名全局不复用的正整数，不能截断哈希生成。hash 用于字节完整性，snapshotId 用于集合身份，cacheVersion 仅用于 Unity 缓存，不得混用原 VersionDir 数组下标。

新增 GET /resources/v1/blobs/<sha256>/<filename>，读取不可变已发布文件，永不回退到当前可变同名文件。发布先构建并检查所有 blob/依赖，最后原子切换清单指针；请求读取一次快照。旧快照和 blob 保留，首版不自动删除已发布历史，后续在明确客户端引用保留策略后增加 GC。发布脚本支持当前三类资源来源优先级，不改原始数据与资源引用。替换资源通过发布命令自动计算 hash/分配版本/激活，无需人工改版本号。

请求有大小、资源数、路径与依赖深度限制；循环依赖报发布错误，缺失依赖禁止激活。校验请求不扫描文件、不计算资源 hash，读取内存索引。建议初始校验总超时 2 秒、同批下载并发 2、单下载无进展超时 15 秒，均为待实测调优的工程默认值。无变化服务器处理 p95 <=20ms 为初始目标，客户端额外耗时按网络 RTT 单独统计，不能宣称公网固定延迟。

## 客户端事务与存储

状态：Resolve -> Stage -> Verify -> Prepare -> Commit -> Release；失败进入 Rollback。只允许一个激活事务，其他操作排队或共享同一 operation，不混合快照。

客户端新增独立 rtd-resource-store，保存不可变下载文件、事务 journal、active/previous 清单及完整性状态；UnityCache 为引擎派生缓存，不作为事务真相。下载采用临时文件，校验长度和 SHA256 后才登记 verified。旧版本保留至切换成功并释放引用。清单提交采用同文件系统临时写入/flush/替换及 journal 恢复；Android/旧 Mono 的真实原子替换与 fsync 能力需要验证，不能假设现代 .NET API 可用。

磁盘提交不等于内存提交。关卡入口先停止新对象创建，准备完整新资源集并验证必需对象，然后在安全边界切换资源句柄/清除受影响管理器引用并重建对象，最后提交 active。旧对象在新对象准备成功前尽量保留。若同 CAB 无法并存，需验证安全卸载后从保留旧文件重建的路径；该路径失败或 native 崩溃不可被托管事务保证回滚，未通过故障验证不得发布功能。共享音频、角色包等跨场景常驻资源也要明确所有者，不能仅刷新敌人 bundle。

启动先恢复 journal，再使用已提交快照；embedded Ensure 只提供基线，不能覆盖或提升优先级压过已提交更新。基线只有经发布映射确认 hash/版本身份后才能作为该版本使用，未知旧缓存不可谎报已校验。新机制独立存储避免旧完成标记覆盖新下载。

每次操作若校验失败或更新失败，选择最后完整、可加载且覆盖本次根资源及依赖的已提交快照；不拼接不同历史快照作为回退。取消/切后台/空间不足不提交。崩溃恢复丢弃未提交暂存；已提交但尚未验收批次按 journal 恢复 previous，真实 native 崩溃恢复行为须设备测试。

## 改动范围

客户端新增 ResourceUpdates 下协议、计划器、存储、下载、事务、Unity桥接与日志模块；新增专用 ResourceUpdatePatcher 和结构测试；修改 build.py、RTDEmbeddedCache.cs、EndpointPatcher 与示例配置。RTDFlow 编译链接入纯逻辑；依赖原游戏类型的桥接按现有 RTDRestoreCache 模式独立程序集，避免循环引用。具体协程/token/字段签名须首任务从当前原 APK 程序集枚举并固化，不从归档猜测。

服务端新增 resource_catalog.py、resource_publish.py、版本接口测试及发布脚本；修改 resources.py、配置示例和部署文档。不改生产配置、不启动/部署服务端、不安装 APK、不控制模拟器；真机验收在后续授权的设备环境执行。

## 验收

无变化只发生一次校验、零资源下载；同长度内容替换能识别；完整递归依赖一起更新；旧内存引用确实切换；任一下载/校验/准备失败整批回旧；第一次缺包失败不进入半资源关卡。发布并发、同名资源、取消、磁盘满、各提交点杀进程、共享资源、重复进出关卡与结算均覆盖。日志记录 operation/snapshot、匿名资源 id、新旧版本、阶段、耗时、回退原因，不记录账号、凭据和真实端点。Mock 测试不能代替旧 Unity Android 的加载、释放、内存与崩溃验收。
