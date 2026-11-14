using Godot;

namespace DesktopPet.Runtime;

/// <summary>
/// 在角色上方显示短反馈，保留独立的文字和气泡尾巴空间。
/// </summary>
public partial class SpeechBubble : PanelContainer
{
    public Label MessageLabel { get; }

    public SpeechBubble()
    {
        CustomMinimumSize = new Vector2(176f, 52f);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Ignore;

        AddThemeStyleboxOverride("panel", CreatePanelStyle());

        MessageLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        MessageLabel.AddThemeColorOverride("font_color", new Color("#302339"));
        MessageLabel.AddThemeFontSizeOverride("font_size", 20);
        AddChild(MessageLabel);

        AddChild(new Polygon2D
        {
            Polygon = [new Vector2(76f, 50f), new Vector2(100f, 50f), new Vector2(88f, 62f)],
            Color = new Color("#4a3154")
        });
        AddChild(new Polygon2D
        {
            Polygon = [new Vector2(79f, 49f), new Vector2(97f, 49f), new Vector2(88f, 59f)],
            Color = new Color("#fff4d6")
        });
    }

    internal static StyleBoxFlat CreatePanelStyle() => new()
    {
        BgColor = new Color("#fff4d6"),
        BorderColor = new Color("#4a3154"),
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomRight = 6, CornerRadiusBottomLeft = 6,
        ContentMarginLeft = 8f, ContentMarginTop = 4f,
        ContentMarginRight = 8f, ContentMarginBottom = 4f
    };
}
