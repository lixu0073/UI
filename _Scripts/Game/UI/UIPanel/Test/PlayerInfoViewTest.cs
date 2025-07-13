using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家信息视图测试类
/// 继承自UIBaseView，展示玩家血量、姓名等信息
/// 演示了事件系统的使用方法
/// </summary>
public class PlayerInfoView : UIBaseView
{
    [Header("UI组件")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Button closeButton;
    
    [Header("玩家数据")]
    [SerializeField] private int maxHealth = 100;
    
    private int _currentHealth;
    private string _playerName;

    /// <summary>
    /// 初始化UI组件
    /// </summary>
    protected override void OnInitialize()
    {
        base.OnInitialize();
        
        // 绑定按钮事件
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // 初始化血量滑动条
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = maxHealth;
        }
        
        // 设置默认值
        _currentHealth = maxHealth;
        UpdateHealthDisplay();
    }

    /// <summary>
    /// 显示视图时的处理
    /// </summary>
    /// <param name="args">显示参数</param>
    public override void OnShow(object args = null)
    {
        base.OnShow(args);

        // 视图显示时，注册关心的数据更新事件
        RegisterEvent(GameEvent.PLAYER_HEALTH_UPDATED, OnHealthUpdated);
        RegisterEvent(GameEvent.PLAYER_LEVEL_UP, OnPlayerLevelUp);

        // 处理传入的参数
        ProcessShowArguments(args);
        
        // 刷新UI显示
        RefreshDisplay();
    }

    /// <summary>
    /// 隐藏视图时的处理
    /// </summary>
    public override void OnHide()
    {
        base.OnHide();
        
        // 在OnDestroyView中会自动注销事件，这里可以不手动注销
        // 如果需要在隐藏时立即注销，可以在这里调用UnregisterEvent
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    protected override void OnCleanup()
    {
        base.OnCleanup();
        
        // 清理按钮事件
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 处理显示参数
    /// </summary>
    /// <param name="args">参数对象</param>
    private void ProcessShowArguments(object args)
    {
        // 支持多种参数类型
        switch (args)
        {
            case string playerName:
                SetPlayerName(playerName);
                break;
                
            case PlayerData playerData:
                SetPlayerData(playerData);
                break;
                
            case null:
                // 使用默认值
                SetPlayerName("Unknown Player");
                break;
                
            default:
                Debug.LogWarning($"不支持的参数类型: {args.GetType()}");
                break;
        }
    }

    /// <summary>
    /// 设置玩家姓名
    /// </summary>
    /// <param name="playerName">玩家姓名</param>
    public void SetPlayerName(string playerName)
    {
        _playerName = playerName;
        if (nameText != null)
        {
            nameText.text = _playerName;
        }
    }

    /// <summary>
    /// 设置玩家数据
    /// </summary>
    /// <param name="playerData">玩家数据</param>
    public void SetPlayerData(PlayerData playerData)
    {
        if (playerData != null)
        {
            SetPlayerName(playerData.name);
            SetHealth(playerData.health);
        }
    }

    /// <summary>
    /// 设置血量
    /// </summary>
    /// <param name="health">血量值</param>
    public void SetHealth(int health)
    {
        _currentHealth = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthDisplay();
    }

    /// <summary>
    /// 刷新整体显示
    /// </summary>
    private void RefreshDisplay()
    {
        UpdateHealthDisplay();
        
        if (nameText != null && string.IsNullOrEmpty(nameText.text))
        {
            nameText.text = _playerName ?? "Unknown Player";
        }
    }

    /// <summary>
    /// 更新血量显示
    /// </summary>
    private void UpdateHealthDisplay()
    {
        // 更新血量文本
        if (healthText != null)
        {
            healthText.text = $"HP: {_currentHealth}/{maxHealth}";
            
            // 根据血量设置颜色
            Color healthColor = GetHealthColor(_currentHealth, maxHealth);
            healthText.color = healthColor;
        }

        // 更新血量滑动条
        if (healthSlider != null)
        {
            healthSlider.value = _currentHealth;
        }
    }

    /// <summary>
    /// 根据血量获取颜色
    /// </summary>
    /// <param name="current">当前血量</param>
    /// <param name="max">最大血量</param>
    /// <returns>对应的颜色</returns>
    private Color GetHealthColor(int current, int max)
    {
        float ratio = (float)current / max;
        
        if (ratio > 0.6f)
            return Color.green;
        else if (ratio > 0.3f)
            return Color.yellow;
        else
            return Color.red;
    }

    #region 事件处理器

    /// <summary>
    /// 处理血量更新事件
    /// </summary>
    /// <param name="payload">事件数据</param>
    private void OnHealthUpdated(object payload)
    {
        if (payload is int newHealth)
        {
            SetHealth(newHealth);
            
            // 播放血量变化效果
            PlayHealthChangeEffect();
        }
        else if (payload is PlayerData playerData)
        {
            SetHealth(playerData.health);
        }
    }

    /// <summary>
    /// 处理玩家升级事件
    /// </summary>
    /// <param name="payload">事件数据</param>
    private void OnPlayerLevelUp(object payload)
    {
        // 升级时恢复满血量
        SetHealth(maxHealth);
        
        // 播放升级特效
        PlayLevelUpEffect();
    }

    #endregion

    #region UI事件处理

    /// <summary>
    /// 关闭按钮点击处理
    /// </summary>
    private void OnCloseButtonClicked()
    {
        // 派发UI关闭事件
        DispatchEvent(GameEvent.UI_PANEL_CLOSED, this.GetType().Name);
        
        // 隐藏视图
        UIManager.Instance?.HideView<PlayerInfoView>();
    }

    #endregion

    #region 特效方法

    /// <summary>
    /// 播放血量变化特效
    /// </summary>
    private void PlayHealthChangeEffect()
    {
        if (healthText != null)
        {
            // 简单的缩放动画效果
            LeanTween.scale(healthText.gameObject, Vector3.one * 1.2f, 0.1f)
                     .setEase(LeanTweenType.easeOutQuad)
                     .setLoopPingPong(1);
        }
    }

    /// <summary>
    /// 播放升级特效
    /// </summary>
    private void PlayLevelUpEffect()
    {
        if (nameText != null)
        {
            // 简单的闪烁效果
            LeanTween.alpha(nameText.gameObject, 0.3f, 0.2f)
                     .setEase(LeanTweenType.easeInOutQuad)
                     .setLoopPingPong(2);
        }
    }

    #endregion
}

/// <summary>
/// 玩家数据结构
/// </summary>
[System.Serializable]
public class PlayerData
{
    public string name;
    public int health;
    public int level;
    public int experience;
}