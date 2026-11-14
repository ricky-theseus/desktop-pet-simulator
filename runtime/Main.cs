using DesktopPet.Core;
using DesktopPet.OfficialMushroom;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : Node2D
{
    private const int FeedAction = 1;
    private const int PetAction = 2;
    private const int CenterAction = 3;
    private const int QuitAction = 4;

    private MushroomPet? pet;
    private PopupMenu? menu;
    private SpeechBubble? feedback;
    private float feedbackSeconds;
    private static readonly Vector2 PetOffset = new(0f, 64f);

    public override void _Ready()
    {
        GetViewport().TransparentBg = true;
        GetViewport().GuiEmbedSubwindows = false;
        GetWindow().Transparent = true;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);

        var host = new ModHost(new ModHostOptions("1.0"));
        var mushroom = new MushroomMod();
        host.Register(mushroom);
        host.StartAll();

        pet = new MushroomPet { Position = PetOffset };
        AddChild(pet);

        menu = new PopupMenu { Theme = CreateMenuTheme() };
        menu.AddItem("喂蘑菇", FeedAction);
        menu.AddItem("摸摸蘑菇", PetAction);
        menu.AddSeparator();
        menu.AddItem("回到屏幕中央", CenterAction);
        menu.AddItem("退出桌宠", QuitAction);
        menu.IdPressed += OnMenuItemPressed;
        AddChild(menu);

        feedback = new SpeechBubble
        {
            Position = new Vector2(8f, 4f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(feedback);
        RefreshMousePassthrough();
    }

    public override void _Process(double delta)
    {
        if (feedback is null || !feedback.Visible)
        {
            return;
        }

        feedbackSeconds -= (float)delta;
        if (feedbackSeconds <= 0f)
        {
            feedback.Visible = false;
            RefreshMousePassthrough();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            pet is null ||
            !pet.InteractionRect.HasPoint(mouseButton.Position - pet.Position))
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (menu is not null)
            {
                menu.ResetSize();
                PositionMenuBesidePet();
                menu.Popup();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        pet.State.Interact();
        ShowFeedback(mouseButton.DoubleClick ? "蘑菇超开心！" : "摸摸！");
        DisplayServer.WindowStartDrag();
        GetViewport().SetInputAsHandled();
    }

    private void OnMenuItemPressed(long action)
    {
        if (pet is null)
        {
            return;
        }

        switch (action)
        {
            case FeedAction:
                pet.State.Feed();
                ShowFeedback("好吃！");
                break;
            case PetAction:
                pet.State.Interact();
                ShowFeedback("摸摸！");
                break;
            case CenterAction:
                CenterWindow();
                ShowFeedback("回到这里啦！");
                break;
            case QuitAction:
                GetTree().Quit();
                break;
        }
    }

    private void CenterWindow()
    {
        var workArea = DisplayServer.ScreenGetUsableRect();
        var windowSize = DisplayServer.WindowGetSize();
        DisplayServer.WindowSetPosition(workArea.Position + (workArea.Size - windowSize) / 2);
    }

    private void RefreshMousePassthrough()
    {
        Vector2[] region = [
            new Vector2(56f, 8f),
            new Vector2(140f, 8f),
            new Vector2(158f, 28f),
            new Vector2(144f, 28f),
            new Vector2(162f, 46f),
            new Vector2(176f, 88f),
            new Vector2(150f, 108f),
            new Vector2(146f, 164f),
            new Vector2(126f, 180f),
            new Vector2(104f, 180f),
            new Vector2(96f, 166f),
            new Vector2(88f, 180f),
            new Vector2(66f, 180f),
            new Vector2(46f, 164f),
            new Vector2(42f, 108f),
            new Vector2(16f, 88f),
            new Vector2(30f, 46f),
            new Vector2(48f, 28f)
        ];
        region = region.Select(point => point + PetOffset).ToArray();
        if (feedback?.Visible == true)
        {
            // Windows 会同时裁剪穿透区域之外的画面，气泡显示时须包含其边界。
            var rect = feedback.GetRect();
            region = Geometry2D.ConvexHull([
                ..region, rect.Position, new Vector2(rect.End.X, rect.Position.Y),
                rect.End, new Vector2(rect.Position.X, rect.End.Y)
            ]);
        }

        DisplayServer.WindowSetMousePassthrough(region);
    }

    private void PositionMenuBesidePet()
    {
        if (menu is null)
        {
            return;
        }

        var windowPosition = DisplayServer.WindowGetPosition();
        var windowSize = DisplayServer.WindowGetSize();
        var workArea = DisplayServer.ScreenGetUsableRect();
        var menuSize = menu.Size;
        const int gap = 8;

        var rightX = windowPosition.X + windowSize.X + gap;
        var leftX = windowPosition.X - menuSize.X - gap;
        var x = rightX + menuSize.X <= workArea.End.X
            ? rightX
            : leftX;
        x = Math.Clamp(x, workArea.Position.X, Math.Max(workArea.Position.X, workArea.End.X - menuSize.X));
        var y = Math.Clamp(windowPosition.Y + (int)PetOffset.Y, workArea.Position.Y,
            Math.Max(workArea.Position.Y, workArea.End.Y - menuSize.Y));
        menu.Position = new Vector2I(x, y);
    }

    private static Theme CreateMenuTheme()
    {
        var theme = new Theme();
        theme.SetFontSize("font_size", "PopupMenu", 20);
        theme.SetConstant("v_separation", "PopupMenu", 12);
        theme.SetColor("font_color", "PopupMenu", new Color("#302339"));
        theme.SetColor("font_hover_color", "PopupMenu", new Color("#302339"));
        var panel = SpeechBubble.CreatePanelStyle();
        panel.ContentMarginTop = 10f;
        panel.ContentMarginBottom = 10f;
        panel.ContentMarginLeft = 14f;
        panel.ContentMarginRight = 14f;
        theme.SetStylebox("panel", "PopupMenu", panel);
        theme.SetStylebox("hover", "PopupMenu", new StyleBoxFlat { BgColor = new Color("#f5d2bc") });
        theme.SetStylebox("separator", "PopupMenu", new StyleBoxLine
        {
            Color = new Color("#b29ba3"), Thickness = 1, GrowBegin = -4, GrowEnd = -4
        });
        return theme;
    }

    private void ShowFeedback(string message)
    {
        if (feedback is null)
        {
            return;
        }

        feedback.MessageLabel.Text = message;
        feedback.Visible = true;
        feedbackSeconds = 3f;
        RefreshMousePassthrough();
    }
}
