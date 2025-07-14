using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 对象池管理器
/// 负责管理游戏中的对象池，提高性能和减少GC压力
/// 支持预制件池、组件池和泛型对象池
/// </summary>
public class PoolManager : MonoBehaviour
{
    [Header("池设置")]
    [SerializeField] private int defaultPoolSize = 10;
    [SerializeField] private int maxPoolSize = 100;
    [SerializeField] private bool autoExpand = true;
    [SerializeField] private bool enableAutoCleanup = true;
    [SerializeField] private float cleanupInterval = 30f;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showPoolStatistics = false;

    // 单例实例
    private static PoolManager _instance;
    public static PoolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PoolManager");
                _instance = go.AddComponent<PoolManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 对象池字典
    private Dictionary<string, ObjectPool> _pools = new Dictionary<string, ObjectPool>();
    private Dictionary<GameObject, string> _pooledObjects = new Dictionary<GameObject, string>();
    
    // 统计信息
    private Dictionary<string, PoolStatistics> _statistics = new Dictionary<string, PoolStatistics>();
    
    // 清理计时器
    private float _cleanupTimer = 0f;

    // 属性
    public bool IsInitialized { get; private set; } = false;
    public int PoolCount => _pools.Count;
    public int TotalPooledObjects => _pooledObjects.Count;

    #region 初始化

