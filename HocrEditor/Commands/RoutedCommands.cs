using System.Windows.Input;

namespace HocrEditor.Commands;

public static class RoutedCommands
{
    public static readonly RoutedCommand OcrRegionCommand = new();
    public static readonly RoutedCommand MergeCommand = new();
    public static readonly RoutedCommand CropCommand = new();
    public static readonly RoutedCommand CreateNodeCommand = new();
    public static readonly RoutedCommand ToggleTextCommand = new();
    public static readonly RoutedCommand ToggleNumbersCommand = new();
    public static readonly RoutedCommand CreateAdjustmentFilterCommand = new();
    public static readonly RoutedCommand DeleteAdjustmentFilterCommand = new();
}
