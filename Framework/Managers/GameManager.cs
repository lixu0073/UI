using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 2D游戏核心管理器
/// 负责游戏的整体生命周期管理、状态控制和各子系统的协调
/// 使用单例模式，确保全局唯一性
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("游戏设置")]
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private GameSettings gameSettings;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool showFPS = false;

    // 单例实例
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    // 游戏状态
    private GameState _currentState = GameState.None;
    private GameState _previousState = GameState.None;
    private bool _isInitialized = false;
    private bool _isPaused = false;

    // 子系统管理器
    private AudioManager _audioManager;
    private PoolManager _poolManager;
    private TweenManager _tweenManager;
    private InputManager _inputManager;
    private CameraManager _cameraManager;

    // 事件
    public static event Action<GameState, GameState> OnGameStateChanged;
    public static event Action OnGameInitialized;
    public static event Action<bool> OnGamePaused;

    // 属性
    public GameState CurrentState => _currentState;
    public GameState PreviousState => _previousState;
    public bool IsInitialized => _isInitialized;
    public bool IsPaused => _isPaused;
    public GameSettings Settings => gameSettings;

    #region Unity生命周期

    /// <summary>
    /// 游戏管理器初始化
    /// </summary>
    private void Awake()
    {
        InitializeSingleton();
        
        if (initializeOnAwake)
        {
            InitializeGameAsync().Forget();
        }
    }

    /// <summary>
    /// 游戏开始
    /// </summary>
    private void Start()
    {
        if (_isInitialized)
        {
            ChangeGameState(GameState.MainMenu);
        }
    }

    /// <summary>
    /// 帧更新
    /// </summary>
    private void Update()
    {
        if (!_isInitialized) return;

        // 处理输入
        HandleInput();
        
        // 更新子系统
        UpdateSubSystems();
    }

    /// <summary>
    /// 应用程序暂停处理
    /// </summary>
    /// <param name="pauseStatus">暂停状态</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }
    }

    /// <summary>
    /// 应用程序焦点处理
    /// </summary>
    /// <param name="hasFocus">是否有焦点</param>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _currentState == GameState.Playing)
        {
            PauseGame();
        }
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy()
    {
        CleanupSystems();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化单例
    /// </summary>
    private void InitializeSingleton()
    {
        if (_instance == null)
        {
            _instance = this;
            
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
            
            LogMessage("GameManager单例初始化完成");
        }
        else if (_instance != this)
        {
            LogMessage("GameManager实例已存在，销毁重复实例");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 异步初始化游戏
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeGameAsync()
    {
        if (_isInitialized)
        {
            LogMessage("游戏已经初始化");
            return;
        }

        LogMessage("开始初始化游戏...");
        ChangeGameState(GameState.Loading);

        try
        {
            // 初始化DOTween
            InitializeDOTween();
            
            // 初始化子系统
            await InitializeSubSystemsAsync();
            
            // 加载游戏设置
            await LoadGameSettingsAsync();
            
            // 初始化完成
            _isInitialized = true;
            LogMessage("游戏初始化完成");
            
            // 触发初始化完成事件
            OnGameInitialized?.Invoke();
            
            // 派发全局事件
            EventDispatcher.Instance.Dispatch(GameEvent.GAME_INITIALIZED);
        }
        catch (Exception e)
        {
            LogError($"游戏初始化失败: {e.Message}");
            ChangeGameState(GameState.Error);
        }
    }

    /// <summary>
    /// 初始化DOTween
    /// </summary>
    private void InitializeDOTween()
    {
        // 配置DOTween
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        DOTween.SetTweensCapacity(500, 50);
        
        LogMessage("DOTween初始化完成");
    }

    /// <summary>
    /// 异步初始化子系统
    /// </summary>
    private async UniTask InitializeSubSystemsAsync()
    {
        LogMessage("初始化子系统...");

        // 初始化音频管理器
        _audioManager = AudioManager.Instance;
        await _audioManager.InitializeAsync();

        // 初始化对象池管理器
        _poolManager = PoolManager.Instance;
        await _poolManager.InitializeAsync();

        // 初始化补间管理器
        _tweenManager = TweenManager.Instance;
        await _tweenManager.InitializeAsync();

        // 初始化输入管理器
        _inputManager = InputManager.Instance;
        await _inputManager.InitializeAsync();

        // 初始化相机管理器
        _cameraManager = CameraManager.Instance;
        await _cameraManager.InitializeAsync();

        LogMessage("子系统初始化完成");
    }

    /// <summary>
    /// 异步加载游戏设置
    /// </summary>
    private async UniTask LoadGameSettingsAsync()
    {
        if (gameSettings == null)
        {
            // 尝试从Resources加载默认设置
            gameSettings = Resources.Load<GameSettings>("GameSettings");
            
            if (gameSettings == null)
            {
                LogWarning("未找到游戏设置文件，使用默认设置");
                gameSettings = ScriptableObject.CreateInstance<GameSettings>();
            }
        }

        // 应用设置
        ApplyGameSettings();
        
        await UniTask.Yield();
        LogMessage("游戏设置加载完成");
    }

    #endregion

    #region 游戏状态管理

    /// <summary>
    /// 改变游戏状态
    /// </summary>
    /// <param name="newState">新状态</param>
    public void ChangeGameState(GameState newState)
    {
        if (_currentState == newState) return;

        GameState oldState = _currentState;
        _previousState = _currentState;
        _currentState = newState;

        LogMessage($"游戏状态改变: {oldState} -> {newState}");

        // 处理状态退出
        OnExitGameState(oldState);
        
        // 处理状态进入
        OnEnterGameState(newState);

        // 触发状态改变事件
        OnGameStateChanged?.Invoke(oldState, newState);
        
        // 派发全局事件
        EventDispatcher.Instance.Dispatch(GameEvent.GAME_STATE_CHANGED, 
            new GameStateChangeData { oldState = oldState, newState = newState });
    }

    /// <summary>
    /// 进入游戏状态处理
    /// </summary>
    /// <param name="state">进入的状态</param>
    private void OnEnterGameState(GameState state)
    {
        switch (state)
        {
            case GameState.Loading:
                OnEnterLoadingState();
                break;
            case GameState.MainMenu:
                OnEnterMainMenuState();
                break;
            case GameState.Playing:
                OnEnterPlayingState();
                break;
            case GameState.Paused:
                OnEnterPausedState();
                break;
            case GameState.GameOver:
                OnEnterGameOverState();
                break;
            case GameState.Error:
                OnEnterErrorState();
                break;
        }
    }

    /// <summary>
    /// 退出游戏状态处理
    /// </summary>
    /// <param name="state">退出的状态</param>
    private void OnExitGameState(GameState state)
    {
        switch (state)
        {
            case GameState.Loading:
                OnExitLoadingState();
                break;
            case GameState.MainMenu:
                OnExitMainMenuState();
                break;
            case GameState.Playing:
                OnExitPlayingState();
                break;
            case GameState.Paused:
                OnExitPausedState();
                break;
            case GameState.GameOver:
                OnExitGameOverState();
                break;
        }
    }

    #endregion

    #region 状态处理方法

    private void OnEnterLoadingState()
    {
        Time.timeScale = 1f;
        // 显示加载界面
        UIManager.Instance?.ShowView<LoadingView>();
    }

    private void OnExitLoadingState()
    {
        // 隐藏加载界面
        UIManager.Instance?.HideView<LoadingView>();
    }

    private void OnEnterMainMenuState()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        // 显示主菜单
        UIManager.Instance?.ShowView<MainMenuView>();
        _audioManager?.PlayMusic("MainMenuMusic");
    }

    private void OnExitMainMenuState()
    {
        // 隐藏主菜单
        UIManager.Instance?.HideView<MainMenuView>();
    }

    private void OnEnterPlayingState()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        // 显示游戏HUD
        UIManager.Instance?.ShowView<GameHUDView>();
        _audioManager?.PlayMusic("GameplayMusic");
    }

    private void OnExitPlayingState()
    {
        // 可能需要保存游戏进度
    }

    private void OnEnterPausedState()
    {
        Time.timeScale = 0f;
        _isPaused = true;
        // 显示暂停菜单
        UIManager.Instance?.ShowView<PauseMenuView>();
        _audioManager?.PauseMusic();
    }

    private void OnExitPausedState()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        // 隐藏暂停菜单
        UIManager.Instance?.HideView<PauseMenuView>();
        _audioManager?.ResumeMusic();
    }

    private void OnEnterGameOverState()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        // 显示游戏结束界面
        UIManager.Instance?.ShowView<GameOverView>();
        _audioManager?.StopMusic();
        _audioManager?.PlaySFX("GameOver");
    }

    private void OnExitGameOverState()
    {
        // 隐藏游戏结束界面
        UIManager.Instance?.HideView<GameOverView>();
    }

    private void OnEnterErrorState()
    {
        Time.timeScale = 1f;
        LogError("游戏进入错误状态");
        // 显示错误界面
        UIManager.Instance?.ShowView<ErrorView>();
    }

    #endregion

    #region 游戏控制

    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame()
    {
        if (!_isInitialized)
        {
            LogWarning("游戏尚未初始化，无法开始游戏");
            return;
        }

        ChangeGameState(GameState.Playing);
        LogMessage("游戏开始");
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        if (_currentState == GameState.Playing)
        {
            ChangeGameState(GameState.Paused);
            OnGamePaused?.Invoke(true);
            LogMessage("游戏暂停");
        }
    }

    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void ResumeGame()
    {
        if (_currentState == GameState.Paused)
        {
            ChangeGameState(GameState.Playing);
            OnGamePaused?.Invoke(false);
            LogMessage("游戏恢复");
        }
    }

    /// <summary>
    /// 重启游戏
    /// </summary>
    public async UniTask RestartGameAsync()
    {
        LogMessage("重启游戏");
        
        // 清理当前游戏状态
        CleanupGame();
        
        // 重新初始化
        await InitializeGameAsync();
        
        // 开始游戏
        StartGame();
    }

    /// <summary>
    /// 退出到主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        CleanupGame();
        ChangeGameState(GameState.MainMenu);
        LogMessage("返回主菜单");
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GameOver()
    {
        ChangeGameState(GameState.GameOver);
        LogMessage("游戏结束");
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
        LogMessage("退出游戏");
        
        // 清理所有系统
        CleanupSystems();
        
        // 保存数据
        SaveGameData();
        
        // 退出应用
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 处理输入
    /// </summary>
    private void HandleInput()
    {
        // ESC键暂停/恢复游戏
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (_currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        // F11切换全屏
        if (Input.GetKeyDown(KeyCode.F11))
        {
            ToggleFullscreen();
        }
    }

    /// <summary>
    /// 更新子系统
    /// </summary>
    private void UpdateSubSystems()
    {
        // 更新音频管理器
        _audioManager?.Update();
        
        // 更新对象池管理器
        _poolManager?.Update();
        
        // 更新补间管理器
        _tweenManager?.Update();
        
        // 更新输入管理器
        _inputManager?.Update();
        
        // 更新相机管理器
        _cameraManager?.Update();
    }

    /// <summary>
    /// 应用游戏设置
    /// </summary>
    private void ApplyGameSettings()
    {
        if (gameSettings == null) return;

        // 应用音频设置
        _audioManager?.SetMasterVolume(gameSettings.masterVolume);
        _audioManager?.SetMusicVolume(gameSettings.musicVolume);
        _audioManager?.SetSFXVolume(gameSettings.sfxVolume);

        // 应用显示设置
        Screen.SetResolution(
            gameSettings.screenWidth, 
            gameSettings.screenHeight, 
            gameSettings.fullscreen
        );
        
        Application.targetFrameRate = gameSettings.targetFrameRate;
        QualitySettings.vSyncCount = gameSettings.vSync ? 1 : 0;

        LogMessage("游戏设置已应用");
    }

    /// <summary>
    /// 切换全屏模式
    /// </summary>
    private void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        LogMessage($"全屏模式: {Screen.fullScreen}");
    }

    /// <summary>
    /// 清理游戏状态
    /// </summary>
    private void CleanupGame()
    {
        // 清理对象池
        _poolManager?.ClearAll();
        
        // 停止所有音效
        _audioManager?.StopAll();
        
        // 停止所有补间动画
        _tweenManager?.KillAll();
        
        // 隐藏所有UI
        UIManager.Instance?.HideAllViews();
        
        LogMessage("游戏状态已清理");
    }

    /// <summary>
    /// 清理所有系统
    /// </summary>
    private void CleanupSystems()
    {
        _audioManager?.Cleanup();
        _poolManager?.Cleanup();
        _tweenManager?.Cleanup();
        _inputManager?.Cleanup();
        _cameraManager?.Cleanup();
        
        LogMessage("所有系统已清理");
    }

    /// <summary>
    /// 保存游戏数据
    /// </summary>
    private void SaveGameData()
    {
        // TODO: 实现保存逻辑
        LogMessage("游戏数据已保存");
    }

    #endregion

    #region 日志方法

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志信息</param>
    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">警告信息</param>
    private void LogWarning(string message)
    {
        if (enableDebugLog)
        {
            Debug.LogWarning($"[GameManager] {message}");
        }
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">错误信息</param>
    private void LogError(string message)
    {
        Debug.LogError($"[GameManager] {message}");
    }

    #endregion
}

/// <summary>
/// 游戏状态枚举
/// </summary>
public enum GameState
{
    None,           // 无状态
    Loading,        // 加载中
    MainMenu,       // 主菜单
    Playing,        // 游戏中
    Paused,         // 暂停
    GameOver,       // 游戏结束
    Error           // 错误状态
}

/// <summary>
/// 游戏状态改变数据
/// </summary>
[Serializable]
public class GameStateChangeData
{
    public GameState oldState;
    public GameState newState;
}