using System.Windows;
using Rooms.App.ViewModels;

namespace Rooms.App.Views;

/// <summary>Create / edit a room (§12.3).</summary>
public partial class RoomEditorWindow : Window
{
    public RoomEditorWindow(RoomEditorViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        CancelButton.Click += (_, _) => Close();
    }
}
