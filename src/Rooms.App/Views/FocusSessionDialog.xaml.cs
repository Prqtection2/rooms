using System.Windows;
using Rooms.App.ViewModels;

namespace Rooms.App.Views;

/// <summary>Dialog to start a focus session in a chosen room for a chosen duration (§10).</summary>
public partial class FocusSessionDialog : Window
{
    public FocusSessionDialog(FocusSessionDialogViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        viewModel.Started += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        CancelButton.Click += (_, _) => Close();
    }
}
