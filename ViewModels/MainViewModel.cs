using System.Collections.ObjectModel;

namespace Custom_keyboard.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private int _selectedRoleIndex;

    public MainViewModel()
    {
        BuyerTasks =
        [
            Tr("Main_BuyerTask1"),
            Tr("Main_BuyerTask2"),
            Tr("Main_BuyerTask3"),
            Tr("Main_BuyerTask4"),
            Tr("Main_BuyerTask5"),
            Tr("Main_BuyerTask6")
        ];

        SellerTasks =
        [
            Tr("Main_SellerTask1"),
            Tr("Main_SellerTask2"),
            Tr("Main_SellerTask3"),
            Tr("Main_SellerTask4")
        ];

        AdminTasks =
        [
            Tr("Main_AdminTask1"),
            Tr("Main_AdminTask2"),
            Tr("Main_AdminTask3"),
            Tr("Main_AdminTask4"),
            Tr("Main_AdminTask5")
        ];
    }

    public string CurrentEnvironmentLabel => Tr("Main_Environment");

    public int SelectedRoleIndex
    {
        get => _selectedRoleIndex;
        set => SetProperty(ref _selectedRoleIndex, value);
    }

    public ObservableCollection<string> BuyerTasks { get; }
    public ObservableCollection<string> SellerTasks { get; }
    public ObservableCollection<string> AdminTasks { get; }
}
