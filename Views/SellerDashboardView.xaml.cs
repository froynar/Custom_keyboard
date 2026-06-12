using System.Windows.Controls;
using Custom_keyboard.ViewModels;

namespace Custom_keyboard.Views;

public partial class SellerDashboardView : UserControl
{
    public SellerDashboardView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SellerDashboardViewModel viewModel
            && viewModel.LoadCommand.CanExecute(null))
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
