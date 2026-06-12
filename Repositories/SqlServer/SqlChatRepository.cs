using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Chat;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlChatRepository : IChatRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlChatRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ChatConversation> GetOrCreateConversationAsync(
        int sellerUserId,
        int? buyerId,
        int? adminUserId,
        string? buildRequestId,
        CancellationToken cancellationToken = default)
    {
        var participantCount = (buyerId is null ? 0 : 1) + (adminUserId is null ? 0 : 1);
        if (participantCount != 1)
        {
            throw new ArgumentException("A conversation must have exactly one of buyerId or adminUserId set.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var findCommand = connection.CreateCommand())
        {
            findCommand.CommandText = $"""
                SELECT TOP (1) {ConversationColumns}
                FROM chat_conversations
                WHERE seller_user_id = @seller_user_id
                  AND ISNULL(buyer_id, 0) = ISNULL(@buyer_id, 0)
                  AND ISNULL(admin_user_id, 0) = ISNULL(@admin_user_id, 0)
                  AND ISNULL(build_request_id, '') = ISNULL(@build_request_id, '')
                ORDER BY created_at, conversation_id;
                """;
            findCommand.AddParameter("@seller_user_id", SqlDbType.Int, sellerUserId);
            findCommand.AddParameter("@buyer_id", SqlDbType.Int, buyerId);
            findCommand.AddParameter("@admin_user_id", SqlDbType.Int, adminUserId);
            findCommand.AddParameter("@build_request_id", SqlDbType.VarChar, buildRequestId, 50);

            await using var findReader = await findCommand.ExecuteReaderAsync(cancellationToken);
            if (await findReader.ReadAsync(cancellationToken))
            {
                return MapConversation(findReader);
            }
        }

        var conversationId = $"CONV_{Guid.NewGuid():N}";

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = $"""
            INSERT INTO chat_conversations (
                conversation_id,
                seller_user_id,
                buyer_id,
                admin_user_id,
                build_request_id,
                created_at
            )
            VALUES (
                @conversation_id,
                @seller_user_id,
                @buyer_id,
                @admin_user_id,
                @build_request_id,
                SYSUTCDATETIME()
            );

            SELECT {ConversationColumns}
            FROM chat_conversations
            WHERE conversation_id = @conversation_id;
            """;
        insertCommand.AddParameter("@conversation_id", SqlDbType.VarChar, conversationId, 50);
        insertCommand.AddParameter("@seller_user_id", SqlDbType.Int, sellerUserId);
        insertCommand.AddParameter("@buyer_id", SqlDbType.Int, buyerId);
        insertCommand.AddParameter("@admin_user_id", SqlDbType.Int, adminUserId);
        insertCommand.AddParameter("@build_request_id", SqlDbType.VarChar, buildRequestId, 50);

        await using var reader = await insertCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapConversation(reader);
        }

        throw new InvalidOperationException("Could not create conversation.");
    }

    public async Task<ChatConversation?> GetConversationByIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ConversationColumns}
            FROM chat_conversations
            WHERE conversation_id = @conversation_id;
            """;
        command.AddParameter("@conversation_id", SqlDbType.VarChar, conversationId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapConversation(reader) : null;
    }

    public async Task<IReadOnlyList<ChatConversation>> GetConversationsForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var results = new List<ChatConversation>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ConversationColumns}
            FROM chat_conversations
            WHERE seller_user_id = @user_id
               OR buyer_id = @user_id
               OR admin_user_id = @user_id
            ORDER BY COALESCE(updated_at, created_at) DESC, conversation_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapConversation(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var results = new List<ChatMessage>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                message_id,
                conversation_id,
                sender_user_id,
                message_text,
                sent_at
            FROM chat_messages
            WHERE conversation_id = @conversation_id
            ORDER BY sent_at, message_id;
            """;
        command.AddParameter("@conversation_id", SqlDbType.VarChar, conversationId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapMessage(reader));
        }

        return results;
    }

    public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            message.MessageId = $"MSG_{Guid.NewGuid():N}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chat_messages (
                message_id,
                conversation_id,
                sender_user_id,
                message_text,
                sent_at
            )
            VALUES (
                @message_id,
                @conversation_id,
                @sender_user_id,
                @message_text,
                COALESCE(@sent_at, SYSUTCDATETIME())
            );

            UPDATE chat_conversations
            SET updated_at = SYSUTCDATETIME()
            WHERE conversation_id = @conversation_id;

            SELECT
                message_id,
                conversation_id,
                sender_user_id,
                message_text,
                sent_at
            FROM chat_messages
            WHERE message_id = @message_id;
            """;
        command.AddParameter("@message_id", SqlDbType.VarChar, message.MessageId, 50);
        command.AddParameter("@conversation_id", SqlDbType.VarChar, message.ConversationId, 50);
        command.AddParameter("@sender_user_id", SqlDbType.Int, message.SenderUserId);
        command.AddParameter("@message_text", SqlDbType.NVarChar, message.MessageText, -1);
        command.AddParameter("@sent_at", SqlDbType.DateTime2, message.SentAt == default ? null : message.SentAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapMessage(reader);
        }

        throw new InvalidOperationException("Could not insert chat message.");
    }

    private const string ConversationColumns = """
        conversation_id,
        seller_user_id,
        buyer_id,
        admin_user_id,
        build_request_id,
        created_at,
        updated_at
        """;

    private static ChatConversation MapConversation(SqlDataReader reader)
    {
        return new ChatConversation
        {
            ConversationId = reader.GetStringValue("conversation_id"),
            SellerUserId = reader.GetIntValue("seller_user_id"),
            BuyerId = reader.GetNullableIntValue("buyer_id"),
            AdminUserId = reader.GetNullableIntValue("admin_user_id"),
            BuildRequestId = reader.GetNullableStringValue("build_request_id"),
            CreatedAt = reader.GetDateTimeValue("created_at"),
            UpdatedAt = reader.GetNullableDateTimeValue("updated_at")
        };
    }

    private static ChatMessage MapMessage(SqlDataReader reader)
    {
        return new ChatMessage
        {
            MessageId = reader.GetStringValue("message_id"),
            ConversationId = reader.GetStringValue("conversation_id"),
            SenderUserId = reader.GetIntValue("sender_user_id"),
            MessageText = reader.GetStringValue("message_text"),
            SentAt = reader.GetDateTimeValue("sent_at")
        };
    }
}
