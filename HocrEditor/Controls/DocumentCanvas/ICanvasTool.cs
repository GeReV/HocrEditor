using HocrEditor.ViewModels;
using SkiaSharp;

namespace HocrEditor.Controls;

public interface ICanvasTool
{
    bool CanMount(HocrPageViewModel page);

    void Mount(DocumentCanvas canvas);

    void Unmount();

    void Render(SKCanvas canvas);
}
