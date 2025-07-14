using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// 音频管理器
/// 负责管理游戏中的所有音频播放，包括背景音乐、音效等
/// 支持音频淡入淡出、音量控制、音频池管理等功能
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("音频设置")]
    [SerializeField] private int maxAudioSources = 10;
    [SerializeField] private float defaultFadeDuration = 1f;
    [SerializeField] private bool enableAudioDebug = false;

    // 单例实例
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 音频源管理
    private AudioSource _musicSource;
    private List<AudioSource> _sfxSources;
    private Queue<AudioSource> _availableSources;
    private Dictionary<string, AudioClip> _audioClips;

    // 音量控制
    private float _masterVolume = 1f;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;

    // 状态控制
    private bool _isInitialized = false;
    private bool _isMusicPaused = false;
    private string _currentMusicName;

    // 淡入淡出控制
    private Tween _musicFadeTween;

    #region 初始化

    /// <summary>
    /// 异步初始化音频管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (_isInitialized)
        {
            LogMessage("音频管理器已经初始化");
            return;
        }

        LogMessage("初始化音频管理器...");

        // 初始化音频源
        InitializeAudioSources();

        // 加载音频剪辑
        await LoadAudioClipsAsync();

        _isInitialized = true;
        LogMessage("音频管理器初始化完成");
    }

    /// <summary>
    /// 初始化音频源
    /// </summary>
    private void InitializeAudioSources()
    {
        // 创建音乐播放源
        GameObject musicGO = new GameObject("MusicSource");
        musicGO.transform.SetParent(transform);
        _musicSource = musicGO.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;

        // 创建音效播放源池
        _sfxSources = new List<AudioSource>();
        _availableSources = new Queue<AudioSource>();

        for (int i = 0; i < maxAudioSources; i++)
        {
            GameObject sfxGO = new GameObject($"SFXSource_{i}");
            sfxGO.transform.SetParent(transform);
            AudioSource source = sfxGO.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            _sfxSources.Add(source);
            _availableSources.Enqueue(source);
        }

        LogMessage($"创建了{maxAudioSources}个音频源");
    }

    /// <summary>
    /// 异步加载音频剪辑
    /// </summary>
    private async UniTask LoadAudioClipsAsync()
    {
        _audioClips = new Dictionary<string, AudioClip>();

        // 从Resources文件夹加载音频剪辑
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
        
        foreach (AudioClip clip in clips)
        {
            if (clip != null)
            {
                _audioClips[clip.name] = clip;
                LogMessage($"加载音频剪辑: {clip.name}");
            }
        }

        await UniTask.Yield();
        LogMessage($"共加载了{_audioClips.Count}个音频剪辑");
    }

    #endregion

    #region 音乐播放

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="musicName">音乐名称</param>
    /// <param name="fadeIn">是否淡入</param>
    /// <param name="fadeDuration">淡入时长</param>
    public void PlayMusic(string musicName, bool fadeIn = true, float fadeDuration = -1f)
    {
        if (!_isInitialized)
        {
            LogWarning("音频管理器未初始化");
            return;
        }

        if (string.IsNullOrEmpty(musicName))
        {
            LogWarning("音乐名称为空");
            return;
        }

        if (!_audioClips.TryGetValue(musicName, out AudioClip clip))
        {
            LogError($"找不到音乐剪辑: {musicName}");
            return;
        }

        // 如果已经在播放相同音乐，则不重复播放
        if (_currentMusicName == musicName && _musicSource.isPlaying)
        {
            LogMessage($"音乐 {musicName} 已在播放");
            return;
        }

        // 停止当前音乐的淡入淡出
        _musicFadeTween?.Kill();

        _currentMusicName = musicName;
        _musicSource.clip = clip;
        _musicSource.volume = fadeIn ? 0f : _masterVolume * _musicVolume;
        _musicSource.Play();

        if (fadeIn)
        {
            float duration = fadeDuration > 0 ? fadeDuration : defaultFadeDuration;
            _musicFadeTween = _musicSource.DOFade(_masterVolume * _musicVolume, duration);
        }

        LogMessage($"播放音乐: {musicName}");
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    /// <param name="fadeOut">是否淡出</param>
    /// <param name="fadeDuration">淡出时长</param>
    public void StopMusic(bool fadeOut = true, float fadeDuration = -1f)
    {
        if (!_musicSource.isPlaying) return;

        _musicFadeTween?.Kill();

        if (fadeOut)
        {
            float duration = fadeDuration > 0 ? fadeDuration : defaultFadeDuration;
            _musicFadeTween = _musicSource.DOFade(0f, duration)
                .OnComplete(() =>
                {
                    _musicSource.Stop();
                    _currentMusicName = null;
                });
        }
        else
        {
            _musicSource.Stop();
            _currentMusicName = null;
        }

        LogMessage("停止音乐播放");
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void PauseMusic()
    {
        if (_musicSource.isPlaying && !_isMusicPaused)
        {
            _musicSource.Pause();
            _isMusicPaused = true;
            LogMessage("暂停音乐播放");
        }
    }

    /// <summary>
    /// 恢复背景音乐
    /// </summary>
    public void ResumeMusic()
    {
        if (_isMusicPaused)
        {
            _musicSource.UnPause();
            _isMusicPaused = false;
            LogMessage("恢复音乐播放");
        }
    }

    #endregion

    #region 音效播放

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="sfxName">音效名称</param>
    /// <param name="volume">音量（0-1）</param>
    /// <param name="pitch">音调（0.1-3）</param>
    /// <returns>播放的音频源，可用于后续控制</returns>
    public AudioSource PlaySFX(string sfxName, float volume = 1f, float pitch = 1f)
    {
        if (!_isInitialized)
        {
            LogWarning("音频管理器未初始化");
            return null;
        }

        if (string.IsNullOrEmpty(sfxName))
        {
            LogWarning("音效名称为空");
            return null;
        }

        if (!_audioClips.TryGetValue(sfxName, out AudioClip clip))
        {
            LogError($"找不到音效剪辑: {sfxName}");
            return null;
        }

        AudioSource source = GetAvailableAudioSource();
        if (source == null)
        {
            LogWarning("没有可用的音频源");
            return null;
        }

        source.clip = clip;
        source.volume = _masterVolume * _sfxVolume * volume;
        source.pitch = pitch;
        source.Play();

        // 音效播放完成后回收音频源
        StartCoroutine(ReturnAudioSourceAfterPlay(source, clip.length / pitch));

        LogMessage($"播放音效: {sfxName}");
        return source;
    }

    /// <summary>
    /// 播放音效（异步版本）
    /// </summary>
    /// <param name="sfxName">音效名称</param>
    /// <param name="volume">音量</param>
    /// <param name="pitch">音调</param>
    /// <returns>播放任务</returns>
    public async UniTask<AudioSource> PlaySFXAsync(string sfxName, float volume = 1f, float pitch = 1f)
    {
        AudioSource source = PlaySFX(sfxName, volume, pitch);
        
        if (source != null && source.clip != null)
        {
            // 等待音效播放完成
            float duration = source.clip.length / pitch;
            await UniTask.Delay((int)(duration * 1000));
        }

        return source;
    }

    /// <summary>
    /// 停止所有音效
    /// </summary>
    public void StopAllSFX()
    {
        foreach (AudioSource source in _sfxSources)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }

        // 重新填充可用音频源队列
        _availableSources.Clear();
        foreach (AudioSource source in _sfxSources)
        {
            _availableSources.Enqueue(source);
        }

        LogMessage("停止所有音效");
    }

    /// <summary>
    /// 停止指定音效
    /// </summary>
    /// <param name="source">要停止的音频源</param>
    public void StopSFX(AudioSource source)
    {
        if (source != null && source.isPlaying)
        {
            source.Stop();
            ReturnAudioSource(source);
            LogMessage("停止指定音效");
        }
    }

    #endregion

    #region 音量控制

    /// <summary>
    /// 设置主音量
    /// </summary>
    /// <param name="volume">音量（0-1）</param>
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
        LogMessage($"设置主音量: {_masterVolume}");
    }

    /// <summary>
    /// 设置音乐音量
    /// </summary>
    /// <param name="volume">音量（0-1）</param>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_musicSource != null)
        {
            _musicSource.volume = _masterVolume * _musicVolume;
        }
        LogMessage($"设置音乐音量: {_musicVolume}");
    }

    /// <summary>
    /// 设置音效音量
    /// </summary>
    /// <param name="volume">音量（0-1）</param>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        LogMessage($"设置音效音量: {_sfxVolume}");
    }

    /// <summary>
    /// 获取主音量
    /// </summary>
    public float GetMasterVolume() => _masterVolume;

    /// <summary>
    /// 获取音乐音量
    /// </summary>
    public float GetMusicVolume() => _musicVolume;

    /// <summary>
    /// 获取音效音量
    /// </summary>
    public float GetSFXVolume() => _sfxVolume;

    #endregion

    #region 私有方法

    /// <summary>
    /// 获取可用的音频源
    /// </summary>
    /// <returns>可用的音频源</returns>
    private AudioSource GetAvailableAudioSource()
    {
        // 优先从队列中获取
        if (_availableSources.Count > 0)
        {
            return _availableSources.Dequeue();
        }

        // 寻找未在播放的音频源
        foreach (AudioSource source in _sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回音频源到可用队列
    /// </summary>
    /// <param name="source">要返回的音频源</param>
    private void ReturnAudioSource(AudioSource source)
    {
        if (source != null && !_availableSources.Contains(source))
        {
            source.clip = null;
            _availableSources.Enqueue(source);
        }
    }

    /// <summary>
    /// 音效播放完成后返回音频源
    /// </summary>
    /// <param name="source">音频源</param>
    /// <param name="delay">延迟时间</param>
    /// <returns>协程</returns>
    private System.Collections.IEnumerator ReturnAudioSourceAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnAudioSource(source);
    }

    /// <summary>
    /// 更新所有音量
    /// </summary>
    private void UpdateAllVolumes()
    {
        // 更新音乐音量
        if (_musicSource != null)
        {
            _musicSource.volume = _masterVolume * _musicVolume;
        }

        // 音效音量会在播放时应用，这里不需要更新
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 停止所有音频
    /// </summary>
    public void StopAll()
    {
        StopMusic(false);
        StopAllSFX();
        LogMessage("停止所有音频");
    }

    /// <summary>
    /// 更新方法（由GameManager调用）
    /// </summary>
    public void Update()
    {
        // 这里可以添加需要每帧更新的逻辑
    }

    /// <summary>
    /// 清理音频管理器
    /// </summary>
    public void Cleanup()
    {
        _musicFadeTween?.Kill();
        StopAll();
        
        if (_audioClips != null)
        {
            _audioClips.Clear();
        }

        LogMessage("音频管理器已清理");
    }

    /// <summary>
    /// 检查音频剪辑是否存在
    /// </summary>
    /// <param name="clipName">剪辑名称</param>
    /// <returns>是否存在</returns>
    public bool HasAudioClip(string clipName)
    {
        return _audioClips != null && _audioClips.ContainsKey(clipName);
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableAudioDebug)
        {
            Debug.Log($"[AudioManager] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (enableAudioDebug)
        {
            Debug.LogWarning($"[AudioManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[AudioManager] {message}");
    }

    #endregion
}