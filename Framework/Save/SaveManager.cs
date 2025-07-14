using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 存档管理器
/// 负责游戏数据的保存和加载，支持多个存档槽位和数据加密
/// 提供自动保存、存档验证和数据备份功能
/// </summary>
public class SaveManager : MonoBehaviour
{
    [Header("存档设置")]
    [SerializeField] private int maxSaveSlots = 3;
    [SerializeField] private bool enableEncryption = true;
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5分钟
    
    [Header("存档路径")]
    [SerializeField] private string saveFileName = "GameSave";
    [SerializeField] private string saveFileExtension = ".save";
    [SerializeField] private string backupSuffix = "_backup";
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool createBackups = true;

    // 单例实例
    private static SaveManager _instance;
    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SaveManager");
                _instance = go.AddComponent<SaveManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 存档数据
    private Dictionary<int, GameSaveData> _saveSlots = new Dictionary<int, GameSaveData>();
    private GameSaveData _currentSaveData;
    private int _currentSlot = -1;
    
    // 自动保存
    private float _autoSaveTimer;
    private bool _hasUnsavedChanges = false;
    
    // 加密密钥
    private readonly string _encryptionKey = "Unity2DFramework2024";
    
    // 存档路径
    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    // 事件
    public System.Action<int> OnSaveCompleted;
    public System.Action<int> OnLoadCompleted;
    public System.Action<string> OnSaveError;
    public System.Action OnAutoSave;

