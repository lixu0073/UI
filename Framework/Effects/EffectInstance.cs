using UnityEngine;
using DG.Tweening;

/// <summary>
/// 特效实例类
/// 表示一个正在播放的特效实例，提供控制和状态查询功能
/// </summary>
public class EffectInstance
{
    private GameObject _gameObject;
    private string _effectName;
    private bool _autoDestroy;
    private float _duration;
    private float _elapsedTime;
    private bool _isPlaying;
    private bool _isPaused;
    private bool _isFinished;

    // 跟随目标
    private Transform _followTarget;
    private Vector3 _followOffset;

    // 粒子系统相关
    private ParticleSystem _particleSystem;
    private float _originalEmissionRate;

    // 属性
    public GameObject GameObject => _gameObject;
    public string EffectName => _effectName;
    public bool IsPlaying => _isPlaying;
    public bool IsPaused => _isPaused;
    public bool IsFinished => _isFinished;
    public float ElapsedTime => _elapsedTime;
    public float Duration => _duration;
    public float Progress => _duration > 0 ? _elapsedTime / _duration : 0f;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="gameObject">特效游戏对象</param>
    /// <param name="effectName">特效名称</param>
    /// <param name="autoDestroy">是否自动销毁</param>
    public EffectInstance(GameObject gameObject, string effectName, bool autoDestroy = true)
    {
        _gameObject = gameObject;
        _effectName = effectName;
        _autoDestroy = autoDestroy;
        _isPlaying = true;
        _isPaused = false;
        _isFinished = false;
        _elapsedTime = 0f;
        _duration = -1f;

        // 获取粒子系统组件
        _particleSystem = _gameObject.GetComponent<ParticleSystem>();
        if (_particleSystem != null)
        {
            _originalEmissionRate = _particleSystem.emission.rateOverTime.constant;
        }

        // 播放特效
        Play();
    }

    /// <summary>
    /// 更新特效实例
    /// </summary>
    public void Update()
    {
        if (_isFinished) return;

        // 更新跟随目标
        UpdateFollowTarget();

        // 更新时间
        if (_isPlaying && !_isPaused)
        {
            _elapsedTime += Time.deltaTime;

            // 检查是否完成
            if (_autoDestroy && _duration > 0 && _elapsedTime >= _duration)
            {
                Finish();
            }
        }
    }

    /// <summary>
    /// 播放特效
    /// </summary>
    public void Play()
    {
        if (_gameObject == null) return;

        _isPlaying = true;
        _isPaused = false;
        _gameObject.SetActive(true);

        // 播放粒子系统
        if (_particleSystem != null)
        {
            _particleSystem.Play();
        }

        // 播放音效
        AudioSource audioSource = _gameObject.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    /// <summary>
    /// 停止特效
    /// </summary>
    public void Stop()
    {
        if (_gameObject == null) return;

        _isPlaying = false;
        
        // 停止粒子系统
        if (_particleSystem != null)
        {
            _particleSystem.Stop();
        }

        // 停止音效
        AudioSource audioSource = _gameObject.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        Finish();
    }

    /// <summary>
    /// 暂停特效
    /// </summary>
    public void Pause()
    {
        if (_gameObject == null || !_isPlaying) return;

        _isPaused = true;

        // 暂停粒子系统
        if (_particleSystem != null)
        {
            _particleSystem.Pause();
        }

        // 暂停音效
        AudioSource audioSource = _gameObject.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// 恢复特效
    /// </summary>
    public void Resume()
    {
        if (_gameObject == null || !_isPlaying) return;

        _isPaused = false;

        // 恢复粒子系统
        if (_particleSystem != null)
        {
            _particleSystem.Play();
        }

        // 恢复音效
        AudioSource audioSource = _gameObject.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.UnPause();
        }
    }

    /// <summary>
    /// 完成特效
    /// </summary>
    private void Finish()
    {
        _isFinished = true;
        _isPlaying = false;
        _isPaused = false;
        
        if (_gameObject != null)
        {
            _gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 设置持续时间
    /// </summary>
    /// <param name="duration">持续时间</param>
    public void SetDuration(float duration)
    {
        _duration = duration;
    }

    /// <summary>
    /// 设置跟随目标
    /// </summary>
    /// <param name="target">跟随目标</param>
    /// <param name="offset">偏移量</param>
    public void SetFollowTarget(Transform target, Vector3 offset = default)
    {
        _followTarget = target;
        _followOffset = offset;
    }

    /// <summary>
    /// 更新跟随目标
    /// </summary>
    private void UpdateFollowTarget()
    {
        if (_followTarget != null && _gameObject != null)
        {
            _gameObject.transform.position = _followTarget.position + _followOffset;
        }
    }

    /// <summary>
    /// 获取原始发射速率
    /// </summary>
    /// <returns>原始发射速率</returns>
    public float GetOriginalEmissionRate()
    {
        return _originalEmissionRate;
    }

    /// <summary>
    /// 设置特效缩放
    /// </summary>
    /// <param name="scale">缩放值</param>
    public void SetScale(Vector3 scale)
    {
        if (_gameObject != null)
        {
            _gameObject.transform.localScale = scale;
        }
    }

    /// <summary>
    /// 设置特效位置
    /// </summary>
    /// <param name="position">位置</param>
    public void SetPosition(Vector3 position)
    {
        if (_gameObject != null)
        {
            _gameObject.transform.position = position;
        }
    }

    /// <summary>
    /// 设置特效旋转
    /// </summary>
    /// <param name="rotation">旋转</param>
    public void SetRotation(Quaternion rotation)
    {
        if (_gameObject != null)
        {
            _gameObject.transform.rotation = rotation;
        }
    }

    /// <summary>
    /// 移动特效到指定位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="duration">移动时间</param>
    /// <param name="ease">缓动类型</param>
    public void MoveTo(Vector3 targetPosition, float duration = 1f, Ease ease = Ease.OutQuad)
    {
        if (_gameObject != null)
        {
            _gameObject.transform.DOMove(targetPosition, duration).SetEase(ease);
        }
    }

    /// <summary>
    /// 淡入特效
    /// </summary>
    /// <param name="duration">淡入时间</param>
    public void FadeIn(float duration = 1f)
    {
        if (_particleSystem != null)
        {
            var main = _particleSystem.main;
            var startColor = main.startColor;
            
            // 设置初始透明度为0
            Color color = startColor.color;
            color.a = 0f;
            main.startColor = color;
            
            // 淡入到原始透明度
            DOTween.To(() => color.a, a => {
                color.a = a;
                var newMain = _particleSystem.main;
                newMain.startColor = color;
            }, startColor.color.a, duration);
        }
    }

    /// <summary>
    /// 淡出特效
    /// </summary>
    /// <param name="duration">淡出时间</param>
    public void FadeOut(float duration = 1f)
    {
        if (_particleSystem != null)
        {
            var main = _particleSystem.main;
            Color color = main.startColor.color;
            
            DOTween.To(() => color.a, a => {
                color.a = a;
                var newMain = _particleSystem.main;
                newMain.startColor = color;
            }, 0f, duration).OnComplete(() => Stop());
        }
        else
        {
            // 如果没有粒子系统，直接延迟停止
            DOVirtual.DelayedCall(duration, () => Stop());
        }
    }
}