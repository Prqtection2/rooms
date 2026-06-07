using Rooms.App.ViewModels;

namespace Rooms.App.Views;

/// <summary>Diagnostic window listing every top-level window and its room ownership.</summary>
public partial class WindowDebugWindow : System.Windows.Window
{
    public WindowDebugWindow(WindowDebugViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
