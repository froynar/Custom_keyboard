using System.Windows.Controls;
using Custom_keyboard.ViewModels;

namespace Custom_keyboard.Views;

public partial class BuyerDashboardView : UserControl
{
    public BuyerDashboardView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is BuyerDashboardViewModel viewModel
            && viewModel.LoadCommand.CanExecute(null))
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
