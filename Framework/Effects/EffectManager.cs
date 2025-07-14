using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 特效管理器
/// 管理游戏中的粒子特效、视觉特效等
/// 提供特效播放、池化管理和性能优化
/// </summary>
public class EffectManager : MonoBehaviour
{
    [Header("特效设置")]
    [SerializeField] private int maxActiveEffects = 50;
    [SerializeField] private bool enablePooling = true;
    [SerializeField] private int defaultPoolSize = 5;
    
    [Header("性能设置")]
    [SerializeField] private float cullingDistance = 20f;
    [SerializeField] private bool enableLOD = true;
    [SerializeField] private float lodDistance = 10f;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showEffectCount = false;

    // 单例实例
    private static EffectManager _instance;
    public static EffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("EffectManager");
                _instance = go.AddComponent<EffectManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 特效管理
    private Dictionary<string, GameObject> _effectPrefabs = new Dictionary<string, GameObject>();
    private List<EffectInstance> _activeEffects = new List<EffectInstance>();
    private PoolManager _poolManager;
    private Camera _mainCamera;

    // 属性
    public int ActiveEffectCount => _activeEffects.Count;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化特效管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("特效管理器已经初始化");
            return;
        }

        LogMessage("初始化特效管理器...");

        // 获取依赖组件
        _poolManager = PoolManager.Instance;
        _mainCamera = Camera.main;

        // 加载特效预制件
        await LoadEffectPrefabs();

        IsInitialized = true;
        LogMessage("特效管理器初始化完成");
    }

    /// <summary>
    /// 加载特效预制件
    /// </summary>
    private async UniTask LoadEffectPrefabs()
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Effects");
        
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
            {
                _effectPrefabs[prefab.name] = prefab;
                
                // 如果启用池化，创建对象池
                if (enablePooling && _poolManager != null)
                {
                    _poolManager.CreatePool($"Effect_{prefab.name}", prefab, defaultPoolSize);
                }
                
                LogMessage($"加载特效预制件: {prefab.name}");
            }
        }

        await UniTask.Yield();
        LogMessage($"共加载了{_effectPrefabs.Count}个特效预制件");
    }

    #endregion

    #region 特效播放

    /// <summary>
    /// 播放特效
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="position">播放位置</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="scale">缩放</param>
    /// <param name="autoDestroy">是否自动销毁</param>
    /// <param name="duration">持续时间（自动销毁时使用）</param>
    /// <returns>特效实例</returns>
    public EffectInstance PlayEffect(string effectName, Vector3 position, 
                                   Quaternion rotation = default, Vector3 scale = default,
                                   bool autoDestroy = true, float duration = -1f)
    {
        if (!_effectPrefabs.ContainsKey(effectName))
        {
            LogError($"找不到特效: {effectName}");
            return null;
        }

        // 检查特效数量限制
        if (_activeEffects.Count >= maxActiveEffects)
        {
            RemoveOldestEffect();
        }

        GameObject effectObject = null;

        // 从池中获取或创建特效
        if (enablePooling && _poolManager != null)
        {
            effectObject = _poolManager.GetFromPool($"Effect_{effectName}", position, rotation);
        }
        else
        {
            GameObject prefab = _effectPrefabs[effectName];
            effectObject = Instantiate(prefab, position, rotation);
        }

        if (effectObject == null)
        {
            LogError($"无法创建特效: {effectName}");
            return null;
        }

        // 设置缩放
        if (scale != default)
        {
            effectObject.transform.localScale = scale;
        }
        else if (effectObject.transform.localScale == Vector3.zero)
        {
            effectObject.transform.localScale = Vector3.one;
        }

        // 创建特效实例
        EffectInstance instance = new EffectInstance(effectObject, effectName, autoDestroy);
        _activeEffects.Add(instance);

        // 自动销毁设置
        if (autoDestroy)
        {
            if (duration > 0)
            {
                instance.SetDuration(duration);
            }
            else
            {
                // 尝试从粒子系统获取持续时间
                ParticleSystem ps = effectObject.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    instance.SetDuration(ps.main.duration + ps.main.startLifetime.constantMax);
                }
                else
                {
                    instance.SetDuration(5f); // 默认5秒
                }
            }
        }

        LogMessage($"播放特效: {effectName} at {position}");
        return instance;
    }

    /// <summary>
    /// 播放特效（简化版本）
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="position">播放位置</param>
    /// <returns>特效实例</returns>
    public EffectInstance PlayEffect(string effectName, Vector3 position)
    {
        return PlayEffect(effectName, position, Quaternion.identity, Vector3.one, true);
    }

    /// <summary>
    /// 在目标对象上播放特效
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="target">目标对象</param>
    /// <param name="offset">位置偏移</param>
    /// <param name="followTarget">是否跟随目标</param>
    /// <param name="autoDestroy">是否自动销毁</param>
    /// <returns>特效实例</returns>
    public EffectInstance PlayEffectOnTarget(string effectName, Transform target, 
                                           Vector3 offset = default, bool followTarget = false,
                                           bool autoDestroy = true)
    {
        if (target == null)
        {
            LogError("目标对象为空");
            return null;
        }

        Vector3 position = target.position + offset;
        EffectInstance instance = PlayEffect(effectName, position, target.rotation, Vector3.one, autoDestroy);

        if (instance != null && followTarget)
        {
            instance.SetFollowTarget(target, offset);
        }

        return instance;
    }

    /// <summary>
    /// 播放循环特效
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="position">播放位置</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>特效实例</returns>
    public EffectInstance PlayLoopingEffect(string effectName, Vector3 position, Quaternion rotation = default)
    {
        return PlayEffect(effectName, position, rotation, Vector3.one, false);
    }

    #endregion

    #region 特效控制

    /// <summary>
    /// 停止特效
    /// </summary>
    /// <param name="instance">特效实例</param>
    public void StopEffect(EffectInstance instance)
    {
        if (instance == null || !_activeEffects.Contains(instance)) return;

        instance.Stop();
        RemoveEffect(instance);
        LogMessage($"停止特效: {instance.EffectName}");
    }

    /// <summary>
    /// 停止所有指定名称的特效
    /// </summary>
    /// <param name="effectName">特效名称</param>
    public void StopAllEffects(string effectName)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i].EffectName == effectName)
            {
                StopEffect(_activeEffects[i]);
            }
        }
    }

    /// <summary>
    /// 停止所有特效
    /// </summary>
    public void StopAllEffects()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            StopEffect(_activeEffects[i]);
        }
        
        LogMessage("停止所有特效");
    }

    /// <summary>
    /// 暂停特效
    /// </summary>
    /// <param name="instance">特效实例</param>
    public void PauseEffect(EffectInstance instance)
    {
        if (instance != null)
        {
            instance.Pause();
        }
    }

    /// <summary>
    /// 恢复特效
    /// </summary>
    /// <param name="instance">特效实例</param>
    public void ResumeEffect(EffectInstance instance)
    {
        if (instance != null)
        {
            instance.Resume();
        }
    }

    #endregion

    #region 特效管理

    /// <summary>
    /// 移除最旧的特效
    /// </summary>
    private void RemoveOldestEffect()
    {
        if (_activeEffects.Count > 0)
        {
            EffectInstance oldest = _activeEffects[0];
            StopEffect(oldest);
        }
    }

    /// <summary>
    /// 移除特效
    /// </summary>
    /// <param name="instance">特效实例</param>
    private void RemoveEffect(EffectInstance instance)
    {
        if (instance == null) return;

        _activeEffects.Remove(instance);

        // 归还到池中或销毁
        if (enablePooling && _poolManager != null)
        {
            _poolManager.ReturnToPool(instance.GameObject);
        }
        else if (instance.GameObject != null)
        {
            Destroy(instance.GameObject);
        }
    }

    /// <summary>
    /// 清理已完成的特效
    /// </summary>
    private void CleanupFinishedEffects()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            EffectInstance effect = _activeEffects[i];
            
            if (effect.IsFinished)
            {
                RemoveEffect(effect);
            }
        }
    }

    #endregion

    #region 性能优化

    /// <summary>
    /// 执行距离剔除
    /// </summary>
    private void PerformCulling()
    {
        if (_mainCamera == null) return;

        Vector3 cameraPos = _mainCamera.transform.position;

        foreach (EffectInstance effect in _activeEffects)
        {
            if (effect.GameObject == null) continue;

            float distance = Vector3.Distance(effect.GameObject.transform.position, cameraPos);
            
            // 距离剔除
            bool shouldBeActive = distance <= cullingDistance;
            if (effect.GameObject.activeSelf != shouldBeActive)
            {
                effect.GameObject.SetActive(shouldBeActive);
            }

            // LOD处理
            if (enableLOD && shouldBeActive)
            {
                float lodFactor = Mathf.Clamp01(1f - (distance / lodDistance));
                ApplyLOD(effect, lodFactor);
            }
        }
    }

    /// <summary>
    /// 应用LOD
    /// </summary>
    /// <param name="effect">特效实例</param>
    /// <param name="lodFactor">LOD因子</param>
    private void ApplyLOD(EffectInstance effect, float lodFactor)
    {
        ParticleSystem ps = effect.GameObject.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var emission = ps.emission;
            float originalRate = effect.GetOriginalEmissionRate();
            emission.rateOverTime = originalRate * lodFactor;
        }
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新
    /// </summary>
    public void Update()
    {
        if (!IsInitialized) return;

        // 更新特效实例
        foreach (EffectInstance effect in _activeEffects)
        {
            effect.Update();
        }

        // 清理已完成的特效
        CleanupFinishedEffects();

        // 性能优化
        if (Time.frameCount % 10 == 0) // 每10帧执行一次
        {
            PerformCulling();
        }

        // 显示统计信息
        if (showEffectCount && Time.frameCount % 60 == 0) // 每秒显示一次
        {
            LogMessage($"活跃特效数量: {_activeEffects.Count}");
        }
    }

    /// <summary>
    /// 清理特效管理器
    /// </summary>
    public void Cleanup()
    {
        StopAllEffects();
        _effectPrefabs.Clear();
        LogMessage("特效管理器已清理");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 预加载特效
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="count">预加载数量</param>
    public async UniTask PreloadEffect(string effectName, int count = 5)
    {
        if (enablePooling && _poolManager != null)
        {
            await _poolManager.PreloadPool($"Effect_{effectName}", count);
            LogMessage($"预加载特效: {effectName}, 数量: {count}");
        }
    }

    /// <summary>
    /// 检查特效是否存在
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <returns>是否存在</returns>
    public bool HasEffect(string effectName)
    {
        return _effectPrefabs.ContainsKey(effectName);
    }

    /// <summary>
    /// 注册特效预制件
    /// </summary>
    /// <param name="effectName">特效名称</param>
    /// <param name="prefab">预制件</param>
    public void RegisterEffect(string effectName, GameObject prefab)
    {
        if (prefab == null) return;

        _effectPrefabs[effectName] = prefab;

        if (enablePooling && _poolManager != null)
        {
            _poolManager.CreatePool($"Effect_{effectName}", prefab, defaultPoolSize);
        }

        LogMessage($"注册特效: {effectName}");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[EffectManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[EffectManager] {message}");
    }

    #endregion
}