    /// <summary>
    /// 异步初始化对象池管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("对象池管理器已经初始化");
            return;
        }

        LogMessage("初始化对象池管理器...");

        // 创建根节点
        CreatePoolRootObjects();

        IsInitialized = true;
        await UniTask.Yield();
        
        LogMessage("对象池管理器初始化完成");
    }

    /// <summary>
    /// 创建池根节点
    /// </summary>
    private void CreatePoolRootObjects()
    {
        // 为每个池创建父节点，便于管理
        GameObject poolRoot = new GameObject("Pools");
        poolRoot.transform.SetParent(transform);
    }

    #endregion

    #region 池管理

    /// <summary>
    /// 创建对象池
    /// </summary>
    /// <param name="poolName">池名称</param>
    /// <param name="prefab">预制件</param>
    /// <param name="initialSize">初始大小</param>
    /// <param name="maxSize">最大大小</param>
    /// <returns>是否创建成功</returns>
    public bool CreatePool(string poolName, GameObject prefab, int initialSize = -1, int maxSize = -1)
    {
        if (string.IsNullOrEmpty(poolName) || prefab == null)
        {
            LogError("池名称或预制件为空");
            return false;
        }

        if (_pools.ContainsKey(poolName))
        {
            LogWarning($"池已存在: {poolName}");
            return false;
        }

        if (initialSize < 0) initialSize = defaultPoolSize;
        if (maxSize < 0) maxSize = maxPoolSize;

        // 创建池容器
        GameObject poolContainer = new GameObject($"Pool_{poolName}");
        poolContainer.transform.SetParent(transform);

        // 创建对象池
        ObjectPool pool = new ObjectPool(prefab, poolContainer.transform, initialSize, maxSize, autoExpand);
        _pools[poolName] = pool;

        // 初始化统计信息
        _statistics[poolName] = new PoolStatistics();

        LogMessage($"创建对象池: {poolName}, 初始大小: {initialSize}, 最大大小: {maxSize}");
        return true;
    }

    /// <summary>
    /// 销毁对象池
    /// </summary>
    /// <param name="poolName">池名称</param>
    public void DestroyPool(string poolName)
    {
        if (!_pools.ContainsKey(poolName)) return;

        ObjectPool pool = _pools[poolName];
        
        // 清理池中的所有对象
        pool.ClearPool();
        
        // 销毁池容器
        if (pool.PoolContainer != null)
        {
            DestroyImmediate(pool.PoolContainer.gameObject);
        }

        _pools.Remove(poolName);
        _statistics.Remove(poolName);

        LogMessage($"销毁对象池: {poolName}");
    }

    /// <summary>
    /// 检查池是否存在
    /// </summary>
    /// <param name="poolName">池名称</param>
    /// <returns>是否存在</returns>
    public bool HasPool(string poolName)
    {
        return _pools.ContainsKey(poolName);
    }

    #endregion

    #region 对象获取和归还

    /// <summary>
    /// 从池中获取对象
    /// </summary>
    /// <param name="poolName">池名称</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <returns>获取的对象</returns>
    public GameObject GetFromPool(string poolName, Vector3 position = default, Quaternion rotation = default)
    {
        if (!_pools.ContainsKey(poolName))
        {
            LogError($"池不存在: {poolName}");
            return null;
        }

        ObjectPool pool = _pools[poolName];
        GameObject obj = pool.Get();

        if (obj != null)
        {
            // 设置位置和旋转
            obj.transform.position = position;
            obj.transform.rotation = rotation == default ? Quaternion.identity : rotation;
            
            // 激活对象
            obj.SetActive(true);
            
            // 记录对象归属
            _pooledObjects[obj] = poolName;
            
            // 更新统计
            _statistics[poolName].GetCount++;
            _statistics[poolName].ActiveCount++;

            LogMessage($"从池获取对象: {poolName}");
        }

        return obj;
    }

    /// <summary>
    /// 归还对象到池中
    /// </summary>
    /// <param name="obj">要归还的对象</param>
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        if (!_pooledObjects.ContainsKey(obj))
        {
            LogWarning($"对象不属于任何池: {obj.name}");
            return;
        }

        string poolName = _pooledObjects[obj];
        if (!_pools.ContainsKey(poolName))
        {
            LogError($"目标池不存在: {poolName}");
            return;
        }

        ObjectPool pool = _pools[poolName];
        
        // 停用对象
        obj.SetActive(false);
        
        // 重置对象状态
        ResetPooledObject(obj);
        
        // 归还到池
        pool.Return(obj);
        
        // 移除记录
        _pooledObjects.Remove(obj);
        
        // 更新统计
        _statistics[poolName].ReturnCount++;
        _statistics[poolName].ActiveCount--;

        LogMessage($"归还对象到池: {poolName}");
    }

    /// <summary>
    /// 获取带组件的对象
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="poolName">池名称</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <returns>组件</returns>
    public T GetFromPool<T>(string poolName, Vector3 position = default, Quaternion rotation = default) where T : Component
    {
        GameObject obj = GetFromPool(poolName, position, rotation);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    /// <summary>
    /// 归还带组件的对象
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="component">组件</param>
    public void ReturnToPool<T>(T component) where T : Component
    {
        if (component != null)
        {
            ReturnToPool(component.gameObject);
        }
    }

    #endregion

    #region 预加载

    /// <summary>
    /// 预加载池对象
    /// </summary>
    /// <param name="poolName">池名称</param>
    /// <param name="count">预加载数量</param>
    public async UniTask PreloadPool(string poolName, int count)
    {
        if (!_pools.ContainsKey(poolName))
        {
            LogError($"池不存在: {poolName}");
            return;
        }

        ObjectPool pool = _pools[poolName];
        await pool.PreloadAsync(count);

        LogMessage($"预加载池对象: {poolName}, 数量: {count}");
    }

    /// <summary>
    /// 预加载多个池
    /// </summary>
    /// <param name="poolData">池数据列表</param>
    public async UniTask PreloadPools(List<PoolPreloadData> poolData)
    {
        if (poolData == null || poolData.Count == 0) return;

        List<UniTask> preloadTasks = new List<UniTask>();

        foreach (var data in poolData)
        {
            if (HasPool(data.poolName))
            {
                preloadTasks.Add(PreloadPool(data.poolName, data.preloadCount));
            }
        }

        await UniTask.WhenAll(preloadTasks);
        LogMessage($"预加载完成，共 {poolData.Count} 个池");
    }

    #endregion

    #region 清理和维护

    /// <summary>
    /// 清理所有池
    /// </summary>
    public void ClearAll()
    {
        foreach (var pool in _pools.Values)
        {
            pool.ClearPool();
        }

        _pooledObjects.Clear();
        
        // 重置统计
        foreach (var stat in _statistics.Values)
        {
            stat.Reset();
        }

        LogMessage("清理所有对象池");
    }

    /// <summary>
    /// 清理指定池
    /// </summary>
    /// <param name="poolName">池名称</param>
    public void ClearPool(string poolName)
    {
        if (!_pools.ContainsKey(poolName)) return;

        ObjectPool pool = _pools[poolName];
        pool.ClearPool();

        // 移除相关的池对象记录
        List<GameObject> toRemove = new List<GameObject>();
        foreach (var kvp in _pooledObjects)
        {
            if (kvp.Value == poolName)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (GameObject obj in toRemove)
        {
            _pooledObjects.Remove(obj);
        }

        // 重置统计
        if (_statistics.ContainsKey(poolName))
        {
            _statistics[poolName].Reset();
        }

        LogMessage($"清理对象池: {poolName}");
    }

    /// <summary>
    /// 自动清理未使用的对象
    /// </summary>
    private void AutoCleanup()
    {
        foreach (var kvp in _pools)
        {
            string poolName = kvp.Key;
            ObjectPool pool = kvp.Value;
            
            int cleaned = pool.CleanupUnused();
            if (cleaned > 0)
            {
                LogMessage($"自动清理池 {poolName}: 清理了 {cleaned} 个未使用对象");
            }
        }
    }

    #endregion

    #region 对象重置

    /// <summary>
    /// 重置池对象状态
    /// </summary>
    /// <param name="obj">要重置的对象</param>
    private void ResetPooledObject(GameObject obj)
    {
        // 重置Transform
        obj.transform.localScale = Vector3.one;
        obj.transform.localRotation = Quaternion.identity;

        // 重置Rigidbody2D
        Rigidbody2D rb2d = obj.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        // 重置可池化对象
        IPoolable poolable = obj.GetComponent<IPoolable>();
        if (poolable != null)
        {
            poolable.OnReturnToPool();
        }

        // 停止所有协程
        MonoBehaviour[] monos = obj.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour mono in monos)
        {
            if (mono != null)
            {
                mono.StopAllCoroutines();
            }
        }
    }

    #endregion

    #region 统计信息

    /// <summary>
    /// 获取池统计信息
    /// </summary>
    /// <param name="poolName">池名称</param>
    /// <returns>统计信息</returns>
    public PoolStatistics GetPoolStatistics(string poolName)
    {
        return _statistics.ContainsKey(poolName) ? _statistics[poolName] : null;
    }

    /// <summary>
    /// 获取所有池的统计信息
    /// </summary>
    /// <returns>统计信息字典</returns>
    public Dictionary<string, PoolStatistics> GetAllStatistics()
    {
        return new Dictionary<string, PoolStatistics>(_statistics);
    }

    /// <summary>
    /// 打印池统计信息
    /// </summary>
    public void PrintStatistics()
    {
        if (!enableDebugLog) return;

        LogMessage("=== 对象池统计信息 ===");
        foreach (var kvp in _statistics)
        {
            string poolName = kvp.Key;
            PoolStatistics stat = kvp.Value;
            ObjectPool pool = _pools[poolName];
            
            LogMessage($"池: {poolName}");
            LogMessage($"  活跃对象: {stat.ActiveCount}");
            LogMessage($"  池大小: {pool.AvailableCount}");
            LogMessage($"  获取次数: {stat.GetCount}");
            LogMessage($"  归还次数: {stat.ReturnCount}");
        }
        LogMessage("===================");
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        if (!enableAutoCleanup) return;

        _cleanupTimer += Time.deltaTime;
        if (_cleanupTimer >= cleanupInterval)
        {
            _cleanupTimer = 0f;
            AutoCleanup();
        }

        // 显示统计信息
        if (showPoolStatistics && Time.frameCount % 300 == 0) // 每5秒显示一次
        {
            PrintStatistics();
        }
    }

    /// <summary>
    /// 清理管理器
    /// </summary>
    public void Cleanup()
    {
        ClearAll();
        
        // 销毁所有池容器
        foreach (var pool in _pools.Values)
        {
            if (pool.PoolContainer != null)
            {
                DestroyImmediate(pool.PoolContainer.gameObject);
            }
        }
        
        _pools.Clear();
        _statistics.Clear();
        
        LogMessage("对象池管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[PoolManager] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (enableDebugLog)
        {
            Debug.LogWarning($"[PoolManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[PoolManager] {message}");
    }

    #endregion
}

/// <summary>
/// 池预加载数据
/// </summary>
[System.Serializable]
public class PoolPreloadData
{
    public string poolName;
    public int preloadCount;
}

/// <summary>
/// 池统计信息
/// </summary>
[System.Serializable]
public class PoolStatistics
{
    public int GetCount { get; set; } = 0;
    public int ReturnCount { get; set; } = 0;
    public int ActiveCount { get; set; } = 0;

    public void Reset()
    {
        GetCount = 0;
        ReturnCount = 0;
        ActiveCount = 0;
    }
}