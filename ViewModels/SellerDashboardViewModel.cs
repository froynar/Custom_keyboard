using System.Windows.Input;
using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.ViewModels;

public sealed class SellerDashboardViewModel : RoleDashboardViewModel
{
    public SellerDashboardViewModel(User currentUser, ICommand logoutCommand)
        : base(
            currentUser,
            logoutCommand,
            "Seller dashboard",
            "Xu ly request build duoc gan cho seller.",
            [
                "Xem danh sach request moi",
                "Mo chi tiet build snapshot",
                "Cap nhat trang thai request",
                "Danh dau Completed khi hoan thanh"
            ])
    {
    }
}
