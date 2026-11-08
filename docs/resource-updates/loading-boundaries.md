# 当前 APK 资源加载边界

原 Assembly-CSharp SHA256：c83aea19ee65ae973815daf6a04e430d9e87b8426b6180c1fe507e4260763ecc。REA selected types evidence：ev_2bbacbd2a8875c0ce21762c4889d0c084fffd8e62fb35cbda73378d06147679e；另对完整程序集 monodis CIL 搜索核对调用。

三个直接 LoadFromCacheOrDownload 调用：AssetBundleLoader.Update（基础下载）、AssetBundleLoader/<LoadAssetBundle_Single>c__Iterator4F.MoveNext（单基础包）、GameManager/<LoadEnemyAssetBundle>c__Iterator7.MoveNext（关卡敌人）。这些并非三个独立事务边界。

统一关卡入口 LevelLoader.InitMap 返回迭代器，MoveNext 顺序启动 LoadAudioAssetBundle、LoadPlayerAssetBundle、LoadEnemyAssetBundle。门闩应包裹 InitMap 迭代器，在原首次 MoveNext 前完成完整校验；不能只包敌人协程，否则音频/玩家已使用旧版本。启动入口 DownloadStart 使用 Update 状态机，需前置异步启动器；单基础包协程需区分操作内调用和独立调用。

AssetBundleLoader.m_BasicBundleList 持有基础内存包。AssetBundleCheck 仅检查 dictionary ContainsKey，更新后必须刷新该引用。UnloadAsset(key, allDelete) 会卸载并移除；UnloadAsset_PlayerUnit 仅卸载编号 1 以上，音频 0 常驻。GameManager.m_EnemyAssetList 持有敌人包；ClearAssetBundleData 使用 Unload(true)，并清空 prefab 列表、卸载玩家包，不能在旧关卡仍有引用时直接复用。

diagnostics/resource-updates/Probe.cs 为独立标记触发候选，测试同 CAB 并存、Unload(false) 后旧对象保留、新加载与旧文件恢复；不默认接入发布构建。结果待设备运行，不把编译成功当作引擎验证。
