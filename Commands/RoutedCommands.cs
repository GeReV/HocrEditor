using System.Windows.Input;

namespace HocrEditor.Commands;

public class RoutedCommands
{
    public static readonly RoutedCommand OcrRegionCommand = new();
    public static readonly RoutedCommand MergeCommand = new();
    public static readonly RoutedCommand CreateNodeCommand = new();
}
