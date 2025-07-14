using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 玩家控制器类
/// 继承自Character2DController，专门用于玩家角色控制
/// 添加了玩家特有的功能，如生命值管理、技能系统等
/// </summary>
public class PlayerController : Character2DController
{
    [Header("玩家特有设置")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invulnerabilityTime = 2f;
    [SerializeField] private float respawnTime = 3f;
    
    [Header("特殊能力")]
    [SerializeField] private bool canWallJump = false;
    [SerializeField] private bool canDash = false;
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashDuration = 0.2f;
    
    [Header("墙壁跳跃设置")]
    [SerializeField] private Transform wallCheckPoint;
    [SerializeField] private float wallCheckDistance = 0.3f;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(8f, 12f);
    
    [Header("玩家音效")]
    [SerializeField] private string damageSFX = "PlayerHurt";
    [SerializeField] private string healSFX = "PlayerHeal";
    [SerializeField] private string dashSFX = "PlayerDash";
    [SerializeField] private string deathSFX = "PlayerDeath";

    // 玩家状态
    private int currentHealth;
    private bool isInvulnerable = false;
    private bool isDead = false;
    private bool isRespawning = false;
    
    // 墙壁跳跃
    private bool isTouchingWall = false;
    private bool isWallSliding = false;
    private int wallDirection = 0;
    
    // 冲刺系统
    private bool isDashing = false;
    private float dashCooldownTimer = 0f;
    private Vector2 dashDirection;
    
    // 动画哈希（玩家特有）
    private readonly int AnimHealth = Animator.StringToHash("Health");
    private readonly int AnimInvulnerable = Animator.StringToHash("Invulnerable");
    private readonly int AnimWallSlide = Animator.StringToHash("WallSlide");
    private readonly int AnimDash = Animator.StringToHash("Dash");

    // 属性
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => isDead;
    public bool CanDash => canDash && dashCooldownTimer <= 0f && !isDashing;
    public bool IsWallSliding => isWallSliding;

    // 事件
    public System.Action<int, int> OnHealthChanged; // 当前血量，最大血量
    public System.Action OnPlayerDied;
    public System.Action OnPlayerRespawned;

    #region Unity生命周期

    protected override void Awake()
    {
        base.Awake();
        InitializePlayer();
    }

    protected override void Start()
    {
        base.Start();
        
        // 初始化玩家状态
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    protected override void Update()
    {
        if (isDead || isRespawning) return;
        
        base.Update();
        UpdateCooldowns();
        HandleSpecialAbilities();
    }

    protected override void FixedUpdate()
    {
        if (isDead || isRespawning) return;
        
        if (!isDashing)
        {
            base.FixedUpdate();
            CheckWalls();
            HandleWallSliding();
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        if (enableDebugDraws && wallCheckPoint != null)
        {
            Gizmos.color = isTouchingWall ? Color.blue : Color.cyan;
            Vector3 wallCheckPos = wallCheckPoint.position + Vector3.right * wallCheckDistance * (facingRight ? 1 : -1);
            Gizmos.DrawLine(wallCheckPoint.position, wallCheckPos);
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化玩家特有组件
    /// </summary>
    private void InitializePlayer()
    {
        if (wallCheckPoint == null && canWallJump)
        {
            // 自动创建墙壁检测点
            GameObject checkPoint = new GameObject("WallCheckPoint");
            checkPoint.transform.SetParent(transform);
            checkPoint.transform.localPosition = new Vector3(col2d.bounds.extents.x, 0, 0);
            wallCheckPoint = checkPoint.transform;
        }

        LogMessage("玩家控制器初始化完成");
    }

    #endregion

    #region 输入处理

    protected override void HandleInput()
    {
        if (isDead || isRespawning) return;
        
        base.HandleInput();
        
        // 冲刺输入
        if (canDash && Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (CanDash)
            {
                PerformDash();
            }
        }
    }

    #endregion

    #region 生命值系统

    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage">伤害值</param>
    /// <param name="damageSource">伤害来源位置</param>
    public void TakeDamage(int damage, Vector3 damageSource = default)
    {
        if (isDead || isInvulnerable || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // 播放受伤音效
        audioManager?.PlaySFX(damageSFX);
        
        // 更新UI
        UpdateHealthUI();
        
        // 触发无敌时间
        StartInvulnerability();
        
        // 击退效果
        if (damageSource != default)
        {
            Vector2 knockbackDirection = (transform.position - damageSource).normalized;
            AddForce(knockbackDirection * 5f);
        }
        
        // 派发事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, currentHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        LogMessage($"玩家受伤: -{damage}, 当前血量: {currentHealth}");
        
        // 检查死亡
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    /// <param name="healAmount">恢复量</param>
    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;

        int oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        
        if (currentHealth > oldHealth)
        {
            // 播放治疗音效
            audioManager?.PlaySFX(healSFX);
            
            // 更新UI
            UpdateHealthUI();
            
            // 派发事件
            EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            
            LogMessage($"玩家治疗: +{healAmount}, 当前血量: {currentHealth}");
        }
    }

    /// <summary>
    /// 设置最大生命值
    /// </summary>
    /// <param name="newMaxHealth">新的最大生命值</param>
    /// <param name="healToFull">是否恢复满血</param>
    public void SetMaxHealth(int newMaxHealth, bool healToFull = false)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);
        
        if (healToFull)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
        
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        LogMessage($"设置最大生命值: {maxHealth}");
    }

    /// <summary>
    /// 更新生命值UI
    /// </summary>
    private void UpdateHealthUI()
    {
        if (animator != null)
        {
            animator.SetFloat(AnimHealth, (float)currentHealth / maxHealth);
        }
    }

    #endregion

    #region 无敌系统

    /// <summary>
    /// 开始无敌时间
    /// </summary>
    private async void StartInvulnerability()
    {
        if (isInvulnerable) return;
        
        isInvulnerable = true;
        
        if (animator != null)
        {
            animator.SetBool(AnimInvulnerable, true);
        }
        
        // 闪烁效果
        if (spriteRenderer != null)
        {
            spriteRenderer.DOFade(0.5f, 0.1f).SetLoops(-1, LoopType.Yoyo);
        }
        
        LogMessage("开始无敌时间");
        
        // 等待无敌时间结束
        await UniTask.Delay((int)(invulnerabilityTime * 1000));
        
        EndInvulnerability();
    }

    /// <summary>
    /// 结束无敌时间
    /// </summary>
    private void EndInvulnerability()
    {
        isInvulnerable = false;
        
        if (animator != null)
        {
            animator.SetBool(AnimInvulnerable, false);
        }
        
        // 停止闪烁效果
        if (spriteRenderer != null)
        {
            spriteRenderer.DOKill();
            spriteRenderer.color = Color.white;
        }
        
        LogMessage("无敌时间结束");
    }

    #endregion

    #region 死亡和重生

    /// <summary>
    /// 玩家死亡
    /// </summary>
    private async void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        // 停止移动
        StopMovement();
        SetControlEnabled(false);
        
        // 播放死亡音效
        audioManager?.PlaySFX(deathSFX);
        
        // 派发死亡事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_DIED, transform.position);
        OnPlayerDied?.Invoke();
        
        LogMessage("玩家死亡");
        
        // 死亡动画
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }
        
        // 等待重生时间
        await UniTask.Delay((int)(respawnTime * 1000));
        
        Respawn();
    }

    /// <summary>
    /// 玩家重生
    /// </summary>
    private async void Respawn()
    {
        isRespawning = true;
        
        // 重置状态
        isDead = false;
        isInvulnerable = false;
        currentHealth = maxHealth;
        
        // 重置位置（这里可以设置检查点系统）
        Vector3 respawnPosition = FindRespawnPosition();
        Teleport(respawnPosition);
        
        // 重生效果
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            spriteRenderer.DOFade(0f, 0f);
            spriteRenderer.DOFade(1f, 1f);
        }
        
        // 等待重生动画完成
        await UniTask.Delay(1000);
        
        // 启用控制
        SetControlEnabled(true);
        isRespawning = false;
        
        // 短暂无敌
        StartInvulnerability();
        
        // 更新UI
        UpdateHealthUI();
        
        // 派发重生事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_RESPAWNED, transform.position);
        OnPlayerRespawned?.Invoke();
        
        LogMessage("玩家重生");
    }

    /// <summary>
    /// 查找重生位置
    /// </summary>
    /// <returns>重生位置</returns>
    private Vector3 FindRespawnPosition()
    {
        // 这里可以实现检查点系统
        // 暂时返回当前位置的上方
        return transform.position + Vector3.up * 2f;
    }

    #endregion

    #region 墙壁跳跃系统

    /// <summary>
    /// 检测墙壁
    /// </summary>
    private void CheckWalls()
    {
        if (!canWallJump || wallCheckPoint == null) return;
        
        wallDirection = facingRight ? 1 : -1;
        Vector3 wallCheckPos = wallCheckPoint.position + Vector3.right * wallCheckDistance * wallDirection;
        
        isTouchingWall = Physics2D.Linecast(wallCheckPoint.position, wallCheckPos, groundLayerMask);
    }

    /// <summary>
    /// 处理墙壁滑动
    /// </summary>
    private void HandleWallSliding()
    {
        if (!canWallJump) return;
        
        bool wasWallSliding = isWallSliding;
        isWallSliding = isTouchingWall && !isGrounded && velocity.y < 0 && Mathf.Abs(horizontalInput) > 0.1f;
        
        if (isWallSliding)
        {
            // 限制下滑速度
            velocity.y = Mathf.Max(velocity.y, -wallSlideSpeed);
        }
        
        // 墙壁跳跃
        if (isWallSliding && jumpBufferCounter > 0f)
        {
            WallJump();
        }
        
        // 更新动画
        if (animator != null && wasWallSliding != isWallSliding)
        {
            animator.SetBool(AnimWallSlide, isWallSliding);
        }
    }

    /// <summary>
    /// 执行墙壁跳跃
    /// </summary>
    private void WallJump()
    {
        Vector2 force = new Vector2(-wallDirection * wallJumpForce.x, wallJumpForce.y);
        velocity = force;
        
        jumpCount = 1; // 墙跳后还可以再跳一次
        jumpBufferCounter = 0f;
        isJumping = true;
        
        // 强制改变朝向
        if ((wallDirection > 0 && facingRight) || (wallDirection < 0 && !facingRight))
        {
            Flip();
        }
        
        // 播放跳跃音效
        audioManager?.PlaySFX(jumpSFX);
        
        LogMessage("墙壁跳跃执行");
    }

    #endregion

    #region 冲刺系统

    /// <summary>
    /// 执行冲刺
    /// </summary>
    private async void PerformDash()
    {
        if (!CanDash) return;
        
        isDashing = true;
        dashCooldownTimer = dashCooldown;
        
        // 确定冲刺方向
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            dashDirection = new Vector2(horizontalInput, 0).normalized;
        }
        else
        {
            dashDirection = new Vector2(facingRight ? 1 : -1, 0);
        }
        
        // 停止重力影响
        float originalGravity = rb2d.gravityScale;
        rb2d.gravityScale = 0f;
        
        // 应用冲刺力
        velocity = dashDirection * dashForce;
        rb2d.velocity = velocity;
        
        // 播放冲刺音效
        audioManager?.PlaySFX(dashSFX);
        
        // 更新动画
        if (animator != null)
        {
            animator.SetBool(AnimDash, true);
        }
        
        LogMessage("冲刺执行");
        
        // 等待冲刺持续时间
        await UniTask.Delay((int)(dashDuration * 1000));
        
        // 恢复正常状态
        isDashing = false;
        rb2d.gravityScale = originalGravity;
        
        if (animator != null)
        {
            animator.SetBool(AnimDash, false);
        }
        
        LogMessage("冲刺结束");
    }

    #endregion

    #region 计时器更新

    /// <summary>
    /// 更新各种冷却计时器
    /// </summary>
    private void UpdateCooldowns()
    {
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region 特殊能力处理

    /// <summary>
    /// 处理特殊能力输入
    /// </summary>
    private void HandleSpecialAbilities()
    {
        // 这里可以添加其他特殊能力的输入处理
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 立即杀死玩家
    /// </summary>
    public void Kill()
    {
        currentHealth = 0;
        Die();
    }

    /// <summary>
    /// 恢复满血
    /// </summary>
    public void HealToFull()
    {
        Heal(maxHealth - currentHealth);
    }

    /// <summary>
    /// 设置无敌状态
    /// </summary>
    /// <param name="invulnerable">是否无敌</param>
    public void SetInvulnerable(bool invulnerable)
    {
        if (invulnerable && !isInvulnerable)
        {
            StartInvulnerability();
        }
        else if (!invulnerable && isInvulnerable)
        {
            EndInvulnerability();
        }
    }

    #endregion
}