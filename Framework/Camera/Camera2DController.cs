using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 2D相机控制器
/// 专门用于2D游戏的相机控制，支持跟随、平滑移动、震动、缩放等功能
/// 提供多种跟随模式和边界限制
/// </summary>
public class Camera2DController : MonoBehaviour
{
    [Header("跟随设置")]
    [SerializeField] private Transform target; // 跟随目标
    [SerializeField] private CameraFollowMode followMode = CameraFollowMode.Smooth;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float smoothTime = 0.3f;
    [SerializeField] private Vector2 offset = Vector2.zero;
    
    [Header("边界设置")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Bounds cameraBounds;
    [SerializeField] private float boundsPadding = 1f;
    
    [Header("死区设置")]
    [SerializeField] private bool useDeadZone = false;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(2f, 1f);
    
    [Header("缩放设置")]
    [SerializeField] private float defaultSize = 5f;
    [SerializeField] private float minSize = 2f;
    [SerializeField] private float maxSize = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    
    [Header("震动设置")]
    [SerializeField] private bool enableShake = true;
    [SerializeField] private float maxShakeIntensity = 1f;
    
    [Header("预测设置")]
    [SerializeField] private bool enablePrediction = false;
    [SerializeField] private float predictionTime = 0.5f;
    [SerializeField] private float predictionInfluence = 0.3f;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showDebugGizmos = false;

    // 组件引用
    private Camera _camera;
    private TweenManager _tweenManager;

    // 跟随状态
    private Vector3 _velocity;
    private Vector3 _targetPosition;
    private Vector3 _currentPosition;
    private Vector3 _lastTargetPosition;
    
    // 震动状态
    private bool _isShaking = false;
    private Vector3 _shakeOffset;
    private Tween _shakeTween;
    
    // 缩放状态
    private float _targetSize;
    private bool _isZooming = false;
    
    // 边界计算缓存
    private Vector3 _minBounds;
    private Vector3 _maxBounds;

    // 属性
    public Transform Target
    {
        get => target;
        set => SetTarget(value);
    }
    
    public CameraFollowMode FollowMode
    {
        get => followMode;
        set => followMode = value;
    }
    
    public float CurrentSize => _camera.orthographicSize;
    public bool IsShaking => _isShaking;
    public Vector3 WorldPosition => transform.position;

    // 事件
    public System.Action<Transform> OnTargetChanged;
    public System.Action<float> OnZoomChanged;
    public System.Action OnShakeStarted;
    public System.Action OnShakeEnded;

    #region Unity生命周期

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        InitializeCamera();
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            UpdateCameraPosition();
        }
        
