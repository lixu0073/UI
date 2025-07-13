using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Threading;

/// <summary>
/// 安全区域适配组件
/// 自动适配手机刘海屏、底部手势条等屏幕安全区域
/// 支持选择性调整上下左右边界
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    [Header("安全区域调整选项")]
    [SerializeField] private bool isUpAdjust = true;
    [SerializeField] private bool isDownAdjust = true;
    [SerializeField] private bool isLeftAdjust = true;
    [SerializeField] private bool isRightAdjust = true;

    [Header("边界偏移设置")]
    [SerializeField][Range(0, 1.0f)] private float topBlank = 0f;
    [SerializeField][Range(0, 1.0f)] private float bottomOffset = 0f;
    [SerializeField][Range(0, 1.0f)] private float leftOffset = 0f;
    [SerializeField][Range(0, 1.0f)] private float rightOffset = 0f;

    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;

    private RectTransform _rectTransform;
    private CancellationTokenSource _cts;
    private Rect _lastSafeArea;

    private void OnEnable()
    {
        InitializeComponent();
        StartSafeAreaObservation();
    }

    private void OnDisable()
    {
        StopSafeAreaObservation();
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponent()
    {
        _rectTransform = GetComponent<RectTransform>();
        _cts = new CancellationTokenSource();
        _lastSafeArea = new Rect();
    }

    /// <summary>
    /// 开始监听安全区域变化
    /// </summary>
    private void StartSafeAreaObservation()
    {
        // 首次加载时立即应用安全区域
        UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, _cts.Token)
               .ContinueWith(ApplySafeAreaImmediate)
               .Forget();

        // 持续监听安全区域变化
        ObserveSafeAreaChanges().Forget();
    }

    /// <summary>
    /// 停止监听安全区域变化
    /// </summary>
    private void StopSafeAreaObservation()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 异步监听安全区域变化
    /// </summary>
    private async UniTaskVoid ObserveSafeAreaChanges()
    {
        try
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, _cts.Token);

            await ScreenObserver
                .ObserveScreenChanges(_cts.Token)
                .ForEachAsync(screenData => 
                {
                    // 只在安全区域真正变化时才应用
                    if (screenData.safeArea != _lastSafeArea)
                    {
                        ApplySafeAreaImmediate();
                        _lastSafeArea = screenData.safeArea;
                    }
                }, _cts.Token);
        }
        catch (System.OperationCanceledException)
        {
            // 正常的取消操作，不需要处理
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"SafeArea观察器发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 立即应用安全区域设置
    /// </summary>
    private void ApplySafeAreaImmediate()
    {
        if (_rectTransform == null) 
        {
            Debug.LogWarning("RectTransform组件未找到！");
            return;
        }

        if (Screen.width <= 0 || Screen.height <= 0) 
        {
            if (enableDebugLog)
                Debug.LogWarning("屏幕尺寸无效，跳过安全区域应用");
            return;
        }

        ApplySafeArea(Screen.safeArea);
    }

    /// <summary>
    /// 应用安全区域设置到RectTransform
    /// </summary>
    /// <param name="safeArea">屏幕安全区域</param>
    private void ApplySafeArea(Rect safeArea)
    {
        // 计算安全区域的锚点坐标（0-1范围）
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        // 根据设置调整各边界
        if (isUpAdjust)
        {
            anchorMax.y = Mathf.Lerp(safeArea.yMax / Screen.height, 1f, topBlank);
        }
        else
        {
            anchorMax.y = safeArea.yMax / Screen.height;
        }

        if (isDownAdjust)
        {
            anchorMin.y = Mathf.Lerp(safeArea.yMin / Screen.height, 0f, bottomOffset);
        }
        else
        {
            anchorMin.y = safeArea.yMin / Screen.height;
        }

        if (isLeftAdjust)
        {
            anchorMin.x = Mathf.Lerp(safeArea.xMin / Screen.width, 0f, leftOffset);
        }
        else
        {
            anchorMin.x = safeArea.xMin / Screen.width;
        }

        if (isRightAdjust)
        {
            anchorMax.x = Mathf.Lerp(safeArea.xMax / Screen.width, 1f, rightOffset);
        }
        else
        {
            anchorMax.x = safeArea.xMax / Screen.width;
        }

        // 应用锚点设置
        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;

        // 重置偏移，让UI完全基于锚点
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;

        if (enableDebugLog)
        {
            Debug.Log($"SafeArea应用完成: anchorMin={anchorMin}, anchorMax={anchorMax}");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器下实时预览安全区域效果
    /// </summary>
    private void Update()
    {
        ApplySafeAreaImmediate();
    }
#endif
}
