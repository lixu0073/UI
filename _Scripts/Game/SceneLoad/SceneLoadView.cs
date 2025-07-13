using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;

/// <summary>
/// 场景加载视图实现类
/// 负责显示加载进度和动画效果
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SceneLoadView : MonoBehaviour, ISceneLoadView
{
    [Header("UI组件")]
    [SerializeField] private Slider progressBar;
    
    [Header("动画配置")]
    [SerializeField] private float fadeTime = 0.5f;

    private CanvasGroup _canvasGroup;

    /// <summary>
    /// 隐藏动画完成时触发的事件
    /// </summary>
    public event Action OnHideAnimationFinished;

    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        
        // 初始状态：完全透明且不可交互
        _canvasGroup.alpha = 0;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 设置加载进度
    /// </summary>
    /// <param name="progress">进度值 (0-1)</param>
    public void SetProgress(float progress)
    {
        if (progressBar == null)
        {
            Debug.LogWarning("进度条组件未设置！");
            return;
        }

        // 确保进度值在有效范围内
        progress = Mathf.Clamp01(progress);
        progressBar.value = progress;
    }

    /// <summary>
    /// 显示加载界面
    /// </summary>
    /// <returns>显示动画的异步任务</returns>
    public async UniTask Show()
    {
        gameObject.SetActive(true);
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        
        // 重置进度条
        SetProgress(0f);
        
        await FadeToAlpha(1f, fadeTime);
    }

    /// <summary>
    /// 隐藏加载界面
    /// </summary>
    /// <returns>隐藏动画的异步任务</returns>
    public async UniTask Hide()
    {
        await FadeToAlpha(0f, fadeTime);
        
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
        
        // 触发隐藏完成事件
        OnHideAnimationFinished?.Invoke();
    }

    /// <summary>
    /// 淡入淡出动画
    /// </summary>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">动画持续时间</param>
    /// <returns>动画异步任务</returns>
    private async UniTask FadeToAlpha(float targetAlpha, float duration)
    {
        float startAlpha = _canvasGroup.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            
            // 使用平滑插值
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            
            await UniTask.Yield();
        }
        
        // 确保最终值准确
        _canvasGroup.alpha = targetAlpha;
    }
}