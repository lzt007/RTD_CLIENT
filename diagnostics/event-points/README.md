# 活动积分只读遥测工具

用于复查「歴史の闇を知りし者」20710／20711／20712的服务端积分协议和客户端解锁状态。工具只生成独立诊断APK，不安装应用、不操作ADB、不修改或部署服务端。

本次事件的 [完整分析结果](https://github.com/lzt007/RTD_SERVER/blob/fix/history-event-unlock-verification/docs/protocol/history-event-unlock-analysis-20261107.md)、[修复方法与ADB回放手册](https://github.com/lzt007/RTD_SERVER/blob/fix/history-event-unlock-verification/docs/protocol/history-event-unlock-fix-20261107.md) 及 [脱敏验收摘要](https://github.com/lzt007/RTD_SERVER/blob/fix/history-event-unlock-verification/docs/protocol/evidence/history-event-unlock-20261107.json) 位于服务端仓库。已验证20710正常通关7分到账、重启重登后20711解锁；50分边界经隔离协议测试，本次未实机刷满50分。工具的一次构建成功不代表这些运行步骤自动完成。

## 观察边界

- 输入必须是含 `RTDFlow.Tick(object)` 的现有兼容客户端。保留输入APK的游戏逻辑、网络地址、缓存资源与界面实现。
- 唯一修改的原ZIP条目是 `assets/bin/Data/Managed/RTDFlow.dll`；唯一新增条目是 `assets/bin/Data/Managed/RTDPointProbe.dll`。原游戏 `Assembly-CSharp.dll` 与其他原条目逐字节一致。签名元数据重新生成。
- 注入在 `RTDFlow.Tick(object)` 入口，将原组件参数原样传给只读探针。除这一对入口指令及明确选择恢复的两个现有UI日志调用外，全部原辅助方法及异常处理区域均回读比较。
- 探针通过反射读取字段，唯一调用的游戏方法是 `PlayerData.GetEventPoint()`。注入器先确认它恰为 `ldsfld PlayerData.SpEventPoint; ret`（忽略NOP），形状不同直接拒绝。探针不调用加分、存档、登录、战斗推进或网络方法，不写游戏字段。
- 诊断异常被捕获，只记录异常类型，不记录账号、会话、请求体、地址、私钥或口令。探针保留自身的采样缓存。日志可能改变运行时序，因此静态结构验证不能代替Android运行验收。

## 准备与构建

依赖已有Python 3.10+、.NET SDK 10、Mono.Cecil、JDK和Android Build Tools。使用输入APK中解出的原 `mscorlib.dll` 和 `UnityEngine.dll` 编译探针，避免引用桌面.NET运行库。工具不下载或安装依赖。注入器从输入程序集所在目录解析 Unity 运行库，避免 Linux/.NET 环境下误用宿主 GAC；请保留构建工具解出的原 `mscorlib.dll`。

签名口令由调用方安全注入环境变量 `RTD_DIAG_STORE_PASS`、`RTD_DIAG_KEY_PASS`，不要把值写入命令、脚本、Git或验证报告。可通过 `--store-pass-env`／`--key-pass-env` 指定其他环境变量**名称**。签名工具只接收 `env:变量名`，不接收明文口令参数。

从客户端仓库根目录运行，例如：

```powershell
python diagnostics/event-points/build_probe.py `
  --apk build/diagnostic-source.apk `
  --output build/event-point-probe.apk `
  --dotnet tools/dotnet/dotnet.exe `
  --cecil tools/cecil/Mono.Cecil.dll `
  --java "$env:JAVA_HOME/bin/java.exe" `
  --zipalign tools/android-sdk/build-tools/36.1.0/zipalign.exe `
  --apksigner tools/android-sdk/build-tools/36.1.0/lib/apksigner.jar `
  --keystore build/development.keystore `
  --alias rtd-development
```

所有路径均由参数指定，示例签名配置只适用于调用方已有的开发密钥；原密钥不会被创建或复制。需要保留现有账号的覆盖安装时，由操作者确认输入应用签名与所用密钥匹配。

其他选项：

- `--baseline-apk`：另选一个较早APK，要求输出相对它也只能改变UI辅助程序集并新增探针，且游戏主程序集字节一致。适用于输入已经恢复UI日志、但还要对照初始安装包的情况。
- `--restore-flow-logs`：输入辅助程序集恰有两处已知 `LogObservation` 日志被替换成 `pop` 时恢复原日志。形状不符拒绝；输入已有UI日志时不要传此参数。它不恢复其他日志，也不改变游戏逻辑。
- `--work-dir`：保留编译中间文件到一个新目录；默认使用临时目录并在完成后清理。
- `--patcher`：传入由本目录项目生成的 `PointProbePatcher.dll`，复用已有注入器，省略 `--cecil`。`--csc` 可显式指定编译器，否则从所选dotnet旁的SDK目录发现。
- `--report`：指定验证JSON；默认在输出旁使用 `.verification.json` 后缀。输出、报告和显式工作目录必须是新路径，防止误覆盖。再次注入已有探针的APK会被拒绝。

将APK、构建目录和报告留在仓库忽略的 `build/` 中。不要把私有部署配置、密钥、APK、原程序集或真实运行日志加入本诊断源码目录。

## 输出与验证

APK只有在编译、辅助IL回读比较、zipalign、签名验证和ZIP条目比较全部通过后才写到指定输出。验证JSON不包含绝对输入路径、密钥路径、账号或口令，记录：

- 输入／输出APK及未改变游戏主程序集的SHA-256；
- 允许的修改、新增条目及其他全部原条目相同的结论；
- 原辅助方法验证数量、异常区域、纯getter和运行库引用验证；
- zipalign与apksigner结果；
- Android运行验证仍由操作者完成的明确状态。

输入被拒绝、签名失败或结构不匹配时，应处理具体错误，不应关闭验证或改动游戏程序集以让流程继续。

## 遥测字段与回放判读

探针只在内容变化时输出 `RTD_POINT_PROBE {JSON}`，最多四次／秒：

|字段|含义|
|---|---|
|`quest_id`／`sp_event_id`|当前选中关卡及原活动ID；不可用为-1|
|`run_points`|原 `PlayerData.GetEventPoint()` 的实际返回值|
|`event_100001_balance`|原登录缓存内的100001累计值；未登录为null，登录后无该活动条目为0|
|`enemy_groups`|每组保留 `enemy_id`／`enemy_set_id`／`sp_event_point`／`flag` 与出现次数 `count`|
|`enemy_count`／`present_count`|敌人条目数／出现条目数|
|`target_41028_count`|已出现的原暗属性杂兵数量|
|`offered_points`|出现条目的正积分总和；表示可获得总量，不代表已击杀或已到账|
|`quest_unlocks`|最近一次实际观察到的MMQuest列表中20710／20711／20712的 `m_SpEventUnlock`；未观察列表前为空数组|

建议按以下证据链回放，保留每步时间和关联操作：

1. 登录后记录100001余额，打开三关列表记录实际UI解锁标记。
2. 新进入20710，检查实际敌表中41028的积分及其他敌人积分；恢复的旧战斗快照应单独标记，不能当成新入场响应。
3. 通过正常UI击杀目标，确认 `run_points` 随击杀增加；避开目标时不得自动增加。
4. 正常通关并确认结算成功，再记录累计值和返回列表后的解锁标记。原MMResult可能先更新本地登录缓存，因此仅看到余额变化不足以证明服务端结算或持久化成功。
5. 重新登录核对持久化余额，并在原5分／50分边界分别验证20711／20712。积分是累计条件，不能误加逐关通关前置。

通过ADB取日志的操作由调用方的回放脚本或人工执行；本工具不会自动点击、退出关卡、清缓存或补写积分。`quest_unlocks` 是最近UI列表缓存，场景切换后不能解释成刚从服务端获取的最新判定。对未完成的阶段应保留未验证状态。
