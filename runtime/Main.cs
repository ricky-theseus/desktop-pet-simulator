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
    private Label? feedback;
    private float feedbackSeconds;

    public override void _Ready()
    {
        GetViewport().TransparentBg = true;
        GetWindow().Transparent = true;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);
        SetMousePassthroughRegion();

        var host = new ModHost(new ModHostOptions("1.0"));
        var mushroom = new MushroomMod();
        host.Register(mushroom);
        host.StartAll();

        pet = new MushroomPet();
        AddChild(pet);

        menu = new PopupMenu();
        menu.AddItem("喂蘑菇", FeedAction);
        menu.AddItem("摸摸蘑菇", PetAction);
        menu.AddSeparator();
        menu.AddItem("回到屏幕中央", CenterAction);
        menu.AddItem("退出桌宠", QuitAction);
        menu.IdPressed += OnMenuItemPressed;
        menu.PopupHide += OnMenuPopupHide;
        AddChild(menu);

        feedback = new Label
        {
            Position = new Vector2(62f, 8f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        feedback.AddThemeColorOverride("font_color", new Color("#fff0c2"));
        feedback.AddThemeColorOverride("font_shadow_color", new Color("#302339"));
        feedback.AddThemeConstantOverride("shadow_offset_x", 2);
        feedback.AddThemeConstantOverride("shadow_offset_y", 2);
        feedback.AddThemeFontSizeOverride("font_size", 12);
        AddChild(feedback);
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
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            pet is null ||
            !pet.InteractionRect.HasPoint(mouseButton.Position))
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (menu is not null)
            {
                DisplayServer.WindowSetMousePassthrough([]);
                menu.Position = new Vector2I(
                    Mathf.RoundToInt(mouseButton.Position.X),
                    Mathf.RoundToInt(mouseButton.Position.Y));
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

    private static void OnMenuPopupHide()
    {
        SetMousePassthroughRegion();
    }

    private void CenterWindow()
    {
        var screenSize = DisplayServer.ScreenGetSize();
        var windowSize = DisplayServer.WindowGetSize();
        DisplayServer.WindowSetPosition(new Vector2I(
            (screenSize.X - windowSize.X) / 2,
            (screenSize.Y - windowSize.Y) / 2));
    }

    private static void SetMousePassthroughRegion()
    {
        DisplayServer.WindowSetMousePassthrough([
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
        ]);
    }

    private void ShowFeedback(string message)
    {
        if (feedback is null)
        {
            return;
        }

        feedback.Text = message;
        feedback.Visible = true;
        feedbackSeconds = 1.2f;
    }
}
