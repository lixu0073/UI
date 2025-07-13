using Cysharp.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载控制器
/// 采用MVP模式，协调Model和View的交互
/// </summary>
public class SceneLoadPresenter
{
    private readonly SceneLoaderModel _model;
    private readonly ISceneLoadView _view;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="model">场景加载模型</param>
    /// <param name="view">场景加载视图</param>
    public SceneLoadPresenter(SceneLoaderModel model, ISceneLoadView view)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    /// <summary>
    /// 加载场景
    /// </summary>
    /// <param name="sceneToLoad">要加载的场景数据</param>
    /// <param name="mode">加载模式</param>
    /// <returns>加载任务</returns>
    public async UniTask LoadScene(SceneDataSO sceneToLoad, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (sceneToLoad == null)
            throw new ArgumentNullException(nameof(sceneToLoad));

        try
        {
            // 显示加载界面
            await _view.Show();

            // 创建进度回调
            var progress = Progress.Create<float>(p => _view.SetProgress(p));

            // 执行场景加载
            await _model.LoadSceneAsync(sceneToLoad.SceneName, mode, progress);

            // 确保加载条显示100%
            _view.SetProgress(1f);

            // 等待一小段时间让用户看到100%
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

            // 隐藏加载界面
            await _view.Hide();
        }
        catch (Exception ex)
        {
            // 发生错误时也要隐藏加载界面
            await _view.Hide();
            throw; // 重新抛出异常
        }
    }

    /// <summary>
    /// 加载附加场景（不显示加载界面）
    /// </summary>
    /// <param name="sceneToLoad">要加载的场景数据</param>
    /// <returns>加载任务</returns>
    public async UniTask LoadAdditiveScene(SceneDataSO sceneToLoad)
    {
        if (sceneToLoad == null)
            throw new ArgumentNullException(nameof(sceneToLoad));

        // 检查场景是否已经加载
        if (_model.IsSceneLoaded(sceneToLoad.SceneName))
        {
            return; // 场景已加载，直接返回
        }

        // Additive 加载通常不需要显示全局加载条
        await _model.LoadSceneAsync(sceneToLoad.SceneName, LoadSceneMode.Additive);
    }

    /// <summary>
    /// 卸载附加场景
    /// </summary>
    /// <param name="sceneToUnload">要卸载的场景数据</param>
    /// <returns>卸载任务</returns>
    public async UniTask UnloadAdditiveScene(SceneDataSO sceneToUnload)
    {
        if (sceneToUnload == null)
            throw new ArgumentNullException(nameof(sceneToUnload));

        await _model.UnloadSceneAsync(sceneToUnload.SceneName);
    }
}