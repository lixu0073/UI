using Cysharp.Threading.Tasks;
using System;

/// <summary>
/// 场景加载视图接口
/// 定义场景加载时UI显示的标准行为
/// </summary>
public interface ISceneLoadView
{
    /// <summary>
    /// 设置加载进度
    /// </summary>
    /// <param name="progress">加载进度 (0-1)</param>
    void SetProgress(float progress);
    
    /// <summary>
    /// 显示加载界面
    /// </summary>
    /// <returns>显示动画的异步任务</returns>
    UniTask Show();
    
    /// <summary>
    /// 隐藏加载界面
    /// </summary>
    /// <returns>隐藏动画的异步任务</returns>
    UniTask Hide();
    
    /// <summary>
    /// 隐藏动画完成时触发的事件
    /// </summary>
    event Action OnHideAnimationFinished;
}