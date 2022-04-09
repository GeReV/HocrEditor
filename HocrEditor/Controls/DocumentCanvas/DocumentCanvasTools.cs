namespace HocrEditor.Controls;

public static class DocumentCanvasTools
{
    public static readonly ICanvasTool SelectionTool = new SelectionTool();

    public static readonly ICanvasTool RegionSelectionTool = new RegionSelectionTool();

    public static readonly ICanvasTool WordSplittingTool = new WordSplittingTool();
}
