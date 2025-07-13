using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 所有UI视图（面板）的抽象基类
/// 定义了UI的生命周期，提供事件注册的便捷管理和动画支持
/// </summary>
public abstract class UIBaseView : MonoBehaviour
{
    [Header("UI配置")]
    [SerializeField] protected bool enableAnimation = true;
    [SerializeField] protected float animationDuration = 0.3f;

    // 用于存储此视图注册的所有事件，方便在销毁时自动注销
    private readonly List<KeyValuePair<string, Action<object>>> _registeredEvents = 
        new List<KeyValuePair<string, Action<object>>>();

    // UI状态管理
    private bool _isVisible = false;
    private bool _isInitialized = false;

    /// <summary>
    /// UI是否可见
    /// </summary>
    public bool IsVisible => _isVisible;

    /// <summary>
    /// UI是否已初始化
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// UI层级深度
    /// </summary>
    public virtual int SortOrder => 0;

    #region 生命周期方法

    /// <summary>
    /// 初始化UI（只在第一次显示时调用）
    /// </summary>
    protected virtual void OnInitialize()
    {
        // 子类重写此方法进行初始化
    }

    /// <summary>
    /// 当视图被显示时调用（由UIManager调用）
    /// </summary>
    /// <param name="args">打开时传递的参数</param>
    public virtual void OnShow(object args = null)
    {
        if (!_isInitialized)
        {
            OnInitialize();
            _isInitialized = true;
        }

        _isVisible = true;
        gameObject.SetActive(true);

        // 触发显示动画
        if (enableAnimation)
        {
            OnShowAnimation();
        }

        OnShowCompleted(args);
    }

    /// <summary>
    /// 显示完成后调用
    /// </summary>
    /// <param name="args">显示参数</param>
    protected virtual void OnShowCompleted(object args)
    {
        // 子类可重写此方法处理显示后的逻辑
    }

    /// <summary>
    /// 当视图被隐藏时调用（由UIManager调用）
    /// </summary>
    public virtual void OnHide()
    {
        _isVisible = false;

        // 触发隐藏动画
        if (enableAnimation)
        {
            OnHideAnimation(() => 
            {
                gameObject.SetActive(false);
                OnHideCompleted();
            });
        }
        else
        {
            gameObject.SetActive(false);
            OnHideCompleted();
        }
    }

    /// <summary>
    /// 隐藏完成后调用
    /// </summary>
    protected virtual void OnHideCompleted()
    {
        // 子类可重写此方法处理隐藏后的逻辑
    }

    /// <summary>
    /// 当视图被销毁前调用（由UIManager调用）
    /// </summary>
    public virtual void OnDestroyView()
    {
        // 自动注销所有通过本基类注册的事件
        UnregisterAllEvents();
        
        OnCleanup();
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    protected virtual void OnCleanup()
    {
        // 子类可重写此方法进行资源清理
    }

    #endregion

    #region 动画方法

    /// <summary>
    /// 显示动画（子类可重写）
    /// </summary>
    protected virtual void OnShowAnimation()
    {
        // 默认简单的缩放动画
        transform.localScale = Vector3.zero;
        LeanTween.scale(gameObject, Vector3.one, animationDuration)
                 .setEase(LeanTweenType.easeOutBack);
    }

    /// <summary>
    /// 隐藏动画（子类可重写）
    /// </summary>
    /// <param name="onComplete">动画完成回调</param>
    protected virtual void OnHideAnimation(Action onComplete)
    {
        // 默认简单的缩放动画
        LeanTween.scale(gameObject, Vector3.zero, animationDuration)
                 .setEase(LeanTweenType.easeInBack)
                 .setOnComplete(onComplete);
    }

    #endregion

    #region 事件管理

    /// <summary>
    /// 便捷的事件注册方法
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="listener">事件监听器</param>
    protected void RegisterEvent(string eventName, Action<object> listener)
    {
        if (string.IsNullOrEmpty(eventName) || listener == null)
        {
            Debug.LogWarning("事件名称或监听器为空，跳过注册");
            return;
        }

        EventDispatcher.Instance.Register(eventName, listener);
        _registeredEvents.Add(new KeyValuePair<string, Action<object>>(eventName, listener));
    }

    /// <summary>
    /// 便捷的事件注销方法
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="listener">事件监听器</param>
    protected void UnregisterEvent(string eventName, Action<object> listener)
    {
        if (string.IsNullOrEmpty(eventName) || listener == null)
        {
            return;
        }

        EventDispatcher.Instance.Unregister(eventName, listener);
        
        // 从已注册列表中移除
        for (int i = _registeredEvents.Count - 1; i >= 0; i--)
        {
            var evt = _registeredEvents[i];
            if (evt.Key == eventName && evt.Value == listener)
            {
                _registeredEvents.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// 注销所有事件
    /// </summary>
    private void UnregisterAllEvents()
    {
        foreach (var evt in _registeredEvents)
        {
            EventDispatcher.Instance.Unregister(evt.Key, evt.Value);
        }
        _registeredEvents.Clear();
    }

    /// <summary>
    /// 派发事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="payload">事件数据</param>
    protected void DispatchEvent(string eventName, object payload = null)
    {
        EventDispatcher.Instance.Dispatch(eventName, payload);
    }

    #endregion

    #region Unity生命周期

    protected virtual void OnDestroy()
    {
        // 确保在Unity销毁时也清理事件
        UnregisterAllEvents();
        
        // 取消所有LeanTween动画
        LeanTween.cancel(gameObject);
    }

    #endregion
}