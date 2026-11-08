# Resource Transactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 正式客户端每次资源加载前批量校验版本，完整依赖事务更新和内存重载，失败整批回退。

**Architecture:** 服务端不可变快照与内容寻址文件；客户端独立资源存储与 journal，通过安全加载边界切换资源批次。原 Unity 缓存仅作派生缓存。

**Tech Stack:** Python unittest/HTTPServer；兼容原 Unity/Mono 的 C#；Mono.Cecil；现有 Python APK 构建链。

**Spec:** `docs/superpowers/specs/2026-11-08-resource-transactions-design.md`

## 执行状态

本计划的任务 1–6 已完成实现、默认构建接入和主要验证；任务 7 已执行核心实机更新、损坏依赖恢复与重启复用，覆盖边界及证据见 [验收记录](../../resource-updates/acceptance.md)。完整关卡结算、硬件故障细分和峰值内存不在本轮已通过证据中，下面原始清单保留作为后续验收目录。

用户后续明确授权远程 ADB 安装调试及备份后同步生产，覆盖下文原先“不部署、不安装”的计划限制；仍禁止推送。生产活动快照已恢复诊断前状态，服务端新接口与正式客户端改动保留。

## Global Constraints

正式逻辑，无测试模式；每个加载操作一次批量 resolve；关卡依赖整体更新回退；未知版本不冒充已校验；不改战斗、存档、结算语义；不提交敏感配置。不部署、不安装 APK。示例配置仅回环或占位符。新功能必须覆盖基础包与所有可达远程包加载入口。协议与默认超时以设计文档为准。

## Review Focus

- 同 CAB 的新旧资源与共享对象：真实设备验证引用与恢复，不以模拟通过代替。
- 发布与下载并发：同批严格读取固定快照。
- 取消、磁盘满、进程死亡：不能留下半提交清单。
- 内置缓存与已提交新版本并存：重启不得降级。
- 动态/间接依赖及同名包：不漏包、不错误合并。

---

### Task 1: 固化加载边界并验证旧引擎重载

**Files:** Create `docs/resource-updates/loading-boundaries.md`, `diagnostics/resource-updates/Probe.cs`, `diagnostics/resource-updates/README.md`, `src/ResourceUpdateTests/BoundaryTests.cs`。

**Interfaces:** 产出可审计的当前 APK hash、所有缓存/包/对象加载点、协程 MoveNext、资源所有者和释放点表；确定 bridge 的 roots 收集与 gate 注入位置。禁止用归档 token 直接打补丁。

- [ ] 枚举 original-managed 中 AssetBundleDefine、AssetBundleLoader、GameManager、ResourceManager 及所有 WWW/AssetBundle/Resources 加载调用，记录实际签名及依赖分支。
- [ ] 编写边界结构测试，要求未知程序集、遗漏调用点、重复补丁均失败；先确认测试失败。
- [ ] 编写最小诊断候选，验证同名同 CAB 两版包、共享材质/动作/音频引用、卸载重建、失败恢复及原结算路径。
- [ ] 在获授权的设备环境验证并记录结果；未证明内存切换与恢复可用，后续可做纯逻辑但不得定稿运行时桥接或发布。
- [ ] 明确是否可保留旧对象准备新版；若不可并存，记录卸载重建的实际回退边界并重新评审设计。

### Task 2: 服务端不可变目录与原子发布

**Files:** Create `../RTD_SERVER/rtd/resource_catalog.py`, `../RTD_SERVER/rtd/resource_publish.py`, `../RTD_SERVER/scripts/publish_resources.py`, `../RTD_SERVER/tests/test_resource_publish.py`。

**Interfaces:** `publish_resources(roots: list[str], output: Path) -> str` 返回 snapshotId；`resolve_snapshot(snapshot: dict, roots: list[str]) -> dict` 返回完整闭包。snapshot 模型按设计协议，cacheVersion 持久递增且不复用。

- [ ] 测试同长度替换产生新 hash/版本、同名不同路径区分、缺依赖/循环拒绝、激活前中断保留原清单、旧 blob 不变；先运行确认失败。
- [ ] 实现三类资源来源统一解析、显式依赖图、不可变 blob 写入及原子清单激活；不可验证的依赖阻止发布而非默认为空。
- [ ] 运行 `python -m unittest tests.test_resource_publish -v`（RTD_SERVER），要求全部通过；审查原始资源与有效关卡数据未改变。

### Task 3: 批量校验与固定版本下载接口

**Files:** Modify `../RTD_SERVER/rtd/resources.py`; Create `../RTD_SERVER/tests/test_resource_versions.py`; retain `tests/test_resource_routes.py`。

**Interfaces:** POST `/resources/v1/resolve` 和 GET `/resources/v1/blobs/<sha256>/<filename>`；JSON 字段见 spec。resolve 从内存快照计算闭包，下载路径不可读可变同名文件。

- [ ] 编写无变化/变化/未知协议/非法路径/请求超限/闭包缺失/发布并发测试；下载旧快照必须仍返回旧字节。
- [ ] 运行新增测试确认失败，再接入 Handler，并保留原公告 POST 与资源 GET 行为。
- [ ] 运行 `python -m unittest tests.test_resource_versions tests.test_resource_routes -v`，再运行服务端现有测试集；记录无变化处理耗时，目标 p95 <=20ms（注明测量环境）。

