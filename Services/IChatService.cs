using Custom_keyboard.Models.Chat;

namespace Custom_keyboard.Services;

// A conversation always pairs a verified seller with exactly one of a buyer or an admin.
// Buyer-admin direct chat is not allowed. Senders/readers must be participants.
public interface IChatService
{
    Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(int userId, CancellationToken cancellationToken = default);
    Task<ChatConversation> StartBuyerConversationAsync(int sellerUserId, int buyerId, string? buildRequestId, CancellationToken cancellationToken = default);
    Task<ChatConversation> StartAdminConversationAsync(int sellerUserId, int adminUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string conversationId, int requestingUserId, CancellationToken cancellationToken = default);
    Task<ChatMessage> SendMessageAsync(string conversationId, int senderUserId, string messageText, CancellationToken cancellationToken = default);
}
