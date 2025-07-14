using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 2D角色控制器基类
/// 提供2D游戏角色的基础控制功能，包括移动、跳跃、动画等
/// 使用Rigidbody2D进行物理移动，支持平台跳跃游戏的常用功能
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Character2DController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float acceleration = 10f;
    [SerializeField] protected float deceleration = 15f;
    [SerializeField] protected float airControl = 0.8f;
    
    [Header("跳跃设置")]
    [SerializeField] protected float jumpForce = 12f;
    [SerializeField] protected int maxJumps = 2;
    [SerializeField] protected float jumpCutMultiplier = 0.5f;
    [SerializeField] protected float coyoteTime = 0.1f;
    [SerializeField] protected float jumpBufferTime = 0.1f;
    
    [Header("地面检测")]
    [SerializeField] protected Transform groundCheckPoint;
    [SerializeField] protected float groundCheckRadius = 0.2f;
    [SerializeField] protected LayerMask groundLayerMask = 1;
    [SerializeField] protected LayerMask platformLayerMask = 1 << 8;
    
    [Header("动画设置")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected bool flipOnDirectionChange = true;
    
    [Header("音效设置")]
    [SerializeField] protected string jumpSFX = "Jump";
    [SerializeField] protected string landSFX = "Land";
    [SerializeField] protected string footstepSFX = "Footstep";
    
    [Header("调试选项")]
    [SerializeField] protected bool enableDebugDraws = false;
    [SerializeField] protected bool enableDebugLog = false;

    // 组件引用
    protected Rigidbody2D rb2d;
    protected Collider2D col2d;
    protected AudioManager audioManager;

    // 移动状态
    protected Vector2 velocity;
    protected float horizontalInput;
    protected bool facingRight = true;
    protected bool isMoving = false;

    // 跳跃状态
    protected bool isGrounded = false;
    protected bool wasGrounded = false;
    protected int jumpCount = 0;
    protected float coyoteTimeCounter = 0f;
    protected float jumpBufferCounter = 0f;
    protected bool isJumping = false;
    protected bool jumpInputHeld = false;

    // 动画哈希
    protected readonly int AnimSpeed = Animator.StringToHash("Speed");
    protected readonly int AnimGrounded = Animator.StringToHash("Grounded");
    protected readonly int AnimJump = Animator.StringToHash("Jump");
    protected readonly int AnimFalling = Animator.StringToHash("Falling");

    // 属性
    public bool IsGrounded => isGrounded;
    public bool IsMoving => isMoving;
    public bool IsJumping => isJumping;
    public bool FacingRight => facingRight;
    public Vector2 Velocity => velocity;

    #region Unity生命周期

    /// <summary>
    /// 初始化
    /// </summary>
    protected virtual void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// 开始
    /// </summary>
    protected virtual void Start()
    {
        InitializeController();
    }

    /// <summary>
    /// 更新
    /// </summary>
    protected virtual void Update()
    {
        HandleInput();
        UpdateTimers();
        UpdateAnimation();
    }

    /// <summary>
    /// 固定更新
    /// </summary>
    protected virtual void FixedUpdate()
    {
        CheckGrounded();
        HandleMovement();
        HandleJumping();
        ApplyMovement();
    }

    /// <summary>
    /// 绘制调试信息
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        if (enableDebugDraws && groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化组件
    /// </summary>
    protected virtual void InitializeComponents()
    {
        rb2d = GetComponent<Rigidbody2D>();
        col2d = GetComponent<Collider2D>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
            
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        if (groundCheckPoint == null)
        {
            // 自动创建地面检测点
            GameObject checkPoint = new GameObject("GroundCheckPoint");
            checkPoint.transform.SetParent(transform);
            checkPoint.transform.localPosition = new Vector3(0, -col2d.bounds.extents.y, 0);
            groundCheckPoint = checkPoint.transform;
        }

        audioManager = AudioManager.Instance;
        LogMessage("组件初始化完成");
    }

    /// <summary>
    /// 初始化控制器
    /// </summary>
    protected virtual void InitializeController()
    {
        // 配置Rigidbody2D
        rb2d.freezeRotation = true;
        rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2d.gravityScale = 1f;

        velocity = Vector2.zero;
        jumpCount = 0;

        LogMessage("角色控制器初始化完成");
    }

    #endregion

    #region 输入处理

    /// <summary>
    /// 处理输入（子类可重写）
    /// </summary>
    protected virtual void HandleInput()
    {
        // 移动输入
        horizontalInput = Input.GetAxisRaw("Horizontal");
        
        // 跳跃输入
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        
        jumpInputHeld = Input.GetButton("Jump");
        
        // 跳跃中断
        if (Input.GetButtonUp("Jump") && velocity.y > 0)
        {
            CutJump();
        }
    }

    #endregion

    #region 移动系统

    /// <summary>
    /// 处理水平移动
    /// </summary>
    protected virtual void HandleMovement()
    {
        float targetSpeed = horizontalInput * moveSpeed;
        float speedDifference = targetSpeed - velocity.x;
        
        // 根据是否在地面选择不同的加速度
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        
        // 在空中时降低控制力
        if (!isGrounded)
        {
            accelRate *= airControl;
        }

        // 应用移动
        float movement = speedDifference * accelRate;
        velocity.x += movement * Time.fixedDeltaTime;

        // 更新移动状态
        isMoving = Mathf.Abs(velocity.x) > 0.1f && isGrounded;

        // 处理朝向
        if (flipOnDirectionChange && Mathf.Abs(horizontalInput) > 0.1f)
        {
            bool shouldFaceRight = horizontalInput > 0;
            if (shouldFaceRight != facingRight)
            {
                Flip();
            }
        }

        LogMessage($"移动处理: 目标速度={targetSpeed}, 当前速度={velocity.x}", false);
    }

    /// <summary>
    /// 翻转角色朝向
    /// </summary>
    protected virtual void Flip()
    {
        facingRight = !facingRight;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !facingRight;
        }
        else
        {
            // 如果没有SpriteRenderer，则翻转Transform
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }

        LogMessage($"角色翻转: 朝向右={facingRight}");
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    /// <param name="speed">新的移动速度</param>
    public virtual void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0, speed);
        LogMessage($"设置移动速度: {moveSpeed}");
    }

    #endregion

    #region 跳跃系统

    /// <summary>
    /// 处理跳跃
    /// </summary>
    protected virtual void HandleJumping()
    {
        // 检查是否可以跳跃
        bool canJump = (isGrounded || coyoteTimeCounter > 0f || jumpCount < maxJumps) 
                      && jumpBufferCounter > 0f;

        if (canJump)
        {
            Jump();
        }
    }

    /// <summary>
    /// 执行跳跃
    /// </summary>
    protected virtual void Jump()
    {
        // 重置垂直速度
        velocity.y = jumpForce;
        
        // 更新跳跃状态
        isJumping = true;
        jumpCount++;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        // 播放跳跃音效
        audioManager?.PlaySFX(jumpSFX);

        // 派发跳跃事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_JUMP, transform.position);

        LogMessage($"跳跃执行: 跳跃次数={jumpCount}");
    }

    /// <summary>
    /// 中断跳跃（短跳）
    /// </summary>
    protected virtual void CutJump()
    {
        if (velocity.y > 0)
        {
            velocity.y *= jumpCutMultiplier;
            LogMessage("跳跃中断");
        }
    }

    /// <summary>
    /// 设置跳跃力度
    /// </summary>
    /// <param name="force">新的跳跃力度</param>
    public virtual void SetJumpForce(float force)
    {
        jumpForce = Mathf.Max(0, force);
        LogMessage($"设置跳跃力度: {jumpForce}");
    }

    #endregion

    #region 地面检测

    /// <summary>
    /// 检测是否在地面上
    /// </summary>
    protected virtual void CheckGrounded()
    {
        wasGrounded = isGrounded;
        
        // 使用OverlapCircle检测地面
        Collider2D[] groundColliders = Physics2D.OverlapCircleAll(
            groundCheckPoint.position, 
            groundCheckRadius, 
            groundLayerMask | platformLayerMask
        );

        isGrounded = groundColliders.Length > 0;

        // 着陆处理
        if (isGrounded && !wasGrounded)
        {
            OnLanded();
        }
        
        // 离开地面处理
        if (!isGrounded && wasGrounded)
        {
            OnLeftGround();
        }
    }

    /// <summary>
    /// 着陆时调用
    /// </summary>
    protected virtual void OnLanded()
    {
        isJumping = false;
        jumpCount = 0;
        
        // 播放着陆音效
        if (velocity.y < -5f) // 只有下落速度足够大才播放
        {
            audioManager?.PlaySFX(landSFX);
        }

        // 派发着陆事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_LAND, transform.position);

        LogMessage("角色着陆");
    }

    /// <summary>
    /// 离开地面时调用
    /// </summary>
    protected virtual void OnLeftGround()
    {
        coyoteTimeCounter = coyoteTime;
        LogMessage("角色离开地面");
    }

    #endregion

    #region 时间管理

    /// <summary>
    /// 更新各种计时器
    /// </summary>
    protected virtual void UpdateTimers()
    {
        // 更新土狼时间
        if (coyoteTimeCounter > 0)
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // 更新跳跃缓冲时间
        if (jumpBufferCounter > 0)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    #endregion

    #region 动画控制

    /// <summary>
    /// 更新动画参数
    /// </summary>
    protected virtual void UpdateAnimation()
    {
        if (animator == null) return;

        // 设置动画参数
        animator.SetFloat(AnimSpeed, Mathf.Abs(velocity.x));
        animator.SetBool(AnimGrounded, isGrounded);
        animator.SetBool(AnimJump, isJumping);
        animator.SetBool(AnimFalling, velocity.y < -0.1f && !isGrounded);
    }

    #endregion

    #region 物理应用

    /// <summary>
    /// 应用移动到Rigidbody2D
    /// </summary>
    protected virtual void ApplyMovement()
    {
        rb2d.velocity = velocity;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 添加力（例如被击退）
    /// </summary>
    /// <param name="force">施加的力</param>
    /// <param name="mode">力的模式</param>
    public virtual void AddForce(Vector2 force, ForceMode2D mode = ForceMode2D.Impulse)
    {
        rb2d.AddForce(force, mode);
        LogMessage($"添加力: {force}");
    }

    /// <summary>
    /// 瞬间移动到指定位置
    /// </summary>
    /// <param name="position">目标位置</param>
    public virtual void Teleport(Vector3 position)
    {
        transform.position = position;
        velocity = Vector2.zero;
        rb2d.velocity = Vector2.zero;
        LogMessage($"瞬移到: {position}");
    }

    /// <summary>
    /// 停止所有移动
    /// </summary>
    public virtual void StopMovement()
    {
        velocity = Vector2.zero;
        rb2d.velocity = Vector2.zero;
        horizontalInput = 0f;
        LogMessage("停止移动");
    }

    /// <summary>
    /// 启用/禁用角色控制
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public virtual void SetControlEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            StopMovement();
        }
        LogMessage($"角色控制: {(enabled ? "启用" : "禁用")}");
    }

    #endregion

    #region 日志方法

    /// <summary>
    /// 记录日志消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="alwaysLog">是否总是记录（忽略调试设置）</param>
    protected virtual void LogMessage(string message, bool alwaysLog = true)
    {
        if (enableDebugLog || alwaysLog)
        {
            Debug.Log($"[Character2DController] {message}");
        }
    }

    #endregion
}