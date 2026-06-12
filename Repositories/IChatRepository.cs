using Custom_keyboard.Models.Chat;

namespace Custom_keyboard.Repositories;

public interface IChatRepository
{
    // Finds the seller+participant conversation (optionally scoped to a build request) or creates it.
    // Exactly one of buyerId / adminUserId must be set.
    Task<ChatConversation> GetOrCreateConversationAsync(
        int sellerUserId,
        int? buyerId,
        int? adminUserId,
        string? buildRequestId,
        CancellationToken cancellationToken = default);

    Task<ChatConversation?> GetConversationByIdAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatConversation>> GetConversationsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
}
