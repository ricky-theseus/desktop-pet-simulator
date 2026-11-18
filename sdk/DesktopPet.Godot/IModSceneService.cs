namespace DesktopPet.Godot;

/// <summary>提供当前 Mod 的独立场景容器；停用时宿主清理容器和窗口。</summary>
public interface IModSceneService
{
    string ApiVersion { get; }
    global::Godot.Node Root { get; }

    /// <summary>将未挂载窗口加入 Root 并显示；已挂载窗口必须属于 Root，同时处理平台激活。</summary>
    void Present(global::Godot.Window window);
}
