using System.Collections.ObjectModel;
using System.Windows.Input;
using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.ViewModels;

public abstract class RoleDashboardViewModel : ViewModelBase
{
    protected RoleDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        string title,
        string subtitle,
        IEnumerable<string> tasks)
    {
        CurrentUser = currentUser;
        LogoutCommand = logoutCommand;
        Title = title;
        Subtitle = subtitle;
        Tasks = new ObservableCollection<string>(tasks);
    }

    public User CurrentUser { get; }
    public ICommand LogoutCommand { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public ObservableCollection<string> Tasks { get; }
}
