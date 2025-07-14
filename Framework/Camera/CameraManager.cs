using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 相机管理器
/// 统一管理游戏中的所有相机，提供相机切换、特效等功能
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("相机设置")]
    [SerializeField] private Camera2DController mainCamera;
    [SerializeField] private bool autoFindMainCamera = true;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;

    // 单例实例
    private static CameraManager _instance;
    public static CameraManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CameraManager");
                _instance = go.AddComponent<CameraManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 相机引用
    private Camera2DController _currentCamera;

    // 属性
    public Camera2DController MainCamera => mainCamera;
    public Camera2DController CurrentCamera => _currentCamera;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化相机管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("相机管理器已经初始化");
            return;
        }

        LogMessage("初始化相机管理器...");

        // 自动查找主相机
        if (autoFindMainCamera && mainCamera == null)
        {
            FindMainCamera();
        }

        // 设置当前相机
        if (mainCamera != null)
        {
            _currentCamera = mainCamera;
        }

        IsInitialized = true;
        await UniTask.Yield();
        
        LogMessage("相机管理器初始化完成");
    }

    /// <summary>
    /// 查找主相机
    /// </summary>
    private void FindMainCamera()
    {
        Camera2DController[] cameras = FindObjectsOfType<Camera2DController>();
        if (cameras.Length > 0)
        {
            mainCamera = cameras[0];
            LogMessage($"自动找到主相机: {mainCamera.name}");
        }
        else
        {
            LogMessage("未找到Camera2DController组件");
        }
    }

    #endregion

    #region 相机控制

    /// <summary>
    /// 设置跟随目标
    /// </summary>
    /// <param name="target">目标</param>
    public void SetTarget(Transform target)
    {
        if (_currentCamera != null)
        {
            _currentCamera.SetTarget(target);
        }
    }

    /// <summary>
    /// 震动相机
    /// </summary>
    /// <param name="intensity">强度</param>
    /// <param name="duration">持续时间</param>
    public void Shake(float intensity = 1f, float duration = 0.5f)
    {
        if (_currentCamera != null)
        {
            _currentCamera.Shake(intensity, duration);
        }
    }

    /// <summary>
    /// 缩放相机
    /// </summary>
    /// <param name="size">目标大小</param>
    /// <param name="duration">持续时间</param>
    public void ZoomTo(float size, float duration = 1f)
    {
        if (_currentCamera != null)
        {
            _currentCamera.ZoomTo(size, duration);
        }
    }

    /// <summary>
    /// 移动相机到指定位置
    /// </summary>
    /// <param name="position">目标位置</param>
    /// <param name="duration">持续时间</param>
    public async UniTask MoveToAsync(Vector3 position, float duration = 1f)
    {
        if (_currentCamera != null)
        {
            await _currentCamera.MoveToAsync(position, duration);
        }
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        // 这里可以添加需要每帧更新的逻辑
    }

    /// <summary>
    /// 清理相机管理器
    /// </summary>
    public void Cleanup()
    {
        LogMessage("相机管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[CameraManager] {message}");
        }
    }

    #endregion
}