using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : IPetDisplayService
{
    private PetDisplayOptions displayOptions = new();
    public PetDisplayOptions GetOptions() => displayOptions;

    public void Apply(PetDisplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (pet is null || !IsInsideTree()) throw new InvalidOperationException("宠物尚未就绪。");
        var window = GetWindow();
        var oldFoot = window.Position + pet.Position + new Vector2(96, 152) * pet.Scale;
        displayOptions = options;
        var scale = (float)options.Scale;
        window.Size = new(Math.Max(192, (int)Math.Ceiling(192 * scale)), (int)Math.Ceiling(64 + 192 * scale));
        pet.Scale = Vector2.One * scale;
        pet.Position = new((window.Size.X - 192 * scale) / 2, 64);
        pet.Modulate = new Color(1, 1, 1, (float)options.Opacity);
        pet.SetSmoothScaling(options.SmoothScaling);
        var changesWindowStyle = window.AlwaysOnTop != options.AlwaysOnTop;
        // Godot 4.5.1 在 Windows 上重复写入置顶标记也会重建样式，清除无边框副窗口的原生可见性。
        // 没有变化时保留现有样式；确实切换时由下面的任务栏适配恢复显示。
        if (changesWindowStyle) window.AlwaysOnTop = options.AlwaysOnTop;
        var nextFoot = pet.Position + new Vector2(96, 152) * scale;
        var nextPosition = (Vector2I)(oldFoot - nextFoot);
        var area = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
        window.Position = new(Math.Clamp(nextPosition.X, area.Position.X, Math.Max(area.Position.X, area.End.X - window.Size.X)),
            Math.Clamp(nextPosition.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - window.Size.Y)));
        if (feedback is not null) feedback.Position = new((window.Size.X - 176) / 2f, 4);
        if (changesWindowStyle) ApplyTaskbarStyle();
        RefreshMousePassthrough();
    }
}
