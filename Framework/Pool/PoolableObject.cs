using UnityEngine;

/// <summary>
/// 池化对象基类
/// 为需要池化的对象提供基础实现
/// </summary>
public abstract class PoolableObject : MonoBehaviour, IPoolable
{
    [Header("池化设置")]
    [SerializeField] protected float autoReturnTime = -1f; // 自动归还时间，-1表示不自动归还
    [SerializeField] protected bool resetOnGet = true; // 获取时是否重置状态
    [SerializeField] protected bool enableDebugLog = false;

    // 状态
    protected bool _isInPool = true;
    protected float _activeTime = 0f;
    protected string _poolName;

    // 属性
    public bool IsInPool => _isInPool;
    public float ActiveTime => _activeTime;
    public string PoolName => _poolName;

    #region Unity生命周期

    protected virtual void Update()
    {
        if (!_isInPool)
        {
            _activeTime += Time.deltaTime;
            
            // 自动归还
            if (autoReturnTime > 0 && _activeTime >= autoReturnTime)
            {
                ReturnToPool();
            }
        }
    }

    #endregion

    #region 池化接口实现

    /// <summary>
    /// 从池中获取时调用
    /// </summary>
    public virtual void OnGetFromPool()
    {
        _isInPool = false;
        _activeTime = 0f;

        if (resetOnGet)
        {
            ResetState();
        }

        OnActivated();

        LogMessage("对象从池中获取");
    }

    /// <summary>
    /// 归还到池中时调用
    /// </summary>
    public virtual void OnReturnToPool()
    {
        _isInPool = true;
        _activeTime = 0f;

        OnDeactivated();

        LogMessage("对象归还到池中");
    }

    #endregion

    #region 虚方法

    /// <summary>
    /// 对象激活时调用（子类重写）
    /// </summary>
    protected virtual void OnActivated()
    {
        // 子类实现具体的激活逻辑
    }

    /// <summary>
    /// 对象停用时调用（子类重写）
    /// </summary>
    protected virtual void OnDeactivated()
    {
        // 子类实现具体的停用逻辑
    }

    /// <summary>
    /// 重置对象状态（子类重写）
    /// </summary>
    protected virtual void ResetState()
    {
        // 重置Transform
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        // 停止所有协程
        StopAllCoroutines();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 手动归还到池中
    /// </summary>
    public void ReturnToPool()
    {
        if (_isInPool) return;

        PoolManager.Instance?.ReturnToPool(gameObject);
    }

    /// <summary>
    /// 设置自动归还时间
    /// </summary>
    /// <param name="time">归还时间</param>
    public void SetAutoReturnTime(float time)
    {
        autoReturnTime = time;
        LogMessage($"设置自动归还时间: {time}");
    }

    /// <summary>
    /// 延迟归还到池中
    /// </summary>
    /// <param name="delay">延迟时间</param>
    public void ReturnToPoolDelayed(float delay)
    {
        if (delay <= 0)
        {
            ReturnToPool();
        }
        else
        {
            Invoke(nameof(ReturnToPool), delay);
        }
    }

    #endregion

    #region 静态便利方法

    /// <summary>
    /// 从池中获取对象的静态方法
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="poolName">池名称</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <returns>获取的对象</returns>
    public static T GetFromPool<T>(string poolName, Vector3 position = default, Quaternion rotation = default) where T : PoolableObject
    {
        return PoolManager.Instance?.GetFromPool<T>(poolName, position, rotation);
    }

    /// <summary>
    /// 创建并获取池化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="poolName">池名称</param>
    /// <param name="prefab">预制件</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <param name="poolSize">池大小</param>
    /// <returns>获取的对象</returns>
    public static T Spawn<T>(string poolName, GameObject prefab, Vector3 position = default, 
                           Quaternion rotation = default, int poolSize = 10) where T : PoolableObject
    {
        PoolManager poolManager = PoolManager.Instance;
        
        if (!poolManager.HasPool(poolName))
        {
            poolManager.CreatePool(poolName, prefab, poolSize);
        }
        
        return poolManager.GetFromPool<T>(poolName, position, rotation);
    }

    #endregion

    #region 日志方法

    protected void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[{GetType().Name}] {message}");
        }
    }

    #endregion
}