using System.Windows.Controls;
using Custom_keyboard.ViewModels;

namespace Custom_keyboard.Views;

public partial class AdminDashboardView : UserControl
{
    public AdminDashboardView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AdminDashboardViewModel viewModel
            && viewModel.LoadCommand.CanExecute(null))
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
