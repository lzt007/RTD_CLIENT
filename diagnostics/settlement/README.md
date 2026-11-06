# 通关阶段诊断候选（未部署）

`StagePatch.cs` 是独立 Mono.Cecil 工具，仅接受源码内固定 SHA-256 的实际分发程序集。不会原地写入，不修改 dist 或发布脚本。重复对已注入程序集执行会拒绝；其他 SHA 同样拒绝。

```sh
mcs -r:/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll -out:/tmp/settlement-stage-patch.exe diagnostics/settlement/StagePatch.cs
mono /tmp/settlement-stage-patch.exe /tmp/settlement-client-current.dll /tmp/settlement-diagnostic-candidate.dll /tmp/settlement-stage-verification.txt
```

输出只有固定方法名、原 IL 偏移和 BEFORE/AFTER/ENTER/EXIT；不输出资源名称、URL、请求内容或账号。每次调用通过新建 Mark(string) 助手写 UnityEngine.Debug.Log，助手用 catch(Exception) 丢弃诊断异常，不写持久文件。

覆盖指定胜利、保存、清包、结算准备和请求构造方法入口及正常 ret，以及 Enemy.UnLoad/AnimationController.UnLoad（实际存在的重载）；全程序集每处 AssetBundle.Unload、Resources.UnloadUnusedAssets 调用前后，以及 SceneManager.ChangeScene 前。异常退出没有 EXIT 是预期定位信号。AFTER 仅位于调用正常返回后的顺序路径，不重定向原有跳转到 AFTER。

工具扩宽短跳转；入口/退出/BEFORE 插入时重定向原分支与 EH 边界以保留原指令所属保护区。写出后重新读取全部原方法，去除标记对，比较全部原指令与操作数、标准化跳转目标和异常区域边界，任一差异即失败。该结构验证不等于 Unity Android 运行验证；托管捕获不能阻止 native 崩溃，日志可能改变时序。候选不能直接称为修复。

测试产物位于 /tmp（不应提交包含原程序集的二进制）：settlement-stage-patch.exe、settlement-diagnostic-candidate.dll、settlement-stage-verification.txt、settlement-duplicate-error.txt。共注入79处标记，结构回读通过；重复运行拒绝通过。没有生成 APK、签名、安装或部署。

复核：Exception 使用原程序集 corelib 作用域，回读检查 AssemblyReferences FullName 序列不变，禁止新增宿主 .NET 4 依赖；短分支仅按操作数种类规范化。预留原最大栈+1，Cecil 写出时重算 maxstack。
