using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Chat;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

// Shared chat panel embedded in every dashboard. A conversation pairs a verified seller with
// either a buyer or an admin; buyers/admins can open a new conversation with a seller, sellers reply.
public sealed class ChatViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IRequestService _requestService;
    private readonly User _currentUser;
    private readonly Dictionary<int, string> _sellerNames = new();

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _messageText = string.Empty;
    private ConversationItemViewModel? _selectedConversation;
    private SellerProfile? _selectedCounterpart;

    public ChatViewModel(IChatService chatService, IRequestService requestService, User currentUser)
    {
        _chatService = chatService;
        _requestService = requestService;
        _currentUser = currentUser;

        RefreshConversationsCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshConversationsAsync, "Da refresh hoi thoai."));
        StartConversationCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(StartConversationAsync), _ => CanStartConversation && SelectedCounterpart is not null && !IsBusy);
        SendMessageCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SendMessageAsync), _ => SelectedConversation is not null && !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);
        RefreshMessagesCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshMessagesAsync), _ => SelectedConversation is not null && !IsBusy);
    }

    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = [];
    public ObservableCollection<ChatMessageItemViewModel> Messages { get; } = [];
    public ObservableCollection<SellerProfile> Counterparts { get; } = [];

    public ICommand RefreshConversationsCommand { get; }
    public ICommand StartConversationCommand { get; }
    public ICommand SendMessageCommand { get; }
    public ICommand RefreshMessagesCommand { get; }

    public bool CanStartConversation => _currentUser.Role is UserRole.Buyer or UserRole.Admin;
    public Visibility StartConversationVisibility => CanStartConversation ? Visibility.Visible : Visibility.Collapsed;
    public string CounterpartLabel => _currentUser.Role == UserRole.Admin ? "Chon seller de chat" : "Chon seller de chat";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string MessageText
    {
        get => _messageText;
        set
        {
            if (SetProperty(ref _messageText, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public ConversationItemViewModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                RaiseCommandStatesChanged();
                _ = ExecuteSafeAsync(RefreshMessagesAsync);
            }
        }
    }

    public SellerProfile? SelectedCounterpart
    {
        get => _selectedCounterpart;
        set
        {
            if (SetProperty(ref _selectedCounterpart, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            await RefreshCounterpartsAsync();
            await RefreshConversationsAsync();
        });
    }

    private async Task RefreshCounterpartsAsync()
    {
        if (!CanStartConversation)
        {
            return;
        }

        Counterparts.Clear();
        _sellerNames.Clear();
        foreach (var seller in await _requestService.GetAvailableSellersAsync(_currentUser.UserId))
        {
            Counterparts.Add(seller);
            _sellerNames[seller.UserId] = seller.ShopName;
        }

        SelectedCounterpart ??= Counterparts.FirstOrDefault();
    }

    private async Task RefreshConversationsAsync()
    {
        var previousId = SelectedConversation?.Conversation.ConversationId;
        Conversations.Clear();
        foreach (var conversation in await _chatService.GetConversationsAsync(_currentUser.UserId))
        {
            Conversations.Add(new ConversationItemViewModel(conversation, DescribeConversation(conversation)));
        }

        SelectedConversation = Conversations.FirstOrDefault(item =>
            string.Equals(item.Conversation.ConversationId, previousId, StringComparison.OrdinalIgnoreCase))
            ?? Conversations.FirstOrDefault();
    }

    private async Task RefreshMessagesAsync()
    {
        Messages.Clear();
        if (SelectedConversation is null)
        {
            return;
        }

        var messages = await _chatService.GetMessagesAsync(
            SelectedConversation.Conversation.ConversationId,
            _currentUser.UserId);

        foreach (var message in messages)
        {
            Messages.Add(new ChatMessageItemViewModel(message, message.SenderUserId == _currentUser.UserId));
        }
    }

    private async Task StartConversationAsync()
    {
        if (SelectedCounterpart is null)
        {
            throw new InvalidOperationException("Chon seller truoc.");
        }

        var conversation = _currentUser.Role == UserRole.Admin
            ? await _chatService.StartAdminConversationAsync(SelectedCounterpart.UserId, _currentUser.UserId)
            : await _chatService.StartBuyerConversationAsync(SelectedCounterpart.UserId, _currentUser.UserId, null);

        await RefreshConversationsAsync();
        SelectedConversation = Conversations.FirstOrDefault(item =>
            string.Equals(item.Conversation.ConversationId, conversation.ConversationId, StringComparison.OrdinalIgnoreCase));
        StatusMessage = "Da mo hoi thoai.";
    }

    private async Task SendMessageAsync()
    {
        if (SelectedConversation is null)
        {
            throw new InvalidOperationException("Chon hoi thoai truoc.");
        }

        await _chatService.SendMessageAsync(
            SelectedConversation.Conversation.ConversationId,
            _currentUser.UserId,
            MessageText);

        MessageText = string.Empty;
        await RefreshMessagesAsync();
    }

    private string DescribeConversation(ChatConversation conversation)
    {
        if (_currentUser.Role == UserRole.Seller)
        {
            var counterpart = conversation.BuyerId is not null
                ? $"Buyer #{conversation.BuyerId}"
                : $"Admin #{conversation.AdminUserId}";
            return conversation.BuildRequestId is null
                ? counterpart
                : $"{counterpart} • {conversation.BuildRequestId}";
        }

        var sellerName = _sellerNames.TryGetValue(conversation.SellerUserId, out var name)
            ? name
            : $"Seller #{conversation.SellerUserId}";
        return conversation.BuildRequestId is null ? sellerName : $"{sellerName} • {conversation.BuildRequestId}";
    }

    private async Task ExecuteSafeAsync(Func<Task> action, string? successMessage = null)
    {
        IsBusy = true;
        try
        {
            await action();
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                StatusMessage = successMessage;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStatesChanged()
    {
        (StartConversationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SendMessageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RefreshMessagesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}

public sealed class ConversationItemViewModel
{
    public ConversationItemViewModel(ChatConversation conversation, string title)
    {
        Conversation = conversation;
        Title = title;
    }

    public ChatConversation Conversation { get; }
    public string Title { get; }
    public string Subtitle => $"Cap nhat: {(Conversation.UpdatedAt ?? Conversation.CreatedAt):yyyy-MM-dd HH:mm}";
}

public sealed class ChatMessageItemViewModel
{
    public ChatMessageItemViewModel(ChatMessage message, bool isOwn)
    {
        Text = message.MessageText;
        SentAt = message.SentAt;
        IsOwn = isOwn;
        SenderLabel = isOwn ? "Ban" : $"User #{message.SenderUserId}";
    }

    public string Text { get; }
    public DateTime SentAt { get; }
    public bool IsOwn { get; }
    public string SenderLabel { get; }
    public HorizontalAlignment Alignment => IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
}
