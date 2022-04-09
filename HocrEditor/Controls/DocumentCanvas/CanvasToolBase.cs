using System.Windows.Input;
using HocrEditor.ViewModels;
using Optional;
using Optional.Unsafe;
using SkiaSharp;

namespace HocrEditor.Controls;

public abstract class CanvasToolBase : ICanvasTool
{
    protected const int KEYBOARD_MOVE_CTRL_MULTIPLIER = 10;
    protected const int KEYBOARD_MOVE_CTRL_SHIFT_MULTIPLIER = 30;

    protected static int KeyboardDeltaMultiply(int delta = 1)
    {
        var multiplier = (Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
                Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) switch
            {
                (true, true) => KEYBOARD_MOVE_CTRL_SHIFT_MULTIPLIER,
                (true, false) => KEYBOARD_MOVE_CTRL_MULTIPLIER,
                _ => 1
            };

        return delta * multiplier;
    }

    protected Option<DocumentCanvas> Canvas { get; private set; } = Option.None<DocumentCanvas>();

    public virtual bool CanMount(HocrPageViewModel page) => true;

    public virtual void Mount(DocumentCanvas canvas)
    {
        Canvas = Option.Some(canvas);
    }

    public void Unmount()
    {
        var canvas = Canvas.ValueOrFailure();

        Unmount(canvas);

        Canvas = Option.None<DocumentCanvas>();
    }

    protected abstract void Unmount(DocumentCanvas canvas);

    public abstract void Render(SKCanvas canvas);
}
