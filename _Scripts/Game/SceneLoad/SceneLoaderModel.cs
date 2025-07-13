using Cysharp.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载模型类
/// 负责实际的场景加载和卸载逻辑，提供异步加载支持
/// </summary>
public class SceneLoaderModel
{
    /// <summary>
    /// 异步加载场景
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="mode">加载模式</param>
    /// <param name="progress">进度回调</param>
    /// <returns>加载任务</returns>
    public async UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode, IProgress<float> progress = null)
    {
        // 验证场景名称
        if (string.IsNullOrEmpty(sceneName))
        {
            throw new ArgumentException("场景名称不能为空", nameof(sceneName));
        }

        var asyncOp = SceneManager.LoadSceneAsync(sceneName, mode);
        if (asyncOp == null)
        {
            throw new InvalidOperationException($"无法加载场景: {sceneName}");
        }

        // 防止场景自动激活（可选）
        // asyncOp.allowSceneActivation = false;
        
        await asyncOp.ToUniTask(progress: progress);
    }

    /// <summary>
    /// 异步卸载场景
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns>卸载任务</returns>
    public async UniTask UnloadSceneAsync(string sceneName)
    {
        // 验证场景名称
        if (string.IsNullOrEmpty(sceneName))
        {
            throw new ArgumentException("场景名称不能为空", nameof(sceneName));
        }

        // 检查场景是否已加载
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded)
        {
            return; // 场景未加载，直接返回
        }

        await SceneManager.UnloadSceneAsync(sceneName).ToUniTask();
    }
    
    /// <summary>
    /// 检查场景是否已加载
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns>是否已加载</returns>
    public bool IsSceneLoaded(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        return scene.isLoaded;
    }
}
