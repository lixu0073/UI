using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// 2D动画控制器
/// 专门用于2D游戏角色和对象的动画控制
/// 提供统一的动画播放接口和状态管理
/// </summary>
[RequireComponent(typeof(Animator))]
public class Animator2DController : MonoBehaviour
{
    [Header("动画设置")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private float defaultTransitionDuration = 0.1f;
    [SerializeField] private bool enableRootMotion = false;
    
    [Header("特效设置")]
    [SerializeField] private bool enableAnimationEffects = true;
    [SerializeField] private float effectScale = 1f;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showCurrentState = false;

    // 组件引用
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private TweenManager tweenManager;

    // 动画状态
    private string currentStateName;
    private string previousStateName;
    private bool isTransitioning = false;
    private float animationSpeed = 1f;

    // 常用动画参数哈希值
    private readonly int HashSpeed = Animator.StringToHash("Speed");
    private readonly int HashGrounded = Animator.StringToHash("Grounded");
    private readonly int HashJump = Animator.StringToHash("Jump");
    private readonly int HashAttack = Animator.StringToHash("Attack");
    private readonly int HashHurt = Animator.StringToHash("Hurt");
    private readonly int HashDead = Animator.StringToHash("Dead");
    private readonly int HashIdle = Animator.StringToHash("Idle");
    private readonly int HashWalk = Animator.StringToHash("Walk");
    private readonly int HashRun = Animator.StringToHash("Run");

    // 属性
    public string CurrentStateName => currentStateName;
    public string PreviousStateName => previousStateName;
    public bool IsTransitioning => isTransitioning;
    public float AnimationSpeed => animationSpeed;

    // 事件
    public System.Action<string> OnAnimationStarted;
    public System.Action<string> OnAnimationCompleted;
    public System.Action<string, string> OnStateChanged;

    #region Unity生命周期

    private void Awake()
    {
        InitializeComponents();
        
        if (autoInitialize)
        {
            Initialize();
        }
    }

    private void Start()
    {
        tweenManager = TweenManager.Instance;
    }

    private void Update()
    {
        UpdateAnimationState();
        
        if (showCurrentState && enableDebugLog)
        {
            UpdateDebugDisplay();
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (animator == null)
        {
            LogError("未找到Animator组件！");
            return;
        }

        LogMessage("动画控制器组件初始化完成");
    }

    /// <summary>
    /// 初始化动画控制器
    /// </summary>
    public void Initialize()
    {
        if (animator == null) return;

        // 设置动画更新模式
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // 启用根运动（如果需要）
        animator.applyRootMotion = enableRootMotion;

        // 获取初始状态
        if (animator.runtimeAnimatorController != null)
        {
            UpdateCurrentState();
        }

        LogMessage("动画控制器初始化完成");
    }

    #endregion

    #region 基础动画控制

    /// <summary>
    /// 播放动画状态
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <param name="layer">动画层</param>
    /// <param name="normalizedTime">归一化时间</param>
    public void PlayAnimation(string stateName, int layer = 0, float normalizedTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        previousStateName = currentStateName;
        currentStateName = stateName;
        isTransitioning = true;

        animator.Play(stateName, layer, normalizedTime);

        // 派发事件
        OnAnimationStarted?.Invoke(stateName);
        OnStateChanged?.Invoke(previousStateName, currentStateName);
        
        EventDispatcher.Instance.Dispatch(GameEvent.ANIMATION_STARTED, 
            new AnimationEventData { animationName = stateName, gameObject = gameObject });

        LogMessage($"播放动画: {stateName}");
    }

    /// <summary>
    /// 交叉淡入动画
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <param name="transitionDuration">过渡时间</param>
    /// <param name="layer">动画层</param>
    /// <param name="normalizedTime">归一化时间</param>
    public void CrossFadeAnimation(string stateName, float transitionDuration = -1f, 
                                  int layer = 0, float normalizedTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        if (transitionDuration < 0)
            transitionDuration = defaultTransitionDuration;

        previousStateName = currentStateName;
        currentStateName = stateName;
        isTransitioning = true;

        animator.CrossFade(stateName, transitionDuration, layer, normalizedTime);

        // 派发事件
        OnAnimationStarted?.Invoke(stateName);
        OnStateChanged?.Invoke(previousStateName, currentStateName);
        
        EventDispatcher.Instance.Dispatch(GameEvent.ANIMATION_STARTED, 
            new AnimationEventData { animationName = stateName, gameObject = gameObject });

        LogMessage($"交叉淡入动画: {stateName}");
    }

    /// <summary>
    /// 触发动画触发器
    /// </summary>
    /// <param name="triggerName">触发器名称</param>
    public void TriggerAnimation(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;

        animator.SetTrigger(triggerName);
        LogMessage($"触发动画: {triggerName}");
    }

    /// <summary>
    /// 停止动画
    /// </summary>
    public void StopAnimation()
    {
        if (animator == null) return;

        animator.enabled = false;
        LogMessage("动画已停止");
    }

    /// <summary>
    /// 恢复动画
    /// </summary>
    public void ResumeAnimation()
    {
        if (animator == null) return;

        animator.enabled = true;
        LogMessage("动画已恢复");
    }

    #endregion

    #region 动画参数控制

    /// <summary>
    /// 设置动画速度
    /// </summary>
    /// <param name="speed">速度值</param>
    public void SetSpeed(float speed)
    {
        if (animator == null) return;

        animator.SetFloat(HashSpeed, speed);
        LogMessage($"设置动画速度: {speed}");
    }

    /// <summary>
    /// 设置是否在地面
    /// </summary>
    /// <param name="grounded">是否在地面</param>
    public void SetGrounded(bool grounded)
    {
        if (animator == null) return;

        animator.SetBool(HashGrounded, grounded);
        LogMessage($"设置地面状态: {grounded}");
    }

    /// <summary>
    /// 触发跳跃动画
    /// </summary>
    public void TriggerJump()
    {
        if (animator == null) return;

        animator.SetTrigger(HashJump);
        LogMessage("触发跳跃动画");
    }

    /// <summary>
    /// 触发攻击动画
    /// </summary>
    public void TriggerAttack()
    {
        if (animator == null) return;

        animator.SetTrigger(HashAttack);
        LogMessage("触发攻击动画");
    }

    /// <summary>
    /// 触发受伤动画
    /// </summary>
    public void TriggerHurt()
    {
        if (animator == null) return;

        animator.SetTrigger(HashHurt);
        
        // 添加受伤震动效果
        if (enableAnimationEffects && tweenManager != null)
        {
            tweenManager.DoShake(transform, 0.5f * effectScale, 10, 0.3f, "Hurt");
        }
        
        LogMessage("触发受伤动画");
    }

    /// <summary>
    /// 触发死亡动画
    /// </summary>
    public void TriggerDeath()
    {
        if (animator == null) return;

        animator.SetTrigger(HashDead);
        LogMessage("触发死亡动画");
    }

    /// <summary>
    /// 设置布尔参数
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="value">布尔值</param>
    public void SetBool(string parameterName, bool value)
    {
        if (animator == null) return;

        animator.SetBool(parameterName, value);
        LogMessage($"设置布尔参数 {parameterName}: {value}");
    }

    /// <summary>
    /// 设置浮点参数
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="value">浮点值</param>
    public void SetFloat(string parameterName, float value)
    {
        if (animator == null) return;

        animator.SetFloat(parameterName, value);
        LogMessage($"设置浮点参数 {parameterName}: {value}");
    }

    /// <summary>
    /// 设置整数参数
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="value">整数值</param>
    public void SetInteger(string parameterName, int value)
    {
        if (animator == null) return;

        animator.SetInteger(parameterName, value);
        LogMessage($"设置整数参数 {parameterName}: {value}");
    }

    #endregion

    #region 动画速度控制

    /// <summary>
    /// 设置动画播放速度
    /// </summary>
    /// <param name="speed">播放速度</param>
    public void SetAnimationSpeed(float speed)
    {
        if (animator == null) return;

        animationSpeed = Mathf.Max(0, speed);
        animator.speed = animationSpeed;
        LogMessage($"设置动画播放速度: {animationSpeed}");
    }

    /// <summary>
    /// 暂停动画
    /// </summary>
    public void PauseAnimation()
    {
        SetAnimationSpeed(0f);
        LogMessage("动画已暂停");
    }

    /// <summary>
    /// 恢复动画播放速度
    /// </summary>
    public void ResumeAnimationSpeed()
    {
        SetAnimationSpeed(1f);
        LogMessage("动画速度已恢复");
    }

    #endregion

    #region 特效增强

    /// <summary>
    /// 播放带特效的动画
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <param name="effectType">特效类型</param>
    public void PlayAnimationWithEffect(string stateName, AnimationEffectType effectType)
    {
        PlayAnimation(stateName);
        
        if (!enableAnimationEffects || tweenManager == null) return;

        switch (effectType)
        {
            case AnimationEffectType.Shake:
                tweenManager.DoShake(transform, 0.3f * effectScale, 5, 0.5f, stateName);
                break;
                
            case AnimationEffectType.Pulse:
                tweenManager.DoPulse(transform, 1.1f, 0.3f, 1, stateName);
                break;
                
            case AnimationEffectType.Flash:
                if (spriteRenderer != null)
                {
                    Color originalColor = spriteRenderer.color;
                    spriteRenderer.color = Color.white;
                    tweenManager.DoFade(spriteRenderer, originalColor.a, 0.2f, DG.Tweening.Ease.OutQuad, stateName);
                }
                break;
        }
    }

    /// <summary>
    /// 添加击中效果
    /// </summary>
    /// <param name="hitDirection">击中方向</param>
    public async void PlayHitEffect(Vector2 hitDirection)
    {
        if (!enableAnimationEffects || tweenManager == null) return;

        // 击退效果
        Vector3 originalPosition = transform.position;
        Vector3 hitPosition = originalPosition + (Vector3)hitDirection * 0.5f * effectScale;
        
        await tweenManager.DoMove(transform, hitPosition, 0.1f, DG.Tweening.Ease.OutQuad, "Hit")
                          .AsyncWaitForCompletion();
        
        await tweenManager.DoMove(transform, originalPosition, 0.2f, DG.Tweening.Ease.OutBounce, "Hit")
                          .AsyncWaitForCompletion();

        // 颜色闪烁
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            await tweenManager.DoFade(spriteRenderer, originalColor.a, 0.3f, DG.Tweening.Ease.OutQuad, "Hit")
                              .AsyncWaitForCompletion();
            spriteRenderer.color = originalColor;
        }
    }

    #endregion

    #region 状态查询

    /// <summary>
    /// 检查动画是否正在播放
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <param name="layer">动画层</param>
    /// <returns>是否正在播放</returns>
    public bool IsPlayingAnimation(string stateName, int layer = 0)
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
        return stateInfo.IsName(stateName);
    }

