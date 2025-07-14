using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 对象池类
/// 管理单个预制件的对象池，提供对象的获取、归还和管理功能
/// </summary>
public class ObjectPool
{
    private GameObject _prefab;
    private Transform _poolContainer;
    private Queue<GameObject> _availableObjects;
    private List<GameObject> _allObjects;
    private int _maxSize;
    private bool _autoExpand;

    // 属性
    public GameObject Prefab => _prefab;
    public Transform PoolContainer => _poolContainer;
    public int AvailableCount => _availableObjects.Count;
    public int TotalCount => _allObjects.Count;
    public int ActiveCount => _allObjects.Count - _availableObjects.Count;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="prefab">预制件</param>
    /// <param name="container">池容器</param>
    /// <param name="initialSize">初始大小</param>
    /// <param name="maxSize">最大大小</param>
    /// <param name="autoExpand">是否自动扩展</param>
    public ObjectPool(GameObject prefab, Transform container, int initialSize, int maxSize, bool autoExpand)
    {
        _prefab = prefab;
        _poolContainer = container;
        _maxSize = maxSize;
        _autoExpand = autoExpand;
        
        _availableObjects = new Queue<GameObject>();
        _allObjects = new List<GameObject>();

        // 预创建对象
        for (int i = 0; i < initialSize; i++)
        {
            CreateObject();
        }
    }

    /// <summary>
    /// 获取对象
    /// </summary>
    /// <returns>对象</returns>
    public GameObject Get()
    {
        GameObject obj = null;

        if (_availableObjects.Count > 0)
        {
            obj = _availableObjects.Dequeue();
        }
        else if (_autoExpand && _allObjects.Count < _maxSize)
        {
            obj = CreateObject();
        }

        if (obj != null)
        {
            // 通知对象从池中获取
            IPoolable poolable = obj.GetComponent<IPoolable>();
            if (poolable != null)
            {
                poolable.OnGetFromPool();
            }
        }

        return obj;
    }

    /// <summary>
    /// 归还对象
    /// </summary>
    /// <param name="obj">要归还的对象</param>
    public void Return(GameObject obj)
    {
        if (obj == null || _availableObjects.Contains(obj)) return;

        // 设置父级
        obj.transform.SetParent(_poolContainer);
        
        // 归还到队列
        _availableObjects.Enqueue(obj);
    }

    /// <summary>
    /// 创建对象
    /// </summary>
    /// <returns>创建的对象</returns>
    private GameObject CreateObject()
    {
        GameObject obj = Object.Instantiate(_prefab, _poolContainer);
        obj.SetActive(false);
        
        _allObjects.Add(obj);
        _availableObjects.Enqueue(obj);
        
        return obj;
    }

    /// <summary>
    /// 异步预加载
    /// </summary>
    /// <param name="count">预加载数量</param>
    public async UniTask PreloadAsync(int count)
    {
        int targetCount = Mathf.Min(_maxSize, _allObjects.Count + count);
        
        for (int i = _allObjects.Count; i < targetCount; i++)
        {
            CreateObject();
            
            // 每创建10个对象就让渡一帧
            if (i % 10 == 0)
            {
                await UniTask.Yield();
            }
        }
    }

    /// <summary>
    /// 清理池
    /// </summary>
    public void ClearPool()
    {
        foreach (GameObject obj in _allObjects)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }

        _allObjects.Clear();
        _availableObjects.Clear();
    }

    /// <summary>
    /// 清理未使用的对象
    /// </summary>
    /// <returns>清理的对象数量</returns>
    public int CleanupUnused()
    {
        int cleanedCount = 0;
        int keepCount = Mathf.Max(5, _maxSize / 4); // 保留最大容量的1/4或至少5个

        while (_availableObjects.Count > keepCount)
        {
            GameObject obj = _availableObjects.Dequeue();
            if (obj != null)
            {
                _allObjects.Remove(obj);
                Object.DestroyImmediate(obj);
                cleanedCount++;
            }
        }

        return cleanedCount;
    }
}

/// <summary>
/// 可池化对象接口
/// 实现此接口的对象可以在池化时接收通知
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 从池中获取时调用
    /// </summary>
    void OnGetFromPool();

    /// <summary>
    /// 归还到池中时调用
    /// </summary>
    void OnReturnToPool();
}