using UnityEngine;

/// <summary>
/// 游戏设置配置文件
/// 使用ScriptableObject存储游戏的各种配置参数
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "Framework/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("显示设置")]
    [SerializeField] public int screenWidth = 1920;
    [SerializeField] public int screenHeight = 1080;
    [SerializeField] public bool fullscreen = true;
    [SerializeField] public int targetFrameRate = 60;
    [SerializeField] public bool vSync = true;

    [Header("音频设置")]
    [Range(0f, 1f)]
    [SerializeField] public float masterVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] public float musicVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] public float sfxVolume = 1f;

    [Header("游戏性设置")]
    [SerializeField] public float gameSpeed = 1f;
    [SerializeField] public int maxLives = 3;
    [SerializeField] public bool autoSave = true;
    [SerializeField] public float autoSaveInterval = 300f; // 5分钟

    [Header("2D游戏设置")]
    [SerializeField] public float pixelsPerUnit = 100f;
    [SerializeField] public bool enablePixelPerfect = true;
    [SerializeField] public LayerMask groundLayerMask = 1;
    [SerializeField] public LayerMask enemyLayerMask = 1 << 8;
    [SerializeField] public LayerMask playerLayerMask = 1 << 9;

    [Header("物理设置")]
    [SerializeField] public float gravity = -9.81f;
    [SerializeField] public float maxFallSpeed = -20f;
    [SerializeField] public bool enablePhysicsDebug = false;

    [Header("调试设置")]
    [SerializeField] public bool enableDebugMode = false;
    [SerializeField] public bool showFPS = false;
    [SerializeField] public bool enableCheatCodes = false;
}