using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 补间动画管理器
/// 统一管理游戏中的所有DOTween动画，提供便捷的动画播放、暂停、停止等功能
/// 支持动画分组管理、性能优化和内存管理
/// </summary>
public class TweenManager : MonoBehaviour
{
    [Header("补间设置")]
    [SerializeField] private int maxTweens = 500;
    [SerializeField] private int maxSequences = 50;
    [SerializeField] private bool enableAutoKill = true;
    [SerializeField] private bool enableRecycling = true;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showTweenCount = false;

    // 单例实例
    private static TweenManager _instance;
    public static TweenManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TweenManager");
                _instance = go.AddComponent<TweenManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 动画管理
    private Dictionary<string, List<Tween>> _tweenGroups = new Dictionary<string, List<Tween>>();
    private Dictionary<string, Sequence> _sequences = new Dictionary<string, Sequence>();
    private List<Tween> _allTweens = new List<Tween>();
    
    // 统计信息
    private int _activeTweenCount = 0;
    private int _activeSequenceCount = 0;

    // 属性
    public int ActiveTweenCount => _activeTweenCount;
    public int ActiveSequenceCount => _activeSequenceCount;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化补间管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("补间管理器已经初始化");
            return;
        }

        LogMessage("初始化补间管理器...");

        // 配置DOTween
        ConfigureDOTween();

        IsInitialized = true;
        await UniTask.Yield();
        
        LogMessage("补间管理器初始化完成");
    }

    /// <summary>
    /// 配置DOTween设置
    /// </summary>
    private void ConfigureDOTween()
    {
        // 设置容量
        DOTween.SetTweensCapacity(maxTweens, maxSequences);
        
        // 设置自动杀死和回收
        DOTween.defaultAutoKill = enableAutoKill;
        DOTween.defaultAutoPlay = true;
        DOTween.defaultRecyclable = enableRecycling;
        
        // 设置更新模式
        DOTween.defaultUpdateType = UpdateType.Normal;
        DOTween.defaultTimeScaleIndependent = false;

        LogMessage("DOTween配置完成");
    }

    #endregion

    #region 基础动画方法

    /// <summary>
    /// 移动动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoMove(Transform target, Vector3 targetPosition, float duration, 
                       Ease ease = Ease.OutQuad, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOMove(targetPosition, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建移动动画: {target.name} -> {targetPosition}");
        return tween;
    }

    /// <summary>
    /// 本地移动动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="targetPosition">目标本地位置</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoLocalMove(Transform target, Vector3 targetPosition, float duration, 
                            Ease ease = Ease.OutQuad, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOLocalMove(targetPosition, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建本地移动动画: {target.name} -> {targetPosition}");
        return tween;
    }

    /// <summary>
    /// 缩放动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="targetScale">目标缩放</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoScale(Transform target, Vector3 targetScale, float duration, 
                        Ease ease = Ease.OutBack, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOScale(targetScale, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建缩放动画: {target.name} -> {targetScale}");
        return tween;
    }

    /// <summary>
    /// 旋转动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="targetRotation">目标旋转</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoRotate(Transform target, Vector3 targetRotation, float duration, 
                         Ease ease = Ease.OutQuad, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DORotate(targetRotation, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建旋转动画: {target.name} -> {targetRotation}");
        return tween;
    }

    /// <summary>
    /// 渐变动画
    /// </summary>
    /// <param name="target">目标SpriteRenderer</param>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoFade(SpriteRenderer target, float targetAlpha, float duration, 
                       Ease ease = Ease.OutQuad, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOFade(targetAlpha, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建渐变动画: {target.name} -> Alpha:{targetAlpha}");
        return tween;
    }

    /// <summary>
    /// CanvasGroup渐变动画
    /// </summary>
    /// <param name="target">目标CanvasGroup</param>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">持续时间</param>
    /// <param name="ease">缓动类型</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoFade(CanvasGroup target, float targetAlpha, float duration, 
                       Ease ease = Ease.OutQuad, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOFade(targetAlpha, duration).SetEase(ease);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建CanvasGroup渐变动画: {target.name} -> Alpha:{targetAlpha}");
        return tween;
    }

    #endregion

    #region 复合动画

    /// <summary>
    /// 弹出动画（常用于UI）
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="duration">持续时间</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Sequence</returns>
    public Sequence DoPopup(Transform target, float duration = 0.5f, string groupName = null)
    {
        if (target == null) return null;

        Sequence sequence = DOTween.Sequence();
        
        // 初始状态
        target.localScale = Vector3.zero;
        
        // 动画序列
        sequence.Append(target.DOScale(Vector3.one * 1.1f, duration * 0.7f).SetEase(Ease.OutBack))
                .Append(target.DOScale(Vector3.one, duration * 0.3f).SetEase(Ease.InOutQuad));
        
        RegisterSequence(sequence, "Popup", groupName);
        
        LogMessage($"创建弹出动画: {target.name}");
        return sequence;
    }

    /// <summary>
    /// 摇摆动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="strength">摇摆强度</param>
    /// <param name="vibrato">震动次数</param>
    /// <param name="duration">持续时间</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoShake(Transform target, float strength = 1f, int vibrato = 10, 
                        float duration = 1f, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOShakePosition(duration, strength, vibrato);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建摇摆动画: {target.name}");
        return tween;
    }

    /// <summary>
    /// 跳跃动画
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="jumpPower">跳跃力度</param>
    /// <param name="numJumps">跳跃次数</param>
    /// <param name="duration">持续时间</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoJump(Transform target, Vector3 endValue, float jumpPower, int numJumps, 
                       float duration, string groupName = null)
    {
        if (target == null) return null;

        Tween tween = target.DOJump(endValue, jumpPower, numJumps, duration);
        RegisterTween(tween, groupName);
        
        LogMessage($"创建跳跃动画: {target.name}");
        return tween;
    }

    /// <summary>
    /// 脉冲动画（心跳效果）
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="scale">缩放倍数</param>
    /// <param name="duration">持续时间</param>
    /// <param name="loops">循环次数</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Tween</returns>
    public Tween DoPulse(Transform target, float scale = 1.2f, float duration = 1f, 
                        int loops = -1, string groupName = null)
    {
        if (target == null) return null;

        Vector3 originalScale = target.localScale;
        Vector3 targetScale = originalScale * scale;
        
        Tween tween = target.DOScale(targetScale, duration)
                           .SetEase(Ease.InOutSine)
                           .SetLoops(loops, LoopType.Yoyo);
        
        RegisterTween(tween, groupName);
        
        LogMessage($"创建脉冲动画: {target.name}");
        return tween;
    }

    #endregion

    #region 序列动画

    /// <summary>
    /// 创建序列动画
    /// </summary>
    /// <param name="sequenceName">序列名称</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Sequence</returns>
    public Sequence CreateSequence(string sequenceName, string groupName = null)
    {
        Sequence sequence = DOTween.Sequence();
        RegisterSequence(sequence, sequenceName, groupName);
        
        LogMessage($"创建序列动画: {sequenceName}");
        return sequence;
    }

    /// <summary>
    /// UI显示动画序列
    /// </summary>
    /// <param name="panel">UI面板</param>
    /// <param name="duration">持续时间</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Sequence</returns>
    public Sequence DoUIShow(Transform panel, float duration = 0.3f, string groupName = "UI")
    {
        if (panel == null) return null;

        Sequence sequence = DOTween.Sequence();
        
        // 获取组件
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        // 初始状态
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        panel.localScale = Vector3.one * 0.8f;
        
        // 动画序列
        sequence.OnStart(() => {
            panel.gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
        });
        
        if (canvasGroup != null)
        {
            sequence.Append(canvasGroup.DOFade(1f, duration));
        }
        
        sequence.Join(panel.DOScale(Vector3.one, duration).SetEase(Ease.OutBack));
        
        sequence.OnComplete(() => {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
            }
        });
        
        RegisterSequence(sequence, "UIShow", groupName);
        
        LogMessage($"创建UI显示动画: {panel.name}");
        return sequence;
    }

    /// <summary>
    /// UI隐藏动画序列
    /// </summary>
    /// <param name="panel">UI面板</param>
    /// <param name="duration">持续时间</param>
    /// <param name="groupName">分组名称</param>
    /// <returns>创建的Sequence</returns>
    public Sequence DoUIHide(Transform panel, float duration = 0.3f, string groupName = "UI")
    {
        if (panel == null) return null;

        Sequence sequence = DOTween.Sequence();
        
        // 获取组件
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        // 动画序列
        sequence.OnStart(() => {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        });
        
        if (canvasGroup != null)
        {
            sequence.Append(canvasGroup.DOFade(0f, duration));
        }
        
        sequence.Join(panel.DOScale(Vector3.one * 0.8f, duration).SetEase(Ease.InBack));
        
        sequence.OnComplete(() => {
            panel.gameObject.SetActive(false);
        });
        
        RegisterSequence(sequence, "UIHide", groupName);
        
        LogMessage($"创建UI隐藏动画: {panel.name}");
        return sequence;
    }

    #endregion

    #region 动画管理

    /// <summary>
    /// 注册Tween到管理器
    /// </summary>
    /// <param name="tween">要注册的Tween</param>
    /// <param name="groupName">分组名称</param>
    private void RegisterTween(Tween tween, string groupName = null)
    {
        if (tween == null) return;

        _allTweens.Add(tween);
        _activeTweenCount++;

        if (!string.IsNullOrEmpty(groupName))
        {
            if (!_tweenGroups.ContainsKey(groupName))
            {
                _tweenGroups[groupName] = new List<Tween>();
            }
            _tweenGroups[groupName].Add(tween);
        }

        // 添加完成回调来清理引用
        tween.OnComplete(() => {
            UnregisterTween(tween, groupName);
        });

        tween.OnKill(() => {
            UnregisterTween(tween, groupName);
        });
    }

    /// <summary>
    /// 注册Sequence到管理器
    /// </summary>
    /// <param name="sequence">要注册的Sequence</param>
    /// <param name="sequenceName">序列名称</param>
    /// <param name="groupName">分组名称</param>
    private void RegisterSequence(Sequence sequence, string sequenceName, string groupName = null)
    {
        if (sequence == null) return;

        _activeSequenceCount++;

        if (!string.IsNullOrEmpty(sequenceName))
        {
            _sequences[sequenceName] = sequence;
        }

        // 添加完成回调
        sequence.OnComplete(() => {
            UnregisterSequence(sequence, sequenceName);
        });

        sequence.OnKill(() => {
            UnregisterSequence(sequence, sequenceName);
        });
    }

    /// <summary>
    /// 注销Tween
    /// </summary>
    /// <param name="tween">要注销的Tween</param>
    /// <param name="groupName">分组名称</param>
    private void UnregisterTween(Tween tween, string groupName = null)
    {
        if (tween == null) return;

        _allTweens.Remove(tween);
        _activeTweenCount = Mathf.Max(0, _activeTweenCount - 1);

        if (!string.IsNullOrEmpty(groupName) && _tweenGroups.ContainsKey(groupName))
        {
            _tweenGroups[groupName].Remove(tween);
            
            // 如果分组为空，删除分组
            if (_tweenGroups[groupName].Count == 0)
            {
                _tweenGroups.Remove(groupName);
            }
        }
    }

    /// <summary>
    /// 注销Sequence
    /// </summary>
    /// <param name="sequence">要注销的Sequence</param>
    /// <param name="sequenceName">序列名称</param>
    private void UnregisterSequence(Sequence sequence, string sequenceName)
    {
        if (sequence == null) return;

        _activeSequenceCount = Mathf.Max(0, _activeSequenceCount - 1);

        if (!string.IsNullOrEmpty(sequenceName) && _sequences.ContainsKey(sequenceName))
        {
            _sequences.Remove(sequenceName);
        }
    }

    #endregion

    #region 控制方法

    /// <summary>
    /// 暂停所有动画
    /// </summary>
    public void PauseAll()
    {
        DOTween.PauseAll();
        LogMessage("暂停所有动画");
    }

    /// <summary>
    /// 恢复所有动画
    /// </summary>
    public void PlayAll()
    {
        DOTween.PlayAll();
        LogMessage("恢复所有动画");
    }

    /// <summary>
    /// 停止所有动画
    /// </summary>
    public void KillAll()
    {
        DOTween.KillAll();
        _allTweens.Clear();
        _tweenGroups.Clear();
        _sequences.Clear();
        _activeTweenCount = 0;
        _activeSequenceCount = 0;
        
        LogMessage("停止所有动画");
    }

    /// <summary>
    /// 暂停指定分组的动画
    /// </summary>
    /// <param name="groupName">分组名称</param>
    public void PauseGroup(string groupName)
    {
        if (!_tweenGroups.ContainsKey(groupName)) return;

        foreach (Tween tween in _tweenGroups[groupName])
        {
            if (tween != null && tween.IsActive())
            {
                tween.Pause();
            }
        }

        LogMessage($"暂停分组动画: {groupName}");
    }

    /// <summary>
    /// 恢复指定分组的动画
    /// </summary>
    /// <param name="groupName">分组名称</param>
    public void PlayGroup(string groupName)
    {
        if (!_tweenGroups.ContainsKey(groupName)) return;

        foreach (Tween tween in _tweenGroups[groupName])
        {
            if (tween != null && tween.IsActive())
            {
                tween.Play();
            }
        }

        LogMessage($"恢复分组动画: {groupName}");
    }

    /// <summary>
    /// 停止指定分组的动画
    /// </summary>
    /// <param name="groupName">分组名称</param>
    public void KillGroup(string groupName)
    {
        if (!_tweenGroups.ContainsKey(groupName)) return;

        foreach (Tween tween in _tweenGroups[groupName])
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }

        _tweenGroups.Remove(groupName);
        LogMessage($"停止分组动画: {groupName}");
    }

    /// <summary>
    /// 获取指定序列
    /// </summary>
    /// <param name="sequenceName">序列名称</param>
    /// <returns>序列对象</returns>
    public Sequence GetSequence(string sequenceName)
    {
        return _sequences.ContainsKey(sequenceName) ? _sequences[sequenceName] : null;
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        // 清理无效的引用
        if (Time.frameCount % 60 == 0) // 每秒清理一次
        {
            CleanupInvalidTweens();
        }

        // 显示统计信息
        if (showTweenCount && enableDebugLog)
        {
            if (Time.frameCount % 120 == 0) // 每2秒显示一次
            {
                LogMessage($"活跃动画数: {_activeTweenCount}, 活跃序列数: {_activeSequenceCount}");
            }
        }
    }

    /// <summary>
    /// 清理无效的Tween引用
    /// </summary>
    private void CleanupInvalidTweens()
    {
        // 清理失效的Tween
        _allTweens.RemoveAll(t => t == null || !t.IsActive());
        
        // 清理失效的分组
        List<string> groupsToRemove = new List<string>();
        foreach (var group in _tweenGroups)
        {
            group.Value.RemoveAll(t => t == null || !t.IsActive());
            if (group.Value.Count == 0)
            {
                groupsToRemove.Add(group.Key);
            }
        }
        
        foreach (string groupName in groupsToRemove)
        {
            _tweenGroups.Remove(groupName);
        }
        
        // 清理失效的序列
        List<string> sequencesToRemove = new List<string>();
        foreach (var sequence in _sequences)
        {
            if (sequence.Value == null || !sequence.Value.IsActive())
            {
                sequencesToRemove.Add(sequence.Key);
            }
        }
        
        foreach (string sequenceName in sequencesToRemove)
        {
            _sequences.Remove(sequenceName);
        }
        
        // 更新计数
        _activeTweenCount = _allTweens.Count;
        _activeSequenceCount = _sequences.Count;
    }

    /// <summary>
    /// 清理管理器
    /// </summary>
    public void Cleanup()
    {
        KillAll();
        LogMessage("补间管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[TweenManager] {message}");
        }
    }

    #endregion
}