    /// <summary>
    /// 获取当前动画的归一化时间
    /// </summary>
    /// <param name="layer">动画层</param>
    /// <returns>归一化时间</returns>
    public float GetCurrentAnimationTime(int layer = 0)
    {
        if (animator == null) return 0f;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
        return stateInfo.normalizedTime;
    }

    /// <summary>
    /// 检查动画是否完成
    /// </summary>
    /// <param name="layer">动画层</param>
    /// <returns>是否完成</returns>
    public bool IsAnimationComplete(int layer = 0)
    {
        if (animator == null) return true;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
        return stateInfo.normalizedTime >= 1f && !stateInfo.loop;
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 更新动画状态
    /// </summary>
    private void UpdateAnimationState()
    {
        if (animator == null) return;

        AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        // 检查是否处于过渡状态
        isTransitioning = animator.IsInTransition(0);
        
        // 更新当前状态名称
        if (!isTransitioning)
        {
            string newStateName = GetStateName(currentStateInfo);
            if (newStateName != currentStateName)
            {
                previousStateName = currentStateName;
                currentStateName = newStateName;
                
                OnStateChanged?.Invoke(previousStateName, currentStateName);
            }
        }
        
        // 检查动画是否完成
        if (!isTransitioning && IsAnimationComplete(0))
        {
            OnAnimationCompleted?.Invoke(currentStateName);
            EventDispatcher.Instance.Dispatch(GameEvent.ANIMATION_COMPLETED, 
                new AnimationEventData { animationName = currentStateName, gameObject = gameObject });
        }
    }

    /// <summary>
    /// 获取状态名称
    /// </summary>
    /// <param name="stateInfo">状态信息</param>
    /// <returns>状态名称</returns>
    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        // 这里可以根据实际需要实现状态名称的获取逻辑
        return stateInfo.shortNameHash.ToString();
    }

    /// <summary>
    /// 更新当前状态
    /// </summary>
    private void UpdateCurrentState()
    {
        if (animator == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        currentStateName = GetStateName(stateInfo);
    }

    /// <summary>
    /// 更新调试显示
    /// </summary>
    private void UpdateDebugDisplay()
    {
        if (Time.frameCount % 30 == 0) // 每半秒更新一次
        {
            LogMessage($"当前状态: {currentStateName}, 过渡中: {isTransitioning}");
        }
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[Animator2DController] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Animator2DController] {message}");
    }

    #endregion
}

/// <summary>
/// 动画特效类型
/// </summary>
public enum AnimationEffectType
{
    None,       // 无特效
    Shake,      // 震动
    Pulse,      // 脉冲
    Flash       // 闪烁
}

/// <summary>
/// 动画事件数据
/// </summary>
[System.Serializable]
public class AnimationEventData
{
    public string animationName;
    public GameObject gameObject;
    public float normalizedTime;
}