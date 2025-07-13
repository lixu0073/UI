/// <summary>
/// 游戏事件常量管理类
/// 集中管理所有游戏事件名称，确保事件名称的一致性和可维护性
/// </summary>
public static class GameEvent
{
    // 玩家相关事件
    public const string PLAYER_HEALTH_UPDATED = "Player.Health.Updated";
    public const string PLAYER_LEVEL_UP = "Player.LevelUp";
    public const string PLAYER_DIED = "Player.Died";
    
    // UI相关事件
    public const string SHOW_MESSAGE_POPUP = "UI.ShowMessagePopup";
    public const string HIDE_MESSAGE_POPUP = "UI.HideMessagePopup";
    public const string UI_PANEL_OPENED = "UI.Panel.Opened";
    public const string UI_PANEL_CLOSED = "UI.Panel.Closed";
    
    // 场景相关事件
    public const string SCENE_LOAD_STARTED = "Scene.Load.Started";
    public const string SCENE_LOAD_COMPLETED = "Scene.Load.Completed";
    public const string SCENE_LOAD_PROGRESS = "Scene.Load.Progress";
}