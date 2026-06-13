using System.Windows.Input;
using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.ViewModels;

public abstract class RoleDashboardViewModel : ViewModelBase
{
    private readonly string _titleKey;
    private readonly string _subtitleKey;
    private readonly string[] _taskKeys;

    protected RoleDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        string titleKey,
        string subtitleKey,
        IEnumerable<string> taskKeys)
    {
        CurrentUser = currentUser;
        LogoutCommand = logoutCommand;
        _titleKey = titleKey;
        _subtitleKey = subtitleKey;
        _taskKeys = taskKeys.ToArray();
    }

    public User CurrentUser { get; }
    public ICommand LogoutCommand { get; }

    // Computed from localization keys so they re-evaluate when the language changes
    // (ViewModelBase raises an all-properties change on language switch).
    public string Title => Tr(_titleKey);
    public string Subtitle => Tr(_subtitleKey);
    public IReadOnlyList<string> Tasks => _taskKeys.Select(Tr).ToList();
}