    // 属性
    public GameSaveData CurrentSaveData => _currentSaveData;
    public int CurrentSlot => _currentSlot;
    public bool HasUnsavedChanges => _hasUnsavedChanges;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化存档管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("存档管理器已经初始化");
            return;
        }

        LogMessage("初始化存档管理器...");

        // 创建存档目录
        CreateSaveDirectory();
        
        // 扫描现有存档
        await ScanExistingSaves();
        
        // 初始化当前存档数据
        _currentSaveData = new GameSaveData();
        
        IsInitialized = true;
        LogMessage("存档管理器初始化完成");
    }

    /// <summary>
    /// 创建存档目录
    /// </summary>
    private void CreateSaveDirectory()
    {
        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
            LogMessage($"创建存档目录: {SaveDirectory}");
        }
    }

    /// <summary>
    /// 扫描现有存档
    /// </summary>
    private async UniTask ScanExistingSaves()
    {
        for (int slot = 0; slot < maxSaveSlots; slot++)
        {
            string savePath = GetSaveFilePath(slot);
            if (File.Exists(savePath))
            {
                try
                {
                    GameSaveData saveData = await LoadSaveDataAsync(slot);
                    if (saveData != null)
                    {
                        _saveSlots[slot] = saveData;
                        LogMessage($"发现存档槽位 {slot}: {saveData.playerName}");
                    }
                }
                catch (Exception e)
                {
                    LogError($"加载存档槽位 {slot} 失败: {e.Message}");
                }
            }
        }
        
        LogMessage($"扫描完成，发现 {_saveSlots.Count} 个存档");
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        if (!IsInitialized) return;

        UpdateAutoSave();
    }

    /// <summary>
    /// 更新自动保存
    /// </summary>
    private void UpdateAutoSave()
    {
        if (!enableAutoSave || !_hasUnsavedChanges || _currentSlot < 0) return;

        _autoSaveTimer += Time.deltaTime;
        if (_autoSaveTimer >= autoSaveInterval)
        {
            AutoSave();
            _autoSaveTimer = 0f;
        }
    }

    /// <summary>
    /// 应用程序暂停时保存
    /// </summary>
    /// <param name="pauseStatus">暂停状态</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _hasUnsavedChanges && _currentSlot >= 0)
        {
            QuickSave();
        }
    }

    /// <summary>
    /// 应用程序退出时保存
    /// </summary>
    private void OnApplicationQuit()
    {
        if (_hasUnsavedChanges && _currentSlot >= 0)
        {
            QuickSave();
        }
    }

    #endregion

    #region 存档操作

    /// <summary>
    /// 保存游戏到指定槽位
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <param name="saveData">存档数据</param>
    /// <returns>保存任务</returns>
    public async UniTask<bool> SaveGameAsync(int slot, GameSaveData saveData = null)
    {
        if (slot < 0 || slot >= maxSaveSlots)
        {
            LogError($"无效的存档槽位: {slot}");
            return false;
        }

        if (saveData == null)
        {
            saveData = _currentSaveData;
        }

        try
        {
            // 更新保存时间
            saveData.saveTime = DateTime.Now;
            saveData.version = Application.version;

            // 序列化数据
            string jsonData = JsonUtility.ToJson(saveData, true);
            
            // 加密数据
            if (enableEncryption)
            {
                jsonData = EncryptData(jsonData);
            }

            // 获取保存路径
            string savePath = GetSaveFilePath(slot);
            
            // 创建备份
            if (createBackups && File.Exists(savePath))
            {
                CreateBackup(slot);
            }

            // 写入文件
            await File.WriteAllTextAsync(savePath, jsonData);
            
            // 更新缓存
            _saveSlots[slot] = saveData;
            _currentSlot = slot;
            _hasUnsavedChanges = false;

            OnSaveCompleted?.Invoke(slot);
            LogMessage($"游戏保存成功到槽位 {slot}");
            return true;
        }
        catch (Exception e)
        {
            string errorMsg = $"保存游戏失败: {e.Message}";
            LogError(errorMsg);
            OnSaveError?.Invoke(errorMsg);
            return false;
        }
    }

    /// <summary>
    /// 从指定槽位加载游戏
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>加载任务</returns>
    public async UniTask<GameSaveData> LoadGameAsync(int slot)
    {
        if (slot < 0 || slot >= maxSaveSlots)
        {
            LogError($"无效的存档槽位: {slot}");
            return null;
        }

        try
        {
            GameSaveData saveData = await LoadSaveDataAsync(slot);
            if (saveData != null)
            {
                _currentSaveData = saveData;
                _currentSlot = slot;
                _hasUnsavedChanges = false;
                
                OnLoadCompleted?.Invoke(slot);
                LogMessage($"游戏加载成功从槽位 {slot}");
            }
            
            return saveData;
        }
        catch (Exception e)
        {
            string errorMsg = $"加载游戏失败: {e.Message}";
            LogError(errorMsg);
            OnSaveError?.Invoke(errorMsg);
            return null;
        }
    }

    /// <summary>
    /// 加载存档数据
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>存档数据</returns>
    private async UniTask<GameSaveData> LoadSaveDataAsync(int slot)
    {
        string savePath = GetSaveFilePath(slot);
        
        if (!File.Exists(savePath))
        {
            LogMessage($"存档文件不存在: {savePath}");
            return null;
        }

        string jsonData = await File.ReadAllTextAsync(savePath);
        
        // 解密数据
        if (enableEncryption)
        {
            jsonData = DecryptData(jsonData);
        }

        // 反序列化
        GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(jsonData);
        
        // 验证存档
        if (!ValidateSaveData(saveData))
        {
            LogError($"存档数据验证失败: 槽位 {slot}");
            return null;
        }

        return saveData;
    }

    #endregion

    #region 存档槽位管理

    /// <summary>
    /// 删除存档
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>是否成功删除</returns>
    public bool DeleteSave(int slot)
    {
        if (slot < 0 || slot >= maxSaveSlots)
        {
            LogError($"无效的存档槽位: {slot}");
            return false;
        }

        try
        {
            string savePath = GetSaveFilePath(slot);
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            // 删除备份
            string backupPath = GetBackupFilePath(slot);
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            // 从缓存中移除
            _saveSlots.Remove(slot);
            
            if (_currentSlot == slot)
            {
                _currentSlot = -1;
                _currentSaveData = new GameSaveData();
            }

            LogMessage($"删除存档槽位 {slot} 成功");
            return true;
        }
        catch (Exception e)
        {
            LogError($"删除存档失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查存档槽位是否存在
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>是否存在</returns>
    public bool HasSave(int slot)
    {
        return _saveSlots.ContainsKey(slot);
    }

    /// <summary>
    /// 获取存档信息
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>存档信息</returns>
    public SaveInfo GetSaveInfo(int slot)
    {
        if (!_saveSlots.ContainsKey(slot))
        {
            return null;
        }

        GameSaveData saveData = _saveSlots[slot];
        return new SaveInfo
        {
            slot = slot,
            playerName = saveData.playerName,
            level = saveData.playerLevel,
            playTime = saveData.playTime,
            saveTime = saveData.saveTime,
            version = saveData.version
        };
    }

    /// <summary>
    /// 获取所有存档信息
    /// </summary>
    /// <returns>存档信息列表</returns>
    public List<SaveInfo> GetAllSaveInfos()
    {
        List<SaveInfo> infos = new List<SaveInfo>();
        
        for (int slot = 0; slot < maxSaveSlots; slot++)
        {
            SaveInfo info = GetSaveInfo(slot);
            if (info != null)
            {
                infos.Add(info);
            }
        }
        
        return infos;
    }

    #endregion

    #region 快速操作

    /// <summary>
    /// 快速保存
    /// </summary>
    public async UniTask QuickSave()
    {
        if (_currentSlot >= 0)
        {
            await SaveGameAsync(_currentSlot);
        }
        else
        {
            LogMessage("没有当前存档槽位，无法快速保存");
        }
    }

    /// <summary>
    /// 自动保存
    /// </summary>
    private async void AutoSave()
    {
        if (_currentSlot >= 0)
        {
            await SaveGameAsync(_currentSlot);
            OnAutoSave?.Invoke();
            LogMessage("自动保存完成");
        }
    }

    /// <summary>
    /// 标记数据已修改
    /// </summary>
    public void MarkDataDirty()
    {
        _hasUnsavedChanges = true;
        _autoSaveTimer = 0f; // 重置自动保存计时器
    }

    #endregion

    #region 数据操作

    /// <summary>
    /// 更新玩家数据
    /// </summary>
    /// <param name="playerName">玩家名称</param>
    /// <param name="level">等级</param>
    /// <param name="experience">经验值</param>
    public void UpdatePlayerData(string playerName, int level, int experience)
    {
        _currentSaveData.playerName = playerName;
        _currentSaveData.playerLevel = level;
        _currentSaveData.playerExperience = experience;
        MarkDataDirty();
    }

    /// <summary>
    /// 更新游戏进度
    /// </summary>
    /// <param name="currentLevel">当前关卡</param>
    /// <param name="completedLevels">已完成关卡</param>
    public void UpdateGameProgress(string currentLevel, List<string> completedLevels)
    {
        _currentSaveData.currentLevel = currentLevel;
        _currentSaveData.completedLevels = completedLevels ?? new List<string>();
        MarkDataDirty();
    }

    /// <summary>
    /// 更新游戏设置
    /// </summary>
    /// <param name="settings">设置数据</param>
    public void UpdateGameSettings(Dictionary<string, object> settings)
    {
        _currentSaveData.gameSettings = settings ?? new Dictionary<string, object>();
        MarkDataDirty();
    }

    /// <summary>
    /// 添加游戏时间
    /// </summary>
    /// <param name="deltaTime">增加的时间</param>
    public void AddPlayTime(float deltaTime)
    {
        _currentSaveData.playTime += deltaTime;
        MarkDataDirty();
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 获取存档文件路径
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>文件路径</returns>
    private string GetSaveFilePath(int slot)
    {
        return Path.Combine(SaveDirectory, $"{saveFileName}_{slot}{saveFileExtension}");
    }

    /// <summary>
    /// 获取备份文件路径
    /// </summary>
    /// <param name="slot">槽位编号</param>
    /// <returns>备份文件路径</returns>
    private string GetBackupFilePath(int slot)
    {
        return Path.Combine(SaveDirectory, $"{saveFileName}_{slot}{backupSuffix}{saveFileExtension}");
    }

    /// <summary>
    /// 创建备份
    /// </summary>
    /// <param name="slot">槽位编号</param>
    private void CreateBackup(int slot)
    {
        try
        {
            string savePath = GetSaveFilePath(slot);
            string backupPath = GetBackupFilePath(slot);
            
            if (File.Exists(savePath))
            {
                File.Copy(savePath, backupPath, true);
                LogMessage($"创建存档备份: 槽位 {slot}");
            }
        }
        catch (Exception e)
        {
            LogError($"创建备份失败: {e.Message}");
        }
    }

    /// <summary>
    /// 验证存档数据
    /// </summary>
    /// <param name="saveData">存档数据</param>
    /// <returns>是否有效</returns>
    private bool ValidateSaveData(GameSaveData saveData)
    {
        if (saveData == null) return false;
        if (string.IsNullOrEmpty(saveData.playerName)) return false;
        if (saveData.playerLevel < 0) return false;
        if (saveData.playTime < 0) return false;
        
        return true;
    }

    /// <summary>
    /// 加密数据
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <returns>加密后的数据</returns>
    private string EncryptData(string data)
    {
        // 简单的XOR加密
        System.Text.StringBuilder encrypted = new System.Text.StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            encrypted.Append((char)(data[i] ^ _encryptionKey[i % _encryptionKey.Length]));
        }
        return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(encrypted.ToString()));
    }

    /// <summary>
    /// 解密数据
    /// </summary>
    /// <param name="encryptedData">加密的数据</param>
    /// <returns>解密后的数据</returns>
    private string DecryptData(string encryptedData)
    {
        // 解码Base64
        byte[] bytes = System.Convert.FromBase64String(encryptedData);
        string data = System.Text.Encoding.UTF8.GetString(bytes);
        
        // XOR解密
        System.Text.StringBuilder decrypted = new System.Text.StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            decrypted.Append((char)(data[i] ^ _encryptionKey[i % _encryptionKey.Length]));
        }
        return decrypted.ToString();
    }

    /// <summary>
    /// 清理存档管理器
    /// </summary>
    public void Cleanup()
    {
        if (_hasUnsavedChanges && _currentSlot >= 0)
        {
            QuickSave().Forget();
        }
        
        _saveSlots.Clear();
        LogMessage("存档管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[SaveManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SaveManager] {message}");
    }

    #endregion
}

/// <summary>
/// 游戏存档数据
/// </summary>
[System.Serializable]
public class GameSaveData
{
    [Header("玩家信息")]
    public string playerName = "";
    public int playerLevel = 1;
    public int playerExperience = 0;
    public Vector3 playerPosition = Vector3.zero;
    
    [Header("游戏进度")]
    public string currentLevel = "";
    public List<string> completedLevels = new List<string>();
    public float playTime = 0f;
    
    [Header("游戏设置")]
    public Dictionary<string, object> gameSettings = new Dictionary<string, object>();
    
    [Header("存档信息")]
    public DateTime saveTime = DateTime.Now;
    public string version = "";
    
    [Header("自定义数据")]
    public Dictionary<string, string> customData = new Dictionary<string, string>();
}

/// <summary>
/// 存档信息
/// </summary>
[System.Serializable]
public class SaveInfo
{
    public int slot;
    public string playerName;
    public int level;
    public float playTime;
    public DateTime saveTime;
    public string version;
}