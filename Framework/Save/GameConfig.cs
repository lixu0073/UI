using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏配置ScriptableObject
/// 包含所有游戏配置设置的数据结构
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game Framework/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("音频设置")]
    public AudioSettings audioSettings = new AudioSettings();
    
    [Header("图形设置")]
    public GraphicsSettings graphicsSettings = new GraphicsSettings();
    
    [Header("控制设置")]
    public ControlSettings controlSettings = new ControlSettings();
    
    [Header("游戏设置")]
    public GameplaySettings gameplaySettings = new GameplaySettings();
}

/// <summary>
/// 音频设置
/// </summary>
[System.Serializable]
public class AudioSettings
{
    [Header("音量设置")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    
    [Header("音频选项")]
    public bool isMuted = false;
    public bool enableSpatialAudio = true;
    public int audioQuality = 1; // 0=低, 1=中, 2=高
}

/// <summary>
/// 图形设置
/// </summary>
[System.Serializable]
public class GraphicsSettings
{
    [Header("显示设置")]
    public Vector2Int resolution = new Vector2Int(1920, 1080);
    public bool fullscreen = true;
    public bool vsyncEnabled = true;
    public int targetFrameRate = 60;
    
    [Header("质量设置")]
    [Range(0, 5)] public int qualityLevel = 2;
    public bool enableAntiAliasing = true;
    public bool enablePostProcessing = true;
    public float renderScale = 1f;
    
    [Header("性能设置")]
    public bool enableBatching = true;
    public bool enableGPUInstancing = true;
    public int maxLODLevel = 2;
}

/// <summary>
/// 控制设置
/// </summary>
[System.Serializable]
public class ControlSettings
{
    [Header("鼠标设置")]
    [Range(0.1f, 5f)] public float mouseSensitivity = 1f;
    public bool invertMouseY = false;
    
    [Header("触摸设置")]
    [Range(0.1f, 3f)] public float touchSensitivity = 1f;
    public bool enableGestures = true;
    
    [Header("按键绑定")]
    public Dictionary<string, KeyCode> keyBindings = new Dictionary<string, KeyCode>
    {
        { "Jump", KeyCode.Space },
        { "Attack", KeyCode.Z },
        { "Dash", KeyCode.X },
        { "Interact", KeyCode.E },
        { "Pause", KeyCode.Escape },
        { "Menu", KeyCode.Tab }
    };
    
    [Header("控制选项")]
    public bool enableVibration = true;
    public float vibrationIntensity = 1f;
}

/// <summary>
/// 游戏设置
/// </summary>
[System.Serializable]
public class GameplaySettings
{
    [Header("游戏选项")]
    public string language = "Chinese";
    public GameDifficulty difficulty = GameDifficulty.Normal;
    public bool autoSave = true;
    public float autoSaveInterval = 300f; // 5分钟
    
    [Header("界面选项")]
    public bool showFPS = false;
    public bool showMinimap = true;
    public bool enableTutorials = true;
    public bool showDamageNumbers = true;
    
    [Header("辅助功能")]
    public bool enableColorBlindMode = false;
    public bool enableSubtitles = false;
    public float uiScale = 1f;
    public bool enableScreenReader = false;
    
    [Header("调试选项")]
    public bool enableDebugMode = false;
    public bool showColliders = false;
    public bool enableConsole = false;
}

/// <summary>
/// 游戏难度枚举
/// </summary>
public enum GameDifficulty
{
    Easy,     // 简单
    Normal,   // 普通
    Hard,     // 困难
    Expert    // 专家
}

/// <summary>
/// 语言枚举
/// </summary>
public enum GameLanguage
{
    Chinese,  // 中文
    English,  // 英文
    Japanese, // 日文
    Korean    // 韩文
}