        UpdateBounds();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        DrawDebugGizmos();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            LogError("未找到Camera组件！");
            return;
        }

        _tweenManager = TweenManager.Instance;
        LogMessage("相机控制器组件初始化完成");
    }

    /// <summary>
    /// 初始化相机
    /// </summary>
    private void InitializeCamera()
    {
        // 设置为2D相机
        _camera.orthographic = true;
        _camera.orthographicSize = defaultSize;
        _targetSize = defaultSize;

        // 初始化位置
        _currentPosition = transform.position;
        _targetPosition = _currentPosition;
        
        if (target != null)
        {
            _lastTargetPosition = target.position;
        }

        UpdateBounds();
        LogMessage("相机初始化完成");
    }

    #endregion

    #region 目标跟随

    /// <summary>
    /// 设置跟随目标
    /// </summary>
    /// <param name="newTarget">新目标</param>
    public void SetTarget(Transform newTarget)
    {
        Transform oldTarget = target;
        target = newTarget;
        
        if (target != null)
        {
            _lastTargetPosition = target.position;
        }
        
        OnTargetChanged?.Invoke(newTarget);
        LogMessage($"设置跟随目标: {(newTarget ? newTarget.name : "null")}");
    }

    /// <summary>
    /// 更新相机位置
    /// </summary>
    private void UpdateCameraPosition()
    {
        // 计算目标位置
        CalculateTargetPosition();
        
        // 根据跟随模式移动相机
        switch (followMode)
        {
            case CameraFollowMode.Instant:
                MoveInstant();
                break;
            case CameraFollowMode.Smooth:
                MoveSmooth();
                break;
            case CameraFollowMode.Lerp:
                MoveLerp();
                break;
            case CameraFollowMode.DeadZone:
                MoveDeadZone();
                break;
        }
        
        // 应用边界限制
        if (useBounds)
        {
            ApplyBounds();
        }
        
        // 应用震动偏移
        Vector3 finalPosition = _currentPosition + _shakeOffset;
        transform.position = new Vector3(finalPosition.x, finalPosition.y, transform.position.z);
    }

    /// <summary>
    /// 计算目标位置
    /// </summary>
    private void CalculateTargetPosition()
    {
        Vector3 baseTarget = target.position + (Vector3)offset;
        
        // 添加预测
        if (enablePrediction && Time.deltaTime > 0)
        {
            Vector3 velocity = (target.position - _lastTargetPosition) / Time.deltaTime;
            Vector3 prediction = velocity * predictionTime;
            baseTarget += prediction * predictionInfluence;
        }
        
        _targetPosition = new Vector3(baseTarget.x, baseTarget.y, transform.position.z);
        _lastTargetPosition = target.position;
    }

    /// <summary>
    /// 立即移动
    /// </summary>
    private void MoveInstant()
    {
        _currentPosition = _targetPosition;
    }

    /// <summary>
    /// 平滑移动
    /// </summary>
    private void MoveSmooth()
    {
        _currentPosition = Vector3.SmoothDamp(_currentPosition, _targetPosition, ref _velocity, smoothTime);
    }

    /// <summary>
    /// 线性插值移动
    /// </summary>
    private void MoveLerp()
    {
        _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, followSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 死区跟随
    /// </summary>
    private void MoveDeadZone()
    {
        Vector3 difference = _targetPosition - _currentPosition;
        
        // 计算死区边界
        float halfWidth = deadZoneSize.x * 0.5f;
        float halfHeight = deadZoneSize.y * 0.5f;
        
        Vector3 movement = Vector3.zero;
        
        // X轴移动
        if (Mathf.Abs(difference.x) > halfWidth)
        {
            movement.x = difference.x - Mathf.Sign(difference.x) * halfWidth;
        }
        
        // Y轴移动
        if (Mathf.Abs(difference.y) > halfHeight)
        {
            movement.y = difference.y - Mathf.Sign(difference.y) * halfHeight;
        }
        
        if (movement.magnitude > 0)
        {
            _currentPosition = Vector3.SmoothDamp(_currentPosition, _currentPosition + movement, ref _velocity, smoothTime);
        }
    }

    #endregion

    #region 边界控制

    /// <summary>
    /// 更新边界计算
    /// </summary>
    private void UpdateBounds()
    {
        if (!useBounds) return;
        
        float cameraHeight = _camera.orthographicSize;
        float cameraWidth = cameraHeight * _camera.aspect;
        
        _minBounds = new Vector3(
            cameraBounds.min.x + cameraWidth + boundsPadding,
            cameraBounds.min.y + cameraHeight + boundsPadding,
            transform.position.z
        );
        
        _maxBounds = new Vector3(
            cameraBounds.max.x - cameraWidth - boundsPadding,
            cameraBounds.max.y - cameraHeight - boundsPadding,
            transform.position.z
        );
    }

    /// <summary>
    /// 应用边界限制
    /// </summary>
    private void ApplyBounds()
    {
        _currentPosition.x = Mathf.Clamp(_currentPosition.x, _minBounds.x, _maxBounds.x);
        _currentPosition.y = Mathf.Clamp(_currentPosition.y, _minBounds.y, _maxBounds.y);
    }

    /// <summary>
    /// 设置相机边界
    /// </summary>
    /// <param name="bounds">边界</param>
    public void SetBounds(Bounds bounds)
    {
        cameraBounds = bounds;
        useBounds = true;
        UpdateBounds();
        LogMessage($"设置相机边界: {bounds}");
    }

    /// <summary>
    /// 禁用边界
    /// </summary>
    public void DisableBounds()
    {
        useBounds = false;
        LogMessage("禁用相机边界");
    }

    #endregion

    #region 缩放控制

    /// <summary>
    /// 设置相机大小
    /// </summary>
    /// <param name="size">目标大小</param>
    /// <param name="duration">过渡时间</param>
    public void SetSize(float size, float duration = 0f)
    {
        _targetSize = Mathf.Clamp(size, minSize, maxSize);
        
        if (duration <= 0)
        {
            _camera.orthographicSize = _targetSize;
            UpdateBounds();
        }
        else
        {
            ZoomTo(_targetSize, duration);
        }
        
        LogMessage($"设置相机大小: {_targetSize}");
    }

    /// <summary>
    /// 缩放到指定大小
    /// </summary>
    /// <param name="targetSize">目标大小</param>
    /// <param name="duration">持续时间</param>
    public void ZoomTo(float targetSize, float duration = 1f)
    {
        if (_isZooming)
        {
            DOTween.Kill(_camera);
        }
        
        _isZooming = true;
        _targetSize = Mathf.Clamp(targetSize, minSize, maxSize);
        
        _camera.DOOrthoSize(_targetSize, duration)
               .SetEase(Ease.OutQuad)
               .OnUpdate(UpdateBounds)
               .OnComplete(() => {
                   _isZooming = false;
                   OnZoomChanged?.Invoke(_targetSize);
               });
        
        LogMessage($"缩放到: {_targetSize}, 时间: {duration}");
    }

    /// <summary>
    /// 放大
    /// </summary>
    /// <param name="factor">放大倍数</param>
    /// <param name="duration">持续时间</param>
    public void ZoomIn(float factor = 0.5f, float duration = 1f)
    {
        float newSize = _camera.orthographicSize * factor;
        ZoomTo(newSize, duration);
    }

    /// <summary>
    /// 缩小
    /// </summary>
    /// <param name="factor">缩小倍数</param>
    /// <param name="duration">持续时间</param>
    public void ZoomOut(float factor = 2f, float duration = 1f)
    {
        float newSize = _camera.orthographicSize * factor;
        ZoomTo(newSize, duration);
    }

    /// <summary>
    /// 重置缩放
    /// </summary>
    /// <param name="duration">持续时间</param>
    public void ResetZoom(float duration = 1f)
    {
        ZoomTo(defaultSize, duration);
    }

    #endregion

    #region 震动效果

    /// <summary>
    /// 震动相机
    /// </summary>
    /// <param name="intensity">震动强度</param>
    /// <param name="duration">持续时间</param>
    public void Shake(float intensity = 1f, float duration = 0.5f)
    {
        if (!enableShake) return;
        
        StopShake();
        
        intensity = Mathf.Clamp(intensity, 0f, maxShakeIntensity);
        _isShaking = true;
        
        _shakeTween = _tweenManager?.DoShake(transform, intensity, 10, duration, "CameraShake");
        
        if (_shakeTween != null)
        {
            _shakeTween.OnUpdate(() => {
                _shakeOffset = transform.position - _currentPosition;
            });
            
            _shakeTween.OnComplete(() => {
                StopShake();
            });
        }
        
        OnShakeStarted?.Invoke();
        LogMessage($"相机震动: 强度={intensity}, 时间={duration}");
    }

    /// <summary>
    /// 停止震动
    /// </summary>
    public void StopShake()
    {
        if (_isShaking)
        {
            _shakeTween?.Kill();
            _isShaking = false;
            _shakeOffset = Vector3.zero;
            OnShakeEnded?.Invoke();
            LogMessage("停止相机震动");
        }
    }

    /// <summary>
    /// 根据距离震动
    /// </summary>
    /// <param name="explosionCenter">爆炸中心</param>
    /// <param name="maxDistance">最大影响距离</param>
    /// <param name="maxIntensity">最大震动强度</param>
    /// <param name="duration">持续时间</param>
    public void ShakeByDistance(Vector3 explosionCenter, float maxDistance, float maxIntensity = 1f, float duration = 0.5f)
    {
        float distance = Vector3.Distance(transform.position, explosionCenter);
        
        if (distance <= maxDistance)
        {
            float intensity = maxIntensity * (1f - distance / maxDistance);
            Shake(intensity, duration);
        }
    }

    #endregion

    #region 相机移动

    /// <summary>
    /// 平滑移动到目标位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="duration">持续时间</param>
    public async UniTask MoveToAsync(Vector3 targetPosition, float duration = 1f)
    {
        Vector3 startPosition = transform.position;
        targetPosition.z = startPosition.z;
        
        // 暂时停止跟随
        Transform originalTarget = target;
        target = null;
        
        await transform.DOMove(targetPosition, duration)
                      .SetEase(Ease.OutQuad)
                      .AsyncWaitForCompletion();
        
        // 恢复跟随
        target = originalTarget;
        _currentPosition = targetPosition;
        
        LogMessage($"相机移动到: {targetPosition}");
    }

    /// <summary>
    /// 立即移动到目标位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    public void MoveTo(Vector3 targetPosition)
    {
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
        _currentPosition = targetPosition;
        _targetPosition = targetPosition;
        LogMessage($"相机立即移动到: {targetPosition}");
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 世界坐标转屏幕坐标
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>屏幕坐标</returns>
    public Vector2 WorldToScreenPoint(Vector3 worldPosition)
    {
        return _camera.WorldToScreenPoint(worldPosition);
    }

    /// <summary>
    /// 屏幕坐标转世界坐标
    /// </summary>
    /// <param name="screenPosition">屏幕坐标</param>
    /// <returns>世界坐标</returns>
    public Vector3 ScreenToWorldPoint(Vector2 screenPosition)
    {
        return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _camera.nearClipPlane));
    }

    /// <summary>
    /// 检查世界坐标是否在相机视野内
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>是否在视野内</returns>
    public bool IsInView(Vector3 worldPosition)
    {
        Vector3 viewportPoint = _camera.WorldToViewportPoint(worldPosition);
        return viewportPoint.x >= 0 && viewportPoint.x <= 1 && 
               viewportPoint.y >= 0 && viewportPoint.y <= 1 && 
               viewportPoint.z > 0;
    }

    #endregion

    #region 调试

    /// <summary>
    /// 绘制调试图形
    /// </summary>
    private void DrawDebugGizmos()
    {
        // 绘制边界
        if (useBounds)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
        }
        
        // 绘制死区
        if (useDeadZone && target != null)
        {
            Gizmos.color = Color.green;
            Vector3 deadZoneCenter = target.position + (Vector3)offset;
            Gizmos.DrawWireCube(deadZoneCenter, new Vector3(deadZoneSize.x, deadZoneSize.y, 0));
        }
        
        // 绘制相机视野
        Gizmos.color = Color.white;
        float cameraHeight = _camera.orthographicSize * 2;
        float cameraWidth = cameraHeight * _camera.aspect;
        Gizmos.DrawWireCube(transform.position, new Vector3(cameraWidth, cameraHeight, 0));
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[Camera2DController] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Camera2DController] {message}");
    }

    #endregion
}

/// <summary>
/// 相机跟随模式
/// </summary>
public enum CameraFollowMode
{
    Instant,    // 立即跟随
    Smooth,     // 平滑跟随
    Lerp,       // 线性插值跟随
    DeadZone    // 死区跟随
}