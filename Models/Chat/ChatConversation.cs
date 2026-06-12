namespace Custom_keyboard.Models.Chat;

// A conversation always has a seller, plus exactly one of a buyer or an admin
// (buyer-admin direct chat is not allowed). May optionally link to a build request.
public sealed class ChatConversation
{
    public string ConversationId { get; set; } = string.Empty;
    public int SellerUserId { get; set; }
    public int? BuyerId { get; set; }
    public int? AdminUserId { get; set; }
    public string? BuildRequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool HasValidParticipants()
    {
        var count = 0;
        if (BuyerId is not null) count++;
        if (AdminUserId is not null) count++;
        return count == 1;
    }
}
