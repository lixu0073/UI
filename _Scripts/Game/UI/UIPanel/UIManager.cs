using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// UI管理器（单例）
/// 负责所有UI视图的加载、实例化、显示、隐藏和销毁
/// 支持UI层级管理、预加载和资源缓存
/// </summary>
public class UIManager : MonoBehaviour
{
    // 单例模式实现
    public static UIManager Instance { get; private set; }

    [Header("UI设置")]
    // 所有UI的根节点
    [SerializeField] private Transform uiRoot;
    // UI预制件在Resources下的路径
    [SerializeField] private string uiPrefabPath = "UIPrefabs/";
    // 是否启用UI缓存
    [SerializeField] private bool enableViewCache = true;
    // 最大缓存UI数量
    [SerializeField] private int maxCacheCount = 10;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;

    // 缓存加载过的UI预制件
    private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    // 缓存实例化的UI视图
    private readonly Dictionary<string, UIBaseView> _viewCache = new Dictionary<string, UIBaseView>();
    // 当前显示的UI栈（用于管理层级）
    private readonly List<UIBaseView> _visibleViews = new List<UIBaseView>();
    // UI显示历史（用于返回功能）
    private readonly Stack<string> _viewHistory = new Stack<string>();

    /// <summary>
    /// 单例初始化
    /// </summary>
    void Awake()
    {
        InitializeSingleton();
        InitializeUIRoot();
    }
    
    /// <summary>
    /// 初始化单例
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 初始化UI根节点
    /// </summary>
    private void InitializeUIRoot()
    {
        if (uiRoot == null)
        {
            // 如果没有设置UI根节点，创建一个
            GameObject rootObject = new GameObject("UIRoot");
            rootObject.transform.SetParent(transform);
            uiRoot = rootObject.transform;
            
            // 添加Canvas组件
            Canvas canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            // 添加CanvasScaler
            var scaler = rootObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            if (enableDebugLog)
                Debug.Log("已自动创建UI根节点");
        }
    }

    /// <summary>
    /// 显示一个UI视图
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    /// <param name="args">传递给OnShow的参数</param>
    /// <param name="addToHistory">是否添加到历史记录</param>
    /// <returns>UI视图实例</returns>
    public T ShowView<T>(object args = null, bool addToHistory = true) where T : UIBaseView
    {
        string viewName = typeof(T).Name;
        
        if (enableDebugLog)
            Debug.Log($"显示UI视图: {viewName}");
            
        // 检查视图是否已在缓存中
        if (_viewCache.TryGetValue(viewName, out var view))
        {
            var typedView = view as T;
            if (typedView != null && !typedView.IsVisible)
            {
                ShowExistingView(typedView, args, addToHistory);
                return typedView;
            }
        }

        // 如果视图不在缓存中，加载并实例化
        return CreateAndShowView<T>(viewName, args, addToHistory);
    }
    
    /// <summary>
    /// 显示已存在的视图
    /// </summary>
    private void ShowExistingView(UIBaseView view, object args, bool addToHistory)
    {
        view.OnShow(args);
        AddToVisibleViews(view);
        UpdateViewSorting();
        
        if (addToHistory)
        {
            _viewHistory.Push(view.GetType().Name);
        }
    }
    
    /// <summary>
    /// 创建并显示新视图
    /// </summary>
    private T CreateAndShowView<T>(string viewName, object args, bool addToHistory) where T : UIBaseView
    {
        GameObject prefab = GetPrefab(viewName);
        if (prefab == null)
        {
            Debug.LogError($"无法找到UI预制件: {viewName}");
            return null;
        }

        GameObject viewObject = Instantiate(prefab, uiRoot);
        viewObject.name = viewName;
        
        T viewComponent = viewObject.GetComponent<T>();
        if (viewComponent == null)
        {
            Debug.LogError($"预制件 {viewName} 缺少 {typeof(T).Name} 组件");
            Destroy(viewObject);
            return null;
        }

        // 缓存视图（如果启用缓存）
        if (enableViewCache)
        {
            CacheView(viewName, viewComponent);
        }

        viewComponent.OnShow(args);
        AddToVisibleViews(viewComponent);
        UpdateViewSorting();
        
        if (addToHistory)
        {
            _viewHistory.Push(viewName);
        }

        return viewComponent;
    }

    /// <summary>
    /// 隐藏一个UI视图
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    /// <param name="destroyView">是否销毁视图</param>
    public void HideView<T>(bool destroyView = false) where T : UIBaseView
    {
        string viewName = typeof(T).Name;
        HideView(viewName, destroyView);
    }
    
    /// <summary>
    /// 隐藏指定名称的UI视图
    /// </summary>
    /// <param name="viewName">视图名称</param>
    /// <param name="destroyView">是否销毁视图</param>
    public void HideView(string viewName, bool destroyView = false)
    {
        if (_viewCache.TryGetValue(viewName, out var view))
        {
            if (enableDebugLog)
                Debug.Log($"隐藏UI视图: {viewName}");
                
            view.OnHide();
            RemoveFromVisibleViews(view);
            
            if (destroyView)
            {
                DestroyView(viewName);
            }
        }
    }

    /// <summary>
    /// 获取指定的UI视图实例
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    /// <returns>UI视图实例，如果不存在返回null</returns>
    public T GetView<T>() where T : UIBaseView
    {
        string viewName = typeof(T).Name;
        if (_viewCache.TryGetValue(viewName, out var view))
        {
            return view as T;
        }
        return null;
    }
    
    /// <summary>
    /// 检查UI视图是否存在
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    /// <returns>是否存在</returns>
    public bool HasView<T>() where T : UIBaseView
    {
        string viewName = typeof(T).Name;
        return _viewCache.ContainsKey(viewName);
    }
    
    /// <summary>
    /// 检查UI视图是否可见
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    /// <returns>是否可见</returns>
    public bool IsViewVisible<T>() where T : UIBaseView
    {
        var view = GetView<T>();
        return view != null && view.IsVisible;
    }

    /// <summary>
    /// 隐藏所有UI视图
    /// </summary>
    /// <param name="destroyViews">是否销毁所有视图</param>
    public void HideAllViews(bool destroyViews = false)
    {
        var viewsToHide = new List<UIBaseView>(_visibleViews);
        
        foreach (var view in viewsToHide)
        {
            view.OnHide();
        }
        
        _visibleViews.Clear();
        _viewHistory.Clear();
        
        if (destroyViews)
        {
            DestroyAllViews();
        }
    }
    
    /// <summary>
    /// 返回上一个UI视图
    /// </summary>
    /// <returns>是否成功返回</returns>
    public bool GoBack()
    {
        if (_viewHistory.Count > 1)
        {
            // 移除当前视图
            string currentView = _viewHistory.Pop();
            HideView(currentView);
            
            // 显示上一个视图
            if (_viewHistory.Count > 0)
            {
                string previousView = _viewHistory.Peek();
                if (_viewCache.TryGetValue(previousView, out var view))
                {
                    view.OnShow();
                    AddToVisibleViews(view);
                    UpdateViewSorting();
                    return true;
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// 预加载UI视图
    /// </summary>
    /// <typeparam name="T">UIBaseView的子类</typeparam>
    public void PreloadView<T>() where T : UIBaseView
    {
        string viewName = typeof(T).Name;
        if (!_viewCache.ContainsKey(viewName))
        {
            GameObject prefab = GetPrefab(viewName);
            if (prefab != null)
            {
                GameObject viewObject = Instantiate(prefab, uiRoot);
                viewObject.name = viewName;
                viewObject.SetActive(false);
                
                T viewComponent = viewObject.GetComponent<T>();
                if (viewComponent != null)
                {
                    CacheView(viewName, viewComponent);
                    
                    if (enableDebugLog)
                        Debug.Log($"预加载UI视图: {viewName}");
                }
            }
        }
    }

    #region 私有方法
    
    /// <summary>
    /// 获取UI预制件
    /// </summary>
    /// <param name="viewName">视图名称</param>
    /// <returns>预制件GameObject</returns>
    private GameObject GetPrefab(string viewName)
    {
        if (_prefabCache.TryGetValue(viewName, out var prefab))
        {
            return prefab;
        }

        // TODO：改为运行时AB包和Editor AssetDatabase加载
        string path = uiPrefabPath + viewName;
        GameObject loadedPrefab = Resources.Load<GameObject>(path);
        if (loadedPrefab != null)
        {
            _prefabCache[viewName] = loadedPrefab;
            
            if (enableDebugLog)
                Debug.Log($"加载UI预制件: {path}");
        }
        else
        {
            Debug.LogError($"无法加载UI预制件: {path}");
        }
        
        return loadedPrefab;
    }
    
    /// <summary>
    /// 缓存视图
    /// </summary>
    private void CacheView(string viewName, UIBaseView view)
    {
        // 检查缓存数量限制
        if (_viewCache.Count >= maxCacheCount && !_viewCache.ContainsKey(viewName))
        {
            // 移除最旧的缓存项（简单LRU策略）
            var oldestView = _viewCache.Values.FirstOrDefault(v => !v.IsVisible);
            if (oldestView != null)
            {
                string oldestName = oldestView.GetType().Name;
                DestroyView(oldestName);
                
                if (enableDebugLog)
                    Debug.Log($"移除旧缓存: {oldestName}");
            }
        }
        
        _viewCache[viewName] = view;
    }
    
    /// <summary>
    /// 添加到可见视图列表
    /// </summary>
    private void AddToVisibleViews(UIBaseView view)
    {
        if (!_visibleViews.Contains(view))
        {
            _visibleViews.Add(view);
        }
    }
    
    /// <summary>
    /// 从可见视图列表移除
    /// </summary>
    private void RemoveFromVisibleViews(UIBaseView view)
    {
        _visibleViews.Remove(view);
    }
    
    /// <summary>
    /// 更新视图层级排序
    /// </summary>
    private void UpdateViewSorting()
    {
        // 根据SortOrder和显示顺序设置sibling index
        var sortedViews = _visibleViews
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => _visibleViews.IndexOf(v))
            .ToList();
            
        for (int i = 0; i < sortedViews.Count; i++)
        {
            sortedViews[i].transform.SetSiblingIndex(i);
        }
    }
    
    /// <summary>
    /// 销毁指定视图
    /// </summary>
    private void DestroyView(string viewName)
    {
        if (_viewCache.TryGetValue(viewName, out var view))
        {
            view.OnDestroyView();
            if (view != null)
            {
                Destroy(view.gameObject);
            }
            _viewCache.Remove(viewName);
            RemoveFromVisibleViews(view);
        }
    }
    
    /// <summary>
    /// 销毁所有视图
    /// </summary>
    private void DestroyAllViews()
    {
        foreach (var kvp in _viewCache.ToList())
        {
            DestroyView(kvp.Key);
        }
    }
    
    #endregion

    /// <summary>
    /// 清理资源
    /// </summary>
    void OnDestroy()
    {
        // 清理所有视图
        DestroyAllViews();
        _prefabCache.Clear();
        _viewHistory.Clear();
        
        if (enableDebugLog)
            Debug.Log("UIManager已清理所有资源");
    }
}