### Task 4: 客户端纯逻辑与持久事务

**Files:** Create `src/ResourceUpdates/Protocol.cs`, `Planner.cs`, `Store.cs`, `Transaction.cs`; Create `src/ResourceUpdateTests/ResourceUpdateTests.csproj`, `Program.cs`。

**Interfaces:** `ResourcePlanner.Plan(ResolveResponse remote, LocalSnapshot local) -> UpdatePlan`; `ResourceStore.Recover() -> LocalSnapshot`; `ResourceTransaction.Prepare(UpdatePlan plan) -> IEnumerator`; `Commit()`/`Rollback()`。DTO 对齐 spec，纯逻辑通过抽象文件/下载/时钟接口测试，避免依赖现代运行时 API。

- [ ] 编写变更/缺失/完整旧闭包选择/不同历史不可拼接/同名缓存版本隔离测试，先跑失败。
- [ ] 编写每个 journal 边界杀进程模拟、损坏与截断、磁盘满、取消、并发操作串行和 hash 校验失败测试。
- [ ] 实现独立 store、verified 标记、active/previous 指针、journal 恢复与保留策略；首次无旧版本明确返回不可回退。
- [ ] `dotnet run --project src/ResourceUpdateTests/ResourceUpdateTests.csproj` 全通过；旧 Mono 编译通过后才允许集成。

### Task 5: Unity 下载与内存批次切换

**Files:** Create `src/ResourceUpdates/UnityTransport.cs`, `UnityResourceBridge.cs`, `UpdateLog.cs`; Modify `src/RTDEmbeddedCache.cs`; Create `src/ResourceUpdateTests/UnityBridgeTests.cs`。

**Interfaces:** `EnsureCurrent(string[] roots) -> IEnumerator` 返回门闩结果 Ready/UsingPrevious/Unavailable；每 operation 固定快照；`PrepareGeneration(UpdatePlan plan) -> IEnumerator`, `ActivateGeneration()`, `RestorePreviousGeneration()`, `ReleasePreviousGeneration()` 按 Task 1 确定的所有者调用，不全局盲卸载。

- [ ] 模拟验证调用顺序：新字节 verified 前不卸载旧对象；Prepare 失败不提交；共享资源引用未释放不得回收；重启 Ensure 不覆盖新批次。
- [ ] 实现一次批量 resolve、并发 2 下载、校验总超时 2 秒与无进展 15 秒默认值，沿用原加载 UI；具体目标设备调整须记录。
- [ ] 按 Task 1 结果实现重载/恢复；对不能安全切换的常驻资源不得偷偷跳过，应阻止功能验收并说明限制。
- [ ] 跑纯逻辑/bridge 测试与旧 Mono 编译；日志必须含阶段、版本和回退原因，不能输出真实端点或账号。

### Task 6: 专用 IL 补丁与默认构建集成

**Files:** Create `src/ResourceUpdatePatcher/ResourceUpdatePatcher.csproj`, `Program.cs`, `ResourceUpdatePatch.cs`; Modify `scripts/build.py`, `src/EndpointPatcher/Program.cs`, configuration example, build report。

**Interfaces:** patcher `apply <input> <helper> <output>` / `verify <patched>`；gate 在原加载开始前 yield，不在同步 WWW 包装器里阻塞。运行时 bridge 独立引用原游戏类型，纯逻辑放 RTDFlow，禁止循环依赖。

- [ ] 结构测试覆盖分支/EH/状态机恢复、门闩只进一次、根集合传递、所有远程入口覆盖、重复 apply/未知 hash 拒绝；先确认失败。
- [ ] 实现并回读验证 IL；接入所有语言默认构建、配置校验和报告，未知协议不得静默绕过校验。
- [ ] 运行 patcher verify、现有 LocalStore/Localization/Loader 测试及 APK 构建校验；确认翻译、内置释放、签名与既有补丁仍通过。

### Task 7: 故障与设备验收、运维交付

**Files:** Create `docs/resource-updates/acceptance.md`, `../RTD_SERVER/docs/resource-version-publishing.md`; Modify 两仓 README。

**Interfaces:** 发布命令、回滚清单流程、诊断日志格式与验收记录。

- [ ] 用本地服务执行全链路：零更新、单包替换、依赖多包、同长度变化、快照发布中切换、404、坏 hash、超时、首次缺包、取消与空间不足。
- [ ] 获授权后真机验证内存对象确实来自新版、故障整批恢复、各提交点终止后启动恢复、常驻共享包、连续进出关卡及结算；记录 native crash 与现存结算闪退基线，不混为功能通过。
- [ ] 记录请求数量、无变化额外延迟、更新耗时和内存峰值；无变化一次请求零下载，失败不呈现半新半旧资源；性能阈值根据基线测量审阅后冻结。
- [ ] 更新发布/回滚/日志文档；仅在检查两仓 diff、敏感信息与相关测试后形成待审提交，不在本计划任务中部署或安装。

## 执行顺序与完成标准

顺序 1→2→3→4→5→6→7。Task 1 是运行时可行性的硬门槛；Task 2/3 接口可先本地实现，但不得把纯逻辑通过视为引擎事务已成立。代码完成、构建通过、设备验收分别记录，缺设备结果时不得宣称整体完成。
