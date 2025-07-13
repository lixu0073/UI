using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 场景数据配置类 (ScriptableObject)
/// 存储场景相关的配置信息，支持编辑器下的可视化配置
/// </summary>
[CreateAssetMenu(fileName = "NewSceneData", menuName = "Scene Manager/Scene Data")]
public class SceneDataSO : ScriptableObject
{
    [Header("场景配置")]
    [SerializeField] private string sceneName;
    [SerializeField] private string sceneDescription = "";
    [SerializeField] private bool isMainScene = false;
    
    /// <summary>
    /// 场景名称（只读）
    /// </summary>
    public string SceneName => sceneName;
    
    /// <summary>
    /// 场景描述
    /// </summary>
    public string SceneDescription => sceneDescription;
    
    /// <summary>
    /// 是否为主场景
    /// </summary>
    public bool IsMainScene => isMainScene;

#if UNITY_EDITOR
    [Header("编辑器配置")]
    [SerializeField] private SceneAsset sceneAsset;

    /// <summary>
    /// 编辑器下验证数据有效性
    /// </summary>
    private void OnValidate()
    {
        if (sceneAsset != null)
        {
            sceneName = sceneAsset.name;
        }
    }
#endif
}
