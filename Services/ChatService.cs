using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Chat;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

public sealed class ChatService : IChatService
{
    private const int MessageMaxLength = 4000;

    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISellerRepository _sellerRepository;

    public ChatService(
        IChatRepository chatRepository,
        IUserRepository userRepository,
        ISellerRepository sellerRepository)
    {
        _chatRepository = chatRepository;
        _userRepository = userRepository;
        _sellerRepository = sellerRepository;
    }

    public Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("User khong hop le.");
        }

        return _chatRepository.GetConversationsForUserAsync(userId, cancellationToken);
    }

    public async Task<ChatConversation> StartBuyerConversationAsync(
        int sellerUserId,
        int buyerId,
        string? buildRequestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVerifiedSellerAsync(sellerUserId, cancellationToken);
        await EnsureRoleAsync(buyerId, UserRole.Buyer, "Buyer", cancellationToken);

        if (sellerUserId == buyerId)
        {
            throw new InvalidOperationException("Seller va buyer khong duoc trung nhau.");
        }

        return await _chatRepository.GetOrCreateConversationAsync(
            sellerUserId,
            buyerId,
            adminUserId: null,
            NormalizeNullable(buildRequestId),
            cancellationToken);
    }

    public async Task<ChatConversation> StartAdminConversationAsync(
        int sellerUserId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVerifiedSellerAsync(sellerUserId, cancellationToken);
        await EnsureRoleAsync(adminUserId, UserRole.Admin, "Admin", cancellationToken);

        return await _chatRepository.GetOrCreateConversationAsync(
            sellerUserId,
            buyerId: null,
            adminUserId,
            buildRequestId: null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string conversationId,
        int requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await RequireConversationAsync(conversationId, cancellationToken);
        EnsureParticipant(conversation, requestingUserId);
        return await _chatRepository.GetMessagesAsync(conversation.ConversationId, cancellationToken);
    }

    public async Task<ChatMessage> SendMessageAsync(
        string conversationId,
        int senderUserId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new InvalidOperationException("Noi dung tin nhan khong duoc rong.");
        }

        var text = messageText.Trim();
        if (text.Length > MessageMaxLength)
        {
            throw new InvalidOperationException($"Tin nhan khong duoc vuot qua {MessageMaxLength} ky tu.");
        }

        var conversation = await RequireConversationAsync(conversationId, cancellationToken);
        EnsureParticipant(conversation, senderUserId);

        return await _chatRepository.AddMessageAsync(
            new ChatMessage
            {
                ConversationId = conversation.ConversationId,
                SenderUserId = senderUserId,
                MessageText = text
            },
            cancellationToken);
    }

    private async Task<ChatConversation> RequireConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new InvalidOperationException("Conversation khong hop le.");
        }

        var conversation = await _chatRepository.GetConversationByIdAsync(conversationId.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Conversation khong ton tai.");

        if (!conversation.HasValidParticipants())
        {
            throw new InvalidOperationException("Conversation phai co dung mot buyer hoac admin.");
        }

        return conversation;
    }

    private static void EnsureParticipant(ChatConversation conversation, int userId)
    {
        var isParticipant = userId == conversation.SellerUserId
            || userId == conversation.BuyerId
            || userId == conversation.AdminUserId;

        if (!isParticipant)
        {
            throw new InvalidOperationException("User khong thuoc conversation nay.");
        }
    }

    private async Task EnsureVerifiedSellerAsync(int sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await EnsureRoleAsync(sellerUserId, UserRole.Seller, "Seller", cancellationToken);
        _ = seller;

        var profile = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Seller chua co profile.");

        if (!profile.IsVerified)
        {
            throw new InvalidOperationException("Seller chua duoc verified.");
        }
    }

    private async Task<User> EnsureRoleAsync(int userId, UserRole role, string roleLabel, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException($"{roleLabel} khong hop le.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"Khong tim thay {roleLabel}.");

        if (user.Role != role)
        {
            throw new InvalidOperationException($"User khong phai {roleLabel}.");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException($"{roleLabel} dang bi khoa.");
        }

        return user;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
