using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 输入管理器
/// 统一管理游戏的输入系统，支持键盘、鼠标、触摸和手柄输入
/// 提供输入事件、输入映射和输入缓冲等功能
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("输入设置")]
    [SerializeField] private bool enableKeyboard = true;
    [SerializeField] private bool enableMouse = true;
    [SerializeField] private bool enableTouch = true;
    [SerializeField] private bool enableGamepad = false;
    
    [Header("输入缓冲")]
    [SerializeField] private float inputBufferTime = 0.1f;
    [SerializeField] private int maxBufferSize = 10;
    
    [Header("触摸设置")]
    [SerializeField] private float touchSensitivity = 1f;
    [SerializeField] private float swipeThreshold = 50f;
    [SerializeField] private float tapMaxDuration = 0.3f;
    
    [Header("调试选项")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showInputDebug = false;

    // 单例实例
    private static InputManager _instance;
    public static InputManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("InputManager");
                _instance = go.AddComponent<InputManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 输入状态
    private Dictionary<string, bool> _keyStates = new Dictionary<string, bool>();
    private Dictionary<string, bool> _keyDownStates = new Dictionary<string, bool>();
    private Dictionary<string, bool> _keyUpStates = new Dictionary<string, bool>();
    
    // 输入缓冲
    private Queue<InputEvent> _inputBuffer = new Queue<InputEvent>();
    private Dictionary<string, float> _inputTimers = new Dictionary<string, float>();
    
    // 鼠标和触摸
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta;
    private bool _mouseLeftDown;
    private bool _mouseRightDown;
    
    // 触摸状态
    private Dictionary<int, TouchInfo> _touches = new Dictionary<int, TouchInfo>();
    private Vector2 _lastTouchPosition;
    private float _touchStartTime;
    
    // 轴输入
    private float _horizontalAxis;
    private float _verticalAxis;
    private Vector2 _moveInput;

    // 事件
    public System.Action<string> OnKeyDown;
    public System.Action<string> OnKeyUp;
    public System.Action<Vector2> OnMouseMove;
    public System.Action<Vector2> OnMouseClick;
    public System.Action<Vector2> OnTouchStart;
    public System.Action<Vector2> OnTouchEnd;
    public System.Action<Vector2, Vector2> OnSwipe;
    public System.Action<Vector2> OnTap;

    // 属性
    public Vector2 MoveInput => _moveInput;
    public Vector2 MousePosition => _mousePosition;
    public Vector2 MouseDelta => _mouseDelta;
    public bool IsInitialized { get; private set; } = false;

    #region 初始化

    /// <summary>
    /// 异步初始化输入管理器
    /// </summary>
    /// <returns>初始化任务</returns>
    public async UniTask InitializeAsync()
    {
        if (IsInitialized)
        {
            LogMessage("输入管理器已经初始化");
            return;
        }

        LogMessage("初始化输入管理器...");

        // 初始化输入映射
        InitializeInputMapping();

        IsInitialized = true;
        await UniTask.Yield();
        
        LogMessage("输入管理器初始化完成");
    }

    /// <summary>
    /// 初始化输入映射
    /// </summary>
    private void InitializeInputMapping()
    {
        // 注册常用按键
        RegisterKey("Jump", KeyCode.Space);
        RegisterKey("Attack", KeyCode.Z);
        RegisterKey("Dash", KeyCode.X);
        RegisterKey("Interact", KeyCode.E);
        RegisterKey("Pause", KeyCode.Escape);
        RegisterKey("Menu", KeyCode.Tab);

        LogMessage("输入映射初始化完成");
    }

    #endregion

    #region Unity生命周期

    /// <summary>
    /// 更新（由GameManager调用）
    /// </summary>
    public void Update()
    {
        if (!IsInitialized) return;

        UpdateKeyboardInput();
        UpdateMouseInput();
        UpdateTouchInput();
        UpdateAxisInput();
        UpdateInputBuffer();
        
        if (showInputDebug)
        {
            UpdateDebugDisplay();
        }
    }

    #endregion

    #region 键盘输入

    /// <summary>
    /// 更新键盘输入
    /// </summary>
    private void UpdateKeyboardInput()
    {
        if (!enableKeyboard) return;

        // 清除上一帧的按下和抬起状态
        _keyDownStates.Clear();
        _keyUpStates.Clear();

        // 检查所有注册的按键
        List<string> keysToCheck = new List<string>(_keyStates.Keys);
        foreach (string keyName in keysToCheck)
        {
            bool wasPressed = _keyStates[keyName];
            bool isPressed = GetKeyPressed(keyName);
            
            _keyStates[keyName] = isPressed;

            // 检查按下
            if (isPressed && !wasPressed)
            {
                _keyDownStates[keyName] = true;
                OnKeyDown?.Invoke(keyName);
                AddInputToBuffer(new InputEvent(InputEventType.KeyDown, keyName));
                LogMessage($"按键按下: {keyName}");
            }
            
            // 检查抬起
            if (!isPressed && wasPressed)
            {
                _keyUpStates[keyName] = true;
                OnKeyUp?.Invoke(keyName);
                AddInputToBuffer(new InputEvent(InputEventType.KeyUp, keyName));
                LogMessage($"按键抬起: {keyName}");
            }
        }
    }

    /// <summary>
    /// 注册按键映射
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <param name="keyCode">按键代码</param>
    public void RegisterKey(string actionName, KeyCode keyCode)
    {
        _keyStates[actionName] = false;
        LogMessage($"注册按键: {actionName} -> {keyCode}");
    }

    /// <summary>
    /// 获取按键是否被按下
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <returns>是否按下</returns>
    private bool GetKeyPressed(string actionName)
    {
        switch (actionName)
        {
            case "Jump": return Input.GetKey(KeyCode.Space);
            case "Attack": return Input.GetKey(KeyCode.Z);
            case "Dash": return Input.GetKey(KeyCode.X);
            case "Interact": return Input.GetKey(KeyCode.E);
            case "Pause": return Input.GetKey(KeyCode.Escape);
            case "Menu": return Input.GetKey(KeyCode.Tab);
            default: return false;
        }
    }

    #endregion

    #region 鼠标输入

    /// <summary>
    /// 更新鼠标输入
    /// </summary>
    private void UpdateMouseInput()
    {
        if (!enableMouse) return;

        // 更新鼠标位置
        Vector2 newMousePosition = Input.mousePosition;
        _mouseDelta = newMousePosition - _mousePosition;
        _mousePosition = newMousePosition;

        // 鼠标移动事件
        if (_mouseDelta.magnitude > 0.1f)
        {
            OnMouseMove?.Invoke(_mouseDelta);
        }

        // 鼠标点击
        if (Input.GetMouseButtonDown(0))
        {
            _mouseLeftDown = true;
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(_mousePosition);
            OnMouseClick?.Invoke(worldPos);
            AddInputToBuffer(new InputEvent(InputEventType.MouseClick, "LeftClick", worldPos));
            LogMessage($"鼠标左键点击: {worldPos}");
        }

        if (Input.GetMouseButtonDown(1))
        {
            _mouseRightDown = true;
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(_mousePosition);
            AddInputToBuffer(new InputEvent(InputEventType.MouseClick, "RightClick", worldPos));
            LogMessage($"鼠标右键点击: {worldPos}");
        }

        // 重置鼠标按下状态
        if (Input.GetMouseButtonUp(0)) _mouseLeftDown = false;
        if (Input.GetMouseButtonUp(1)) _mouseRightDown = false;
    }

    #endregion

    #region 触摸输入

    /// <summary>
    /// 更新触摸输入
    /// </summary>
    private void UpdateTouchInput()
    {
        if (!enableTouch) return;

        // 处理所有触摸点
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            ProcessTouch(touch);
        }

        // 清理结束的触摸
        List<int> touchesToRemove = new List<int>();
        foreach (var kvp in _touches)
        {
            if (kvp.Value.phase == TouchPhase.Ended || kvp.Value.phase == TouchPhase.Canceled)
            {
                touchesToRemove.Add(kvp.Key);
            }
        }

        foreach (int touchId in touchesToRemove)
        {
            _touches.Remove(touchId);
        }
    }

    /// <summary>
    /// 处理单个触摸
    /// </summary>
    /// <param name="touch">触摸信息</param>
    private void ProcessTouch(Touch touch)
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(touch.position);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                _touches[touch.fingerId] = new TouchInfo
                {
                    startPosition = touch.position,
                    currentPosition = touch.position,
                    startTime = Time.time,
                    phase = touch.phase
                };
                
                _lastTouchPosition = touch.position;
                _touchStartTime = Time.time;
                
                OnTouchStart?.Invoke(worldPos);
                AddInputToBuffer(new InputEvent(InputEventType.TouchStart, "Touch", worldPos));
                LogMessage($"触摸开始: {worldPos}");
                break;

            case TouchPhase.Moved:
                if (_touches.ContainsKey(touch.fingerId))
                {
                    _touches[touch.fingerId].currentPosition = touch.position;
                    _touches[touch.fingerId].phase = touch.phase;
                }
                break;

            case TouchPhase.Ended:
                if (_touches.ContainsKey(touch.fingerId))
                {
                    TouchInfo touchInfo = _touches[touch.fingerId];
                    Vector2 deltaPos = touch.position - touchInfo.startPosition;
                    float duration = Time.time - touchInfo.startTime;

                    // 检查是否为点击
                    if (duration <= tapMaxDuration && deltaPos.magnitude < swipeThreshold)
                    {
                        OnTap?.Invoke(worldPos);
                        AddInputToBuffer(new InputEvent(InputEventType.Tap, "Tap", worldPos));
                        LogMessage($"点击: {worldPos}");
                    }
                    // 检查是否为滑动
                    else if (deltaPos.magnitude >= swipeThreshold)
                    {
                        Vector2 swipeDirection = deltaPos.normalized;
                        OnSwipe?.Invoke(worldPos, swipeDirection);
                        AddInputToBuffer(new InputEvent(InputEventType.Swipe, "Swipe", worldPos, swipeDirection));
                        LogMessage($"滑动: {worldPos}, 方向: {swipeDirection}");
                    }

                    _touches[touch.fingerId].phase = touch.phase;
                }

                OnTouchEnd?.Invoke(worldPos);
                AddInputToBuffer(new InputEvent(InputEventType.TouchEnd, "Touch", worldPos));
                break;
        }
    }

    #endregion

    #region 轴输入

    /// <summary>
    /// 更新轴输入
    /// </summary>
    private void UpdateAxisInput()
    {
        // 获取水平和垂直轴
        _horizontalAxis = Input.GetAxisRaw("Horizontal");
        _verticalAxis = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(_horizontalAxis, _verticalAxis);

        // 处理触摸移动输入（虚拟摇杆）
        if (enableTouch && Input.touchCount > 0)
        {
            // 这里可以添加虚拟摇杆的处理逻辑
        }
    }

    #endregion

    #region 输入查询

    /// <summary>
    /// 获取按键状态
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <returns>是否按下</returns>
    public bool GetKey(string actionName)
    {
        return _keyStates.ContainsKey(actionName) && _keyStates[actionName];
    }

    /// <summary>
    /// 获取按键按下事件
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <returns>是否在本帧按下</returns>
    public bool GetKeyDown(string actionName)
    {
        return _keyDownStates.ContainsKey(actionName) && _keyDownStates[actionName];
    }

    /// <summary>
    /// 获取按键抬起事件
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <returns>是否在本帧抬起</returns>
    public bool GetKeyUp(string actionName)
    {
        return _keyUpStates.ContainsKey(actionName) && _keyUpStates[actionName];
    }

    /// <summary>
    /// 获取水平轴输入
    /// </summary>
    /// <returns>水平轴值</returns>
    public float GetHorizontalAxis()
    {
        return _horizontalAxis;
    }

    /// <summary>
    /// 获取垂直轴输入
    /// </summary>
    /// <returns>垂直轴值</returns>
    public float GetVerticalAxis()
    {
        return _verticalAxis;
    }

    /// <summary>
    /// 获取移动输入向量
    /// </summary>
    /// <returns>移动向量</returns>
    public Vector2 GetMoveInput()
    {
        return _moveInput;
    }

    #endregion

    #region 输入缓冲

    /// <summary>
    /// 添加输入到缓冲区
    /// </summary>
    /// <param name="inputEvent">输入事件</param>
    private void AddInputToBuffer(InputEvent inputEvent)
    {
        _inputBuffer.Enqueue(inputEvent);
        _inputTimers[inputEvent.actionName] = Time.time;

        // 限制缓冲区大小
        while (_inputBuffer.Count > maxBufferSize)
        {
            _inputBuffer.Dequeue();
        }
    }

    /// <summary>
    /// 更新输入缓冲
    /// </summary>
    private void UpdateInputBuffer()
    {
        // 清理过期的输入计时器
        List<string> expiredKeys = new List<string>();
        foreach (var kvp in _inputTimers)
        {
            if (Time.time - kvp.Value > inputBufferTime)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (string key in expiredKeys)
        {
            _inputTimers.Remove(key);
        }
    }

    /// <summary>
    /// 检查缓冲输入
    /// </summary>
    /// <param name="actionName">动作名称</param>
    /// <returns>是否在缓冲时间内有输入</returns>
    public bool GetBufferedInput(string actionName)
    {
        return _inputTimers.ContainsKey(actionName);
    }

    /// <summary>
    /// 消耗缓冲输入
    /// </summary>
    /// <param name="actionName">动作名称</param>
    public void ConsumeBufferedInput(string actionName)
    {
        if (_inputTimers.ContainsKey(actionName))
        {
            _inputTimers.Remove(actionName);
        }
    }

    #endregion

    #region 调试和工具

    /// <summary>
    /// 更新调试显示
    /// </summary>
    private void UpdateDebugDisplay()
    {
        if (Time.frameCount % 30 == 0) // 每半秒更新一次
        {
            LogMessage($"移动输入: {_moveInput}, 活跃按键: {_keyStates.Count}");
        }
    }

    /// <summary>
    /// 清理输入管理器
    /// </summary>
    public void Cleanup()
    {
        _keyStates.Clear();
        _keyDownStates.Clear();
        _keyUpStates.Clear();
        _inputBuffer.Clear();
        _inputTimers.Clear();
        _touches.Clear();
        
        LogMessage("输入管理器已清理");
    }

    #endregion

    #region 日志方法

    private void LogMessage(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[InputManager] {message}");
        }
    }

    #endregion
}

/// <summary>
/// 输入事件类型
/// </summary>
public enum InputEventType
{
    KeyDown,
    KeyUp,
    MouseClick,
    TouchStart,
    TouchEnd,
    Tap,
    Swipe
}

/// <summary>
/// 输入事件数据
/// </summary>
[System.Serializable]
public class InputEvent
{
    public InputEventType type;
    public string actionName;
    public Vector2 position;
    public Vector2 direction;
    public float timestamp;

    public InputEvent(InputEventType type, string actionName, Vector2 position = default, Vector2 direction = default)
    {
        this.type = type;
        this.actionName = actionName;
        this.position = position;
        this.direction = direction;
        this.timestamp = Time.time;
    }
}

/// <summary>
/// 触摸信息
/// </summary>
[System.Serializable]
public class TouchInfo
{
    public Vector2 startPosition;
    public Vector2 currentPosition;
    public float startTime;
    public TouchPhase phase;
}