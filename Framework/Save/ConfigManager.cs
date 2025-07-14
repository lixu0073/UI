using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 配置管理器
/// 管理游戏的各种配置设置，包括音频、图形、控制等
/// 支持配置的保存、加载和实时更新
/// </summary>
public class ConfigManager : MonoBehaviour
{
    [Header("配置文件设置")]
    [SerializeField] private string configFileName = "GameConfig";
    [SerializeField] private string configFileExtension = ".config";
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveDelay = 2f;
    
    [Header("默认配置")]
    [SerializeField] private GameConfig defaultConfig;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;

    // 单例实例
    private static ConfigManager _instance;
    public static ConfigManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ConfigManager");
                _instance = go.AddComponent<ConfigManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 配置数据
    private GameConfig _currentConfig;
    private bool _hasUnsavedChanges = false;
    private float _autoSaveTimer = 0f;
    
    // 配置文件路径
    private string ConfigFilePath => System.IO.Path.Combine(Application.persistentDataPath, $"{configFileName}{configFileExtension}");

    // 事件
    public System.Action<GameConfig> OnConfigLoaded;
    public System.Action<GameConfig> OnConfigSaved;
    public System.Action<string, object> OnConfigChanged;

    // 属性
    public GameConfig CurrentConfig => _currentConfig;
    public bool HasUnsavedChanges => _hasUnsavedChanges;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化配置管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("配置管理器已经初始化");
            return;
        }

        LogMessage("初始化配置管理器...");

        // 创建默认配置
        if (defaultConfig == null)
        {
            CreateDefaultConfig();
        }

        // 加载配置
        await LoadConfigAsync();

        IsInitialized = true;
        LogMessage("配置管理器初始化完成");
    }

    /// <summary>
    /// 创建默认配置
    /// </summary>
    private void CreateDefaultConfig()
    {
        defaultConfig = ScriptableObject.CreateInstance<GameConfig>();
        
        // 音频设置
        defaultConfig.audioSettings.masterVolume = 1f;
        defaultConfig.audioSettings.musicVolume = 0.8f;
        defaultConfig.audioSettings.sfxVolume = 1f;
        defaultConfig.audioSettings.voiceVolume = 1f;
        defaultConfig.audioSettings.isMuted = false;
        
        // 图形设置
        defaultConfig.graphicsSettings.resolution = new Vector2Int(1920, 1080);
        defaultConfig.graphicsSettings.fullscreen = true;
        defaultConfig.graphicsSettings.vsyncEnabled = true;
        defaultConfig.graphicsSettings.qualityLevel = 2;
        defaultConfig.graphicsSettings.targetFrameRate = 60;
        
        // 控制设置
        defaultConfig.controlSettings.mouseSensitivity = 1f;
        defaultConfig.controlSettings.invertMouseY = false;
        defaultConfig.controlSettings.keyBindings = new Dictionary<string, KeyCode>
        {
            { "Jump", KeyCode.Space },
            { "Attack", KeyCode.Z },
            { "Dash", KeyCode.X },
            { "Interact", KeyCode.E },
            { "Pause", KeyCode.Escape }
        };
        
        // 游戏设置
        defaultConfig.gameplaySettings.language = "Chinese";
        defaultConfig.gameplaySettings.showFPS = false;
        defaultConfig.gameplaySettings.enableTutorials = true;
        defaultConfig.gameplaySettings.autoSave = true;
        defaultConfig.gameplaySettings.difficulty = GameDifficulty.Normal;
        
        LogMessage("创建默认配置");
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        if (!IsInitialized) return;

        UpdateAutoSave();
    }

    /// <summary>
    /// 更新自动保存
    /// </summary>
    private void UpdateAutoSave()
    {
        if (!enableAutoSave || !_hasUnsavedChanges) return;

        _autoSaveTimer += Time.deltaTime;
        if (_autoSaveTimer >= autoSaveDelay)
        {
            SaveConfigAsync().Forget();
            _autoSaveTimer = 0f;
        }
    }

    /// <summary>
    /// 应用程序退出时保存配置
    /// </summary>
    private void OnApplicationQuit()
    {
        if (_hasUnsavedChanges)
        {
            SaveConfig();
        }
    }

    #endregion

    #region 配置加载和保存

    /// <summary>
    /// 异步加载配置
    /// </summary>
    /// <returns>加载任务</returns>
    public async UniTask LoadConfigAsync()
    {
        try
        {
            if (System.IO.File.Exists(ConfigFilePath))
            {
                string jsonData = await System.IO.File.ReadAllTextAsync(ConfigFilePath);
                GameConfigData configData = JsonUtility.FromJson<GameConfigData>(jsonData);
                
                if (configData != null)
                {
                    _currentConfig = ConvertToGameConfig(configData);
                    LogMessage("配置加载成功");
                }
                else
                {
                    _currentConfig = CreateConfigCopy(defaultConfig);
                    LogMessage("配置文件损坏，使用默认配置");
                }
            }
            else
            {
                _currentConfig = CreateConfigCopy(defaultConfig);
                LogMessage("配置文件不存在，使用默认配置");
            }

            // 应用配置
            ApplyConfig();
            
            OnConfigLoaded?.Invoke(_currentConfig);
        }
        catch (System.Exception e)
        {
            LogError($"加载配置失败: {e.Message}");
            _currentConfig = CreateConfigCopy(defaultConfig);
            ApplyConfig();
        }
    }

    /// <summary>
    /// 异步保存配置
    /// </summary>
    /// <returns>保存任务</returns>
    public async UniTask SaveConfigAsync()
    {
        try
        {
            GameConfigData configData = ConvertToConfigData(_currentConfig);
            string jsonData = JsonUtility.ToJson(configData, true);
            
            await System.IO.File.WriteAllTextAsync(ConfigFilePath, jsonData);
            
            _hasUnsavedChanges = false;
            OnConfigSaved?.Invoke(_currentConfig);
            LogMessage("配置保存成功");
        }
        catch (System.Exception e)
        {
            LogError($"保存配置失败: {e.Message}");
        }
    }

    /// <summary>
    /// 同步保存配置
    /// </summary>
    public void SaveConfig()
    {
        SaveConfigAsync().Forget();
    }

    #endregion

    #region 配置设置

    /// <summary>
    /// 设置音频配置
    /// </summary>
    /// <param name="masterVolume">主音量</param>
    /// <param name="musicVolume">音乐音量</param>
    /// <param name="sfxVolume">音效音量</param>
    /// <param name="voiceVolume">语音音量</param>
    public void SetAudioSettings(float masterVolume, float musicVolume, float sfxVolume, float voiceVolume)
    {
        _currentConfig.audioSettings.masterVolume = Mathf.Clamp01(masterVolume);
        _currentConfig.audioSettings.musicVolume = Mathf.Clamp01(musicVolume);
        _currentConfig.audioSettings.sfxVolume = Mathf.Clamp01(sfxVolume);
        _currentConfig.audioSettings.voiceVolume = Mathf.Clamp01(voiceVolume);
        
        MarkConfigDirty();
        ApplyAudioSettings();
        OnConfigChanged?.Invoke("AudioSettings", _currentConfig.audioSettings);
    }

    /// <summary>
    /// 设置静音状态
    /// </summary>
    /// <param name="isMuted">是否静音</param>
    public void SetMuted(bool isMuted)
    {
        _currentConfig.audioSettings.isMuted = isMuted;
        MarkConfigDirty();
        ApplyAudioSettings();
        OnConfigChanged?.Invoke("Muted", isMuted);
    }

    /// <summary>
    /// 设置图形配置
    /// </summary>
    /// <param name="resolution">分辨率</param>
    /// <param name="fullscreen">全屏</param>
    /// <param name="qualityLevel">质量等级</param>
    public void SetGraphicsSettings(Vector2Int resolution, bool fullscreen, int qualityLevel)
    {
        _currentConfig.graphicsSettings.resolution = resolution;
        _currentConfig.graphicsSettings.fullscreen = fullscreen;
        _currentConfig.graphicsSettings.qualityLevel = Mathf.Clamp(qualityLevel, 0, 5);
        
        MarkConfigDirty();
        ApplyGraphicsSettings();
        OnConfigChanged?.Invoke("GraphicsSettings", _currentConfig.graphicsSettings);
    }

    /// <summary>
    /// 设置VSync
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetVSync(bool enabled)
    {
        _currentConfig.graphicsSettings.vsyncEnabled = enabled;
        MarkConfigDirty();
        ApplyGraphicsSettings();
        OnConfigChanged?.Invoke("VSync", enabled);
    }

    /// <summary>
    /// 设置目标帧率
    /// </summary>
    /// <param name="frameRate">目标帧率</param>
    public void SetTargetFrameRate(int frameRate)
    {
        _currentConfig.graphicsSettings.targetFrameRate = Mathf.Clamp(frameRate, 30, 120);
        MarkConfigDirty();
        ApplyGraphicsSettings();
        OnConfigChanged?.Invoke("TargetFrameRate", frameRate);
    }

    /// <summary>
    /// 设置鼠标灵敏度
    /// </summary>
    /// <param name="sensitivity">灵敏度</param>
    public void SetMouseSensitivity(float sensitivity)
    {
        _currentConfig.controlSettings.mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
        MarkConfigDirty();
        OnConfigChanged?.Invoke("MouseSensitivity", sensitivity);
    }

    /// <summary>
    /// 设置鼠标Y轴反转
    /// </summary>
    /// <param name="invert">是否反转</param>
    public void SetInvertMouseY(bool invert)
    {
        _currentConfig.controlSettings.invertMouseY = invert;
        MarkConfigDirty();
        OnConfigChanged?.Invoke("InvertMouseY", invert);
    }

    /// <summary>
    /// 设置按键绑定
    /// </summary>
    /// <param name="action">动作名称</param>
    /// <param name="keyCode">按键代码</param>
    public void SetKeyBinding(string action, KeyCode keyCode)
    {
        _currentConfig.controlSettings.keyBindings[action] = keyCode;
        MarkConfigDirty();
        OnConfigChanged?.Invoke("KeyBinding", new { action, keyCode });
    }

    /// <summary>
    /// 设置语言
    /// </summary>
    /// <param name="language">语言</param>
    public void SetLanguage(string language)
    {
        _currentConfig.gameplaySettings.language = language;
        MarkConfigDirty();
        OnConfigChanged?.Invoke("Language", language);
    }

    /// <summary>
    /// 设置显示FPS
    /// </summary>
    /// <param name="show">是否显示</param>
    public void SetShowFPS(bool show)
    {
        _currentConfig.gameplaySettings.showFPS = show;
        MarkConfigDirty();
        OnConfigChanged?.Invoke("ShowFPS", show);
    }

    /// <summary>
    /// 设置游戏难度
    /// </summary>
    /// <param name="difficulty">难度</param>
    public void SetDifficulty(GameDifficulty difficulty)
    {
        _currentConfig.gameplaySettings.difficulty = difficulty;
        MarkConfigDirty();
        OnConfigChanged?.Invoke("Difficulty", difficulty);
    }

    #endregion

    #region 配置应用

    /// <summary>
    /// 应用所有配置
    /// </summary>
    private void ApplyConfig()
    {
        ApplyAudioSettings();
        ApplyGraphicsSettings();
        ApplyGameplaySettings();
    }

    /// <summary>
    /// 应用音频设置
    /// </summary>
    private void ApplyAudioSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(_currentConfig.audioSettings.masterVolume);
            AudioManager.Instance.SetMusicVolume(_currentConfig.audioSettings.musicVolume);
            AudioManager.Instance.SetSFXVolume(_currentConfig.audioSettings.sfxVolume);
            AudioManager.Instance.SetMuted(_currentConfig.audioSettings.isMuted);
        }
    }

    /// <summary>
    /// 应用图形设置
    /// </summary>
    private void ApplyGraphicsSettings()
    {
        var graphics = _currentConfig.graphicsSettings;
        
        // 设置分辨率和全屏
        Screen.SetResolution(graphics.resolution.x, graphics.resolution.y, graphics.fullscreen);
        
        // 设置质量等级
        QualitySettings.SetQualityLevel(graphics.qualityLevel);
        
        // 设置VSync
        QualitySettings.vSyncCount = graphics.vsyncEnabled ? 1 : 0;
        
        // 设置目标帧率
        Application.targetFrameRate = graphics.targetFrameRate;
    }

    /// <summary>
    /// 应用游戏设置
    /// </summary>
    private void ApplyGameplaySettings()
    {
        // 这里可以根据需要应用游戏设置
        LogMessage($"应用游戏设置: 语言={_currentConfig.gameplaySettings.language}, 难度={_currentConfig.gameplaySettings.difficulty}");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 重置为默认配置
    /// </summary>
    public void ResetToDefault()
    {
        _currentConfig = CreateConfigCopy(defaultConfig);
        ApplyConfig();
        MarkConfigDirty();
        OnConfigChanged?.Invoke("Reset", _currentConfig);
        LogMessage("重置为默认配置");
    }

    /// <summary>
    /// 标记配置已修改
    /// </summary>
    private void MarkConfigDirty()
    {
        _hasUnsavedChanges = true;
        _autoSaveTimer = 0f;
    }

    /// <summary>
    /// 创建配置副本
    /// </summary>
    /// <param name="source">源配置</param>
    /// <returns>配置副本</returns>
    private GameConfig CreateConfigCopy(GameConfig source)
    {
        GameConfig copy = ScriptableObject.CreateInstance<GameConfig>();
        copy.audioSettings = source.audioSettings;
        copy.graphicsSettings = source.graphicsSettings;
        copy.controlSettings = source.controlSettings;
        copy.gameplaySettings = source.gameplaySettings;
        return copy;
    }

    /// <summary>
    /// 转换为配置数据
    /// </summary>
    /// <param name="config">配置</param>
    /// <returns>配置数据</returns>
    private GameConfigData ConvertToConfigData(GameConfig config)
    {
        return new GameConfigData
        {
            audioSettings = config.audioSettings,
            graphicsSettings = config.graphicsSettings,
            controlSettings = config.controlSettings,
            gameplaySettings = config.gameplaySettings
        };
    }

    /// <summary>
    /// 转换为游戏配置
    /// </summary>
    /// <param name="data">配置数据</param>
    /// <returns>游戏配置</returns>
    private GameConfig ConvertToGameConfig(GameConfigData data)
    {
        GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
        config.audioSettings = data.audioSettings;
        config.graphicsSettings = data.graphicsSettings;
        config.controlSettings = data.controlSettings;
        config.gameplaySettings = data.gameplaySettings;
        return config;
    }

    /// <summary>
    /// 清理配置管理器
    /// </summary>
    public void Cleanup()
    {
        if (_hasUnsavedChanges)
        {
            SaveConfig();
        }
        LogMessage("配置管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[ConfigManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ConfigManager] {message}");
    }

    #endregion
}

/// <summary>
/// 游戏配置数据（用于序列化）
/// </summary>
[System.Serializable]
public class GameConfigData
{
    public AudioSettings audioSettings = new AudioSettings();
    public GraphicsSettings graphicsSettings = new GraphicsSettings();
    public ControlSettings controlSettings = new ControlSettings();
    public GameplaySettings gameplaySettings = new GameplaySettings();
}