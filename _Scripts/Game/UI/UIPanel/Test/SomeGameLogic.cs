using System.Collections;
using UnityEngine;

/// <summary>
/// 游戏逻辑示例类
/// 演示如何使用事件系统进行模块间通信
/// 包含玩家数据管理、定时器、状态机等常用功能
/// </summary>
public class SomeGameLogic : MonoBehaviour
{
    [Header("玩家设置")]
    [SerializeField] private int playerMaxHealth = 100;
    [SerializeField] private int playerLevel = 1;
    [SerializeField] private string playerName = "Player";
    
    [Header("游戏设置")]
    [SerializeField] private float healthRegenRate = 1f; // 每秒回血量
    [SerializeField] private float damageInterval = 3f; // 受伤间隔
    [SerializeField] private int damageAmount = 10; // 受伤数值
    
    [Header("调试选项")]
    [SerializeField] private bool enableAutoDemo = true;
    [SerializeField] private bool enableDebugLog = true;

    // 游戏状态
    private PlayerData _playerData;
    private bool _isGameRunning = false;
    private Coroutine _healthRegenCoroutine;
    private Coroutine _damageCoroutine;

    /// <summary>
    /// 游戏初始化
    /// </summary>
    void Start()
    {
        InitializePlayer();
        StartGame();
    }

    /// <summary>
    /// 帧更新
    /// </summary>
    void Update()
    {
        HandleInput();
        
        // 调试输入
        if (enableDebugLog && Input.GetKeyDown(KeyCode.Space))
        {
            ShowPlayerInfo();
        }
    }

    /// <summary>
    /// 初始化玩家数据
    /// </summary>
    private void InitializePlayer()
    {
        _playerData = new PlayerData
        {
            name = playerName,
            health = playerMaxHealth,
            level = playerLevel,
            experience = 0
        };

        if (enableDebugLog)
            Debug.Log($"玩家初始化完成: {_playerData.name}, HP:{_playerData.health}");
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame()
    {
        if (_isGameRunning) return;

        _isGameRunning = true;
        
        // 注册事件监听
        RegisterGameEvents();
        
        // 启动自动演示
        if (enableAutoDemo)
        {
            StartAutoDemo();
        }

        if (enableDebugLog)
            Debug.Log("游戏开始");
    }

    /// <summary>
    /// 停止游戏
    /// </summary>
    public void StopGame()
    {
        if (!_isGameRunning) return;

        _isGameRunning = false;
        
        // 停止所有协程
        StopAllCoroutines();
        
        // 注销事件监听
        UnregisterGameEvents();

        if (enableDebugLog)
            Debug.Log("游戏停止");
    }

    /// <summary>
    /// 注册游戏事件
    /// </summary>
    private void RegisterGameEvents()
    {
        EventDispatcher.Instance.Register(GameEvent.UI_PANEL_CLOSED, OnUIPanelClosed);
        EventDispatcher.Instance.Register(GameEvent.SCENE_LOAD_COMPLETED, OnSceneLoadCompleted);
    }

    /// <summary>
    /// 注销游戏事件
    /// </summary>
    private void UnregisterGameEvents()
    {
        EventDispatcher.Instance.Unregister(GameEvent.UI_PANEL_CLOSED, OnUIPanelClosed);
        EventDispatcher.Instance.Unregister(GameEvent.SCENE_LOAD_COMPLETED, OnSceneLoadCompleted);
    }

    /// <summary>
    /// 处理输入
    /// </summary>
    private void HandleInput()
    {
        if (!_isGameRunning) return;

        // 键盘输入处理
        if (Input.GetKeyDown(KeyCode.H))
        {
            TakeDamage(damageAmount);
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            HealPlayer(20);
        }
        
        if (Input.GetKeyDown(KeyCode.L))
        {
            LevelUp();
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            ShowPlayerInfo();
        }
    }

    /// <summary>
    /// 开始自动演示
    /// </summary>
    private void StartAutoDemo()
    {
        // 启动自动回血
        if (_healthRegenCoroutine == null)
        {
            _healthRegenCoroutine = StartCoroutine(HealthRegenCoroutine());
        }
        
        // 启动自动受伤
        if (_damageCoroutine == null)
        {
            _damageCoroutine = StartCoroutine(AutoDamageCoroutine());
        }
    }

    /// <summary>
    /// 自动回血协程
    /// </summary>
    private IEnumerator HealthRegenCoroutine()
    {
        while (_isGameRunning)
        {
            yield return new WaitForSeconds(1f);
            
            if (_playerData.health < playerMaxHealth)
            {
                HealPlayer(Mathf.RoundToInt(healthRegenRate));
            }
        }
    }

    /// <summary>
    /// 自动受伤协程
    /// </summary>
    private IEnumerator AutoDamageCoroutine()
    {
        yield return new WaitForSeconds(2f); // 延迟开始
        
        while (_isGameRunning)
        {
            yield return new WaitForSeconds(damageInterval);
            
            if (_playerData.health > 0)
            {
                TakeDamage(damageAmount);
            }
        }
    }

    #region 玩家操作方法

    /// <summary>
    /// 玩家受伤
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        if (!_isGameRunning || _playerData.health <= 0) return;

        _playerData.health = Mathf.Max(0, _playerData.health - damage);
        
        // 派发血量更新事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, _playerData.health);
        
        if (enableDebugLog)
            Debug.Log($"玩家受伤 -{damage}, 当前血量: {_playerData.health}");

        // 检查死亡
        if (_playerData.health <= 0)
        {
            OnPlayerDied();
        }
    }

