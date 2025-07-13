using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件分发中心（单例）
/// 负责游戏中所有系统间的消息传递，实现模块解耦。
/// </summary>
public class EventDispatcher
{
    // 单例模式实现
    private static EventDispatcher _instance;
    public static EventDispatcher Instance => _instance ?? (_instance = new EventDispatcher());

    // 存储所有事件监听者的字典
    // Key: 事件名称 (字符串)
    // Value: 委托，包含所有监听该事件的方法
    private readonly Dictionary<string, Action<object>> _eventListeners = new Dictionary<string, Action<object>>();

    /// <summary>
    /// 注册（订阅）一个事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="listener">监听回调函数</param>
    public void Register(string eventName, Action<object> listener)
    {
        if (_eventListeners.TryGetValue(eventName, out var thisEvent))
        {
            // 如果事件已存在，添加新的监听者
            _eventListeners[eventName] = thisEvent + listener;
        }
        else
        {
            // 如果事件不存在，创建新的事件并添加监听者
            _eventListeners[eventName] = listener;
        }
    }

    /// <summary>
    /// 注销（取消订阅）一个事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="listener">监听回调函数</param>
    public void Unregister(string eventName, Action<object> listener)
    {
        if (_eventListeners.TryGetValue(eventName, out var thisEvent))
        {
            // 从事件中移除监听者
            thisEvent -= listener;
            
            // 如果移除后没有监听者了，清理字典项以节省内存
            if (thisEvent == null)
            {
                _eventListeners.Remove(eventName);
            }
            else
            {
                _eventListeners[eventName] = thisEvent;
            }
        }
    }

    /// <summary>
    /// 派发一个事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="payload">附带的参数（可选）</param>
    public void Dispatch(string eventName, object payload = null)
    {
        if (_eventListeners.TryGetValue(eventName, out var thisEvent))
        {
            // 调用所有监听该事件的方法
            thisEvent?.Invoke(payload);
        }
    }
}