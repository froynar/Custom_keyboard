using System.Collections.ObjectModel;

namespace Custom_keyboard.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private int _selectedRoleIndex;

    public MainViewModel()
    {
        BuyerTasks =
        [
            "Dang nhap/dang ky tai khoan buyer",
            "Tra cuu danh muc linh kien va rule tuong thich",
            "Tao cau hinh build keyboard",
            "Tinh tong gia snapshot",
            "Luu build va gui request cho seller",
            "Theo doi trang thai request"
        ];

        SellerTasks =
        [
            "Dang nhap tai khoan seller",
            "Xem danh sach request duoc gan",
            "Mo chi tiet build snapshot",
            "Cap nhat trang thai Accepted/In_progress/Completed/Cancelled"
        ];

        AdminTasks =
        [
            "Dang nhap tai khoan admin",
            "Quan ly user va role",
            "Quan ly seller profile va verify seller",
            "Quan ly danh muc linh kien",
            "Xem audit log"
        ];
    }

    public string CurrentEnvironmentLabel { get; } = "WPF + SQL Server ready structure";

    public int SelectedRoleIndex
    {
        get => _selectedRoleIndex;
        set => SetProperty(ref _selectedRoleIndex, value);
    }

    public ObservableCollection<string> BuyerTasks { get; }
    public ObservableCollection<string> SellerTasks { get; }
    public ObservableCollection<string> AdminTasks { get; }
}