    /// <summary>
    /// 玩家治疗
    /// </summary>
    /// <param name="healAmount">治疗量</param>
    public void HealPlayer(int healAmount)
    {
        if (!_isGameRunning || _playerData.health >= playerMaxHealth) return;

        _playerData.health = Mathf.Min(playerMaxHealth, _playerData.health + healAmount);
        
        // 派发血量更新事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, _playerData.health);
        
        if (enableDebugLog)
            Debug.Log($"玩家治疗 +{healAmount}, 当前血量: {_playerData.health}");
    }

    /// <summary>
    /// 玩家升级
    /// </summary>
    public void LevelUp()
    {
        if (!_isGameRunning) return;

        _playerData.level++;
        _playerData.experience = 0;
        
        // 升级时恢复满血
        _playerData.health = playerMaxHealth;
        
        // 派发升级事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_LEVEL_UP, _playerData);
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, _playerData.health);
        
        if (enableDebugLog)
            Debug.Log($"玩家升级! 当前等级: {_playerData.level}");
    }

    /// <summary>
    /// 玩家死亡处理
    /// </summary>
    private void OnPlayerDied()
    {
        // 派发死亡事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_DIED, _playerData);
        
        if (enableDebugLog)
            Debug.Log("玩家死亡!");
        
        // 停止游戏逻辑
        StopGame();
        
        // 3秒后复活
        StartCoroutine(RevivePlayer(3f));
    }

    /// <summary>
    /// 复活玩家
    /// </summary>
    private IEnumerator RevivePlayer(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 复活并恢复部分血量
        _playerData.health = playerMaxHealth / 2;
        
        // 重新开始游戏
        StartGame();
        
        // 派发血量更新事件
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, _playerData.health);
        
        if (enableDebugLog)
            Debug.Log("玩家复活!");
    }

    /// <summary>
    /// 显示玩家信息UI
    /// </summary>
    public void ShowPlayerInfo()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowView<PlayerInfoView>(_playerData);
        }
    }

    #endregion

    #region 事件处理器

    /// <summary>
    /// UI面板关闭事件处理
    /// </summary>
    private void OnUIPanelClosed(object payload)
    {
        if (payload is string panelName)
        {
            if (enableDebugLog)
                Debug.Log($"UI面板已关闭: {panelName}");
        }
    }

    /// <summary>
    /// 场景加载完成事件处理
    /// </summary>
    private void OnSceneLoadCompleted(object payload)
    {
        if (enableDebugLog)
            Debug.Log("场景加载完成，重新初始化游戏逻辑");
        
        // 重新初始化游戏
        InitializePlayer();
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 组件销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        StopGame();
        
        if (enableDebugLog)
            Debug.Log("游戏逻辑已清理");
    }

    /// <summary>
    /// 应用暂停时处理
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopGame();
        }
        else if (!_isGameRunning)
        {
            StartGame();
        }
    }

    #endregion

    #region 调试方法

    /// <summary>
    /// 获取当前玩家数据（用于调试）
    /// </summary>
    public PlayerData GetPlayerData()
    {
        return _playerData;
    }

    /// <summary>
    /// 设置玩家数据（用于调试）
    /// </summary>
    public void SetPlayerData(PlayerData playerData)
    {
        _playerData = playerData;
        EventDispatcher.Instance.Dispatch(GameEvent.PLAYER_HEALTH_UPDATED, _playerData.health);
    }

    #endregion
}
