using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Chat;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Realtime;
using Custom_keyboard.Repositories;
using Custom_keyboard.Repositories.SqlServer;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Security;
using Microsoft.Data.SqlClient;

var runner = new Phase6Runner();
return await runner.RunAsync();

internal sealed class Phase6Runner
{
    private readonly List<string> _failures = [];
    private int _passed;

    public async Task<int> RunAsync()
    {
        Console.WriteLine("Phase 6 verification started.");

        await Run("BuildService validates totals and applies snapshots", UnitBuildServiceValidTotalAsync);
        await Run("BuildService rejects incompatible switch technology", UnitBuildServiceRejectsIncompatibleSwitchAsync);
        await Run("RequestService enforces request status state machine", UnitRequestServiceStateMachineAsync);
        await Run("RequestService rejects requests to unverified sellers", UnitRequestServiceRejectsUnverifiedSellerAsync);
        await Run("RequestService scopes requests to the owning seller (T08/T09)", UnitRequestServiceSellerScopingAsync);
        await Run("RequestService publishes realtime after DB write (best-effort)", UnitRequestServiceRealtimeBestEffortAsync);
        await Run("ChatService enforces participants and verified sellers", UnitChatServiceParticipantsAsync);
        await Run("ChatService supports admin-seller conversations (T14)", UnitChatServiceAdminConversationAsync);
        await Run("AccountService validates email/phone on register", UnitAccountServiceRegisterValidationAsync);
        await Run("AccountService login accepts valid and blocks wrong/banned (T01/T02)", UnitAccountServiceLoginAsync);
        await Run("AdminService writes audit entries for admin actions", UnitAdminServiceAuditActionsAsync);
        await Run("SQL integration covers build/request/chat CRUD", IntegrationSqlBuildRequestChatAsync);
        await Run("SQL integration: seed accounts log in with Password123 (Phase 10)", IntegrationSqlSeedAccountLoginAsync);
        await Run("VerifyRefactor invariant queries return clean results", IntegrationSqlVerifyRefactorAsync);

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failures.Count}");

        if (_failures.Count == 0)
        {
            Console.WriteLine("Phase 6 verification passed.");
            return 0;
        }

        foreach (var failure in _failures)
        {
            Console.WriteLine(failure);
        }

        return 1;
    }

    private async Task Run(string name, Func<Task> test)
    {
        try
        {
            await test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failures.Add($"FAIL {name}{Environment.NewLine}{ex}");
            Console.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static async Task UnitBuildServiceValidTotalAsync()
    {
        var catalog = FakeCatalog.Standard();
        var builds = new FakeBuildRepository();
        var service = new BuildService(builds, catalog);
        var build = StandardBuild();

        var saved = await service.SaveBuildAsync(build);

        AssertEqual(217.30m, saved.TotalCostSnapshot, "total snapshot");
        AssertEqual(BuildStatus.Saved, saved.Status, "saved status");
        AssertEqual(5, saved.Items.Count, "item count");
        AssertEqual(0.40m, saved.Items.Single(item => item.SwitchId is not null).UnitPriceSnapshot, "switch snapshot");
        AssertEqual(7.30m, saved.Items.Single(item => item.AccessoryId == "ACC_TEST").UnitPriceSnapshot, "accessory snapshot");

        var total = await service.CalculateTotalAsync(saved);
        AssertEqual(217.30m, total, "calculated total");
    }

    private static async Task UnitBuildServiceRejectsIncompatibleSwitchAsync()
    {
        var catalog = FakeCatalog.Standard();
        catalog.Switches["SW_TEST"] = catalog.Switches["SW_TEST"].WithValues(technology: "HE", mount: "HE");
        var service = new BuildService(new FakeBuildRepository(), catalog);

        var validation = await service.ValidateBuildAsync(StandardBuild());

        AssertFalse(validation.IsValid, "invalid switch build should fail");
        AssertContains(validation.Errors, "Switch technology", "switch technology error");
    }

    private static async Task UnitRequestServiceStateMachineAsync()
    {
        var build = StandardBuild();
        build.TotalCostSnapshot = 217.30m;
        var buildRepository = new FakeBuildRepository();
        await buildRepository.SaveAsync(build);

        var requestRepository = new FakeRequestRepository();
        var service = new RequestService(
            buildRepository,
            FakeSellerRepository.Standard(),
            requestRepository,
            new AlwaysValidBuildService(217.30m),
            FakeCatalog.Standard(),
            NullRealtimeNotifier.Instance);

        var request = await service.SendRequestAsync(build.BuildId, build.BuyerId, 20, "phase 6 snapshot");
        AssertEqual(RequestStatus.Pending, request.Status, "initial status");
        AssertContains(request.RequestPayloadJson, build.BuildId, "request payload build id");

        var accepted = await service.UpdateStatusAsync(request.RequestId, 20, RequestStatus.Accepted);
        AssertEqual(RequestStatus.Accepted, accepted.Status, "accepted status");

        await AssertThrowsAsync<InvalidOperationException>(
            () => service.UpdateStatusAsync(request.RequestId, 20, RequestStatus.Completed),
            "accepted cannot jump to completed");

        var inProgress = await service.UpdateStatusAsync(request.RequestId, 20, RequestStatus.In_progress);
        AssertEqual(RequestStatus.In_progress, inProgress.Status, "in progress status");

        var completed = await service.UpdateStatusAsync(request.RequestId, 20, RequestStatus.Completed);
        AssertEqual(RequestStatus.Completed, completed.Status, "completed status");
        AssertTrue(completed.CompletedAt is not null, "completed timestamp");
    }

    private static async Task UnitChatServiceParticipantsAsync()
    {
        var chatRepository = new FakeChatRepository();
        var service = new ChatService(chatRepository, FakeUserRepository.Standard(), FakeSellerRepository.Standard());

        var conversation = await service.StartBuyerConversationAsync(20, 10, "REQ_TEST");
        AssertTrue(conversation.HasValidParticipants(), "buyer conversation participant shape");

        var sent = await service.SendMessageAsync(conversation.ConversationId, 10, "hello seller");
        AssertEqual(10, sent.SenderUserId, "sender id");

        await AssertThrowsAsync<InvalidOperationException>(
            () => service.GetMessagesAsync(conversation.ConversationId, 999),
            "outsider cannot read messages");

        await AssertThrowsAsync<InvalidOperationException>(
            () => service.StartBuyerConversationAsync(21, 10, null),
            "unverified seller cannot start chat");
    }

    private static async Task UnitChatServiceAdminConversationAsync()
    {
        var chatRepository = new FakeChatRepository();
        var service = new ChatService(chatRepository, FakeUserRepository.Standard(), FakeSellerRepository.Standard());

        // T14: admin (30) opens a conversation with the verified seller (20) and exchanges a message.
        var conversation = await service.StartAdminConversationAsync(20, 30);
        AssertTrue(conversation.HasValidParticipants(), "admin conversation participant shape");
        AssertEqual(30, conversation.AdminUserId ?? -1, "admin participant set");
        AssertTrue(conversation.BuyerId is null, "admin conversation has no buyer");

        var sent = await service.SendMessageAsync(conversation.ConversationId, 30, "admin to seller");
        AssertEqual(30, sent.SenderUserId, "admin sender id");

        // The message is persisted and readable from the seller's side (DB history).
        var history = await service.GetMessagesAsync(conversation.ConversationId, 20);
        AssertContains(history.Select(message => message.MessageText), "admin to seller", "seller reads admin message");
    }

    private static async Task UnitRequestServiceRejectsUnverifiedSellerAsync()
    {
        var build = StandardBuild();
        var buildRepository = new FakeBuildRepository();
        await buildRepository.SaveAsync(build);

        var service = new RequestService(
            buildRepository,
            FakeSellerRepository.Standard(),
            new FakeRequestRepository(),
            new AlwaysValidBuildService(217.30m),
            FakeCatalog.Standard(),
            NullRealtimeNotifier.Instance);

        // Seller 21 has a profile but is_verified = false -> request must be rejected.
        await AssertThrowsAsync<InvalidOperationException>(
            () => service.SendRequestAsync(build.BuildId, build.BuyerId, 21, "to unverified seller"),
            "request to unverified seller rejected");

        // Sanity: the verified seller (20) still succeeds against the same build.
        var ok = await service.SendRequestAsync(build.BuildId, build.BuyerId, 20, "to verified seller");
        AssertEqual(RequestStatus.Pending, ok.Status, "verified seller request created");
        AssertEqual(20, ok.SellerUserId, "request targets verified seller");
    }

    private static async Task UnitRequestServiceSellerScopingAsync()
    {
        var build = StandardBuild();
        var buildRepository = new FakeBuildRepository();
        await buildRepository.SaveAsync(build);

        var service = new RequestService(
            buildRepository,
            FakeSellerRepository.Standard(),
            new FakeRequestRepository(),
            new AlwaysValidBuildService(217.30m),
            FakeCatalog.Standard(),
            NullRealtimeNotifier.Instance);

        var request = await service.SendRequestAsync(build.BuildId, build.BuyerId, 20, "scoping");

        // T08: the owning seller sees the request in their own queue.
        var ownerQueue = await service.GetSellerRequestsAsync(20);
        AssertContains(ownerQueue.Select(item => item.RequestId), request.RequestId, "owner seller sees request");

        // T09: a different seller neither sees the request nor can mutate its status.
        var otherQueue = await service.GetSellerRequestsAsync(21);
        AssertFalse(otherQueue.Any(item => item.RequestId == request.RequestId), "other seller cannot see request");
        await AssertThrowsAsync<InvalidOperationException>(
            () => service.UpdateStatusAsync(request.RequestId, 21, RequestStatus.Accepted),
            "other seller cannot update request");
    }

    private static async Task UnitRequestServiceRealtimeBestEffortAsync()
    {
        var build = StandardBuild();
        var buildRepository = new FakeBuildRepository();
        await buildRepository.SaveAsync(build);

        var notifier = new FakeRealtimeNotifier();
        var service = new RequestService(
            buildRepository,
            FakeSellerRepository.Standard(),
            new FakeRequestRepository(),
            new AlwaysValidBuildService(217.30m),
            FakeCatalog.Standard(),
            notifier);

        // Publish happens after the DB write, with the right identifiers.
        var saved = await service.SendRequestAsync(build.BuildId, build.BuyerId, 20, "realtime");
        AssertEqual(20, notifier.LastCreatedSeller ?? -1, "notifier received seller id");
        AssertEqual(saved.RequestId, notifier.LastCreatedRequestId ?? "", "notifier received request id");

        await service.UpdateStatusAsync(saved.RequestId, 20, RequestStatus.Accepted);
        AssertEqual(saved.RequestId, notifier.LastStatusRequestId ?? "", "notifier received status request id");
        AssertEqual(RequestStatus.Accepted, notifier.LastStatus ?? RequestStatus.Pending, "notifier received status");

        // A failing notifier (broker down) must NOT break the committed DB write.
        var build2 = StandardBuild();
        build2.BuildId = "BUILD_UNIT_PHASE8";
        var buildRepository2 = new FakeBuildRepository();
        await buildRepository2.SaveAsync(build2);
        var service2 = new RequestService(
            buildRepository2,
            FakeSellerRepository.Standard(),
            new FakeRequestRepository(),
            new AlwaysValidBuildService(217.30m),
            FakeCatalog.Standard(),
            new FakeRealtimeNotifier { Throw = true });

        var saved2 = await service2.SendRequestAsync(build2.BuildId, build2.BuyerId, 20, "should still save");
        AssertEqual(RequestStatus.Pending, saved2.Status, "request persisted despite notifier failure");
    }

    private static async Task UnitAccountServiceRegisterValidationAsync()
    {
        var service = new AccountService(FakeUserRepository.Standard(), new Pbkdf2PasswordHasher());

        var badEmail = await service.RegisterBuyerAsync("newbuyer", "not-an-email", "0901234567", "Password123");
        AssertFalse(badEmail.Succeeded, "invalid email rejected");
        AssertEqual(AccountOperationStatus.ValidationError, badEmail.Status, "invalid email status");
        AssertContains(badEmail.Message, "Email", "invalid email message");

        var badPhone = await service.RegisterBuyerAsync("newbuyer", "new@test.local", "12", "Password123");
        AssertFalse(badPhone.Succeeded, "invalid phone rejected");
        AssertEqual(AccountOperationStatus.ValidationError, badPhone.Status, "invalid phone status");
        AssertContains(badPhone.Message, "dien thoai", "invalid phone message");

        // Separators are stripped before validation/storage.
        var ok = await service.RegisterBuyerAsync("newbuyer", "new@test.local", "090-123 4567", "Password123");
        AssertTrue(ok.Succeeded, "valid registration succeeds");
        AssertEqual(UserRole.Buyer, ok.User!.Role, "registered as buyer");
        AssertEqual("0901234567", ok.User!.Phone, "phone normalized");
    }

    private static async Task UnitAccountServiceLoginAsync()
    {
        var users = FakeUserRepository.Standard();
        var service = new AccountService(users, new Pbkdf2PasswordHasher());

        // Register seeds a user with a real PBKDF2 hash so login exercises the real verifier (T01 login).
        var registered = await service.RegisterBuyerAsync("login_user", "login@test.local", "0905550000", "Password123");
        AssertTrue(registered.Succeeded, "registration for login test");
        var userId = registered.User!.UserId;

        // Wrong password is rejected with InvalidCredentials and establishes no session.
        var wrong = await service.LoginAsync("login_user", "WrongPass1");
        AssertFalse(wrong.Succeeded, "wrong password rejected");
        AssertEqual(AccountOperationStatus.InvalidCredentials, wrong.Status, "wrong password status");
        AssertTrue(service.CurrentUser is null, "wrong password establishes no session");

        // Correct credentials succeed and set the current session (T01).
        var good = await service.LoginAsync("login_user", "Password123");
        AssertTrue(good.Succeeded, "correct login succeeds");
        AssertEqual(UserRole.Buyer, good.User!.Role, "logged in as buyer");
        AssertEqual("login_user", service.CurrentUser?.Username ?? "", "current user set after login");

        // Clear the session first so the banned check proves a banned user cannot establish a NEW one
        // (LoginAsync rejects an inactive user before touching CurrentUser, so we start from null).
        service.Logout();
        AssertTrue(service.CurrentUser is null, "logout clears the session");

        // A banned (inactive) user cannot log in even with the right password, and no session is created (T02).
        await users.SetActiveAsync(userId, false);
        var banned = await service.LoginAsync("login_user", "Password123");
        AssertFalse(banned.Succeeded, "banned user cannot log in");
        AssertEqual(AccountOperationStatus.InactiveUser, banned.Status, "banned login status");
        AssertTrue(service.CurrentUser is null, "banned user establishes no session");
    }

    private static async Task UnitAdminServiceAuditActionsAsync()
    {
        var audit = new FakeAuditLogRepository();
        var service = new AdminService(
            FakeUserRepository.Standard(),
            FakeSellerRepository.Standard(),
            new FakeComponentRepository(),
            new FakeRequestRepository(),
            audit);

        const int adminId = 30;

        await service.SetUserActiveAsync(10, false, adminId);          // ban buyer
        await service.SetSellerVerifiedAsync(21, true, adminId);       // verify seller
        var brand = await service.SaveBrandAsync(new Brand { BrandName = "Phase 9 Brand" }, adminId); // catalog CRUD
        AssertTrue(brand.BrandId > 0, "brand saved with id");

        AssertEqual(3, audit.Entries.Count, "three audit entries written");
        AssertContains(audit.Entries.Select(entry => entry.Action), "UserBan", "ban audit action");
        AssertContains(audit.Entries.Select(entry => entry.Action), "SellerVerify", "verify audit action");
        AssertContains(audit.Entries.Select(entry => entry.Action), "BrandCreate", "brand audit action");
        AssertTrue(audit.Entries.All(entry => entry.UserId == adminId), "audit attributed to acting admin");

        // A non-admin actor cannot perform an admin action (and writes no audit).
        await AssertThrowsAsync<InvalidOperationException>(
            () => service.SetUserActiveAsync(20, false, 10),
            "non-admin cannot ban");
        AssertEqual(3, audit.Entries.Count, "rejected action writes no audit");
    }

    private static async Task IntegrationSqlBuildRequestChatAsync()
    {
        var settings = new SqlServerSettings();
        var factory = new SqlConnectionFactory(settings);
        var marker = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var buildId = $"P6_BUILD_{marker}";
        string? requestId = null;
        string? conversationId = null;

        await CleanupSqlAsync(factory);

        try
        {
            var userRepository = new SqlUserRepository(factory);
            var sellerRepository = new SqlSellerRepository(factory);
            var componentRepository = new SqlComponentRepository(factory);
            var buildRepository = new SqlBuildRepository(factory);
            var requestRepository = new SqlRequestRepository(factory);
            var chatRepository = new SqlChatRepository(factory);
            var catalog = new ComponentCatalogService(componentRepository);
            var buildService = new BuildService(buildRepository, catalog);
            var requestService = new RequestService(buildRepository, sellerRepository, requestRepository, buildService, catalog, NullRealtimeNotifier.Instance);
            var chatService = new ChatService(chatRepository, userRepository, sellerRepository);

            var buyer = await userRepository.FindByUsernameAsync("buyer_refactor")
                ?? throw new InvalidOperationException("Missing seed buyer_refactor.");
            var seller = (await sellerRepository.GetVerifiedSellersAsync()).FirstOrDefault()
                ?? throw new InvalidOperationException("Missing verified seller seed data.");
            var build = await CreateSqlBuildAsync(catalog, buyer.UserId, buildId);

            var saved = await buildService.SaveBuildAsync(build);
            AssertEqual(buildId, saved.BuildId, "SQL saved build id");
            AssertTrue(saved.Items.Count >= 3, "SQL build items saved");
            AssertTrue(saved.TotalCostSnapshot > 0, "SQL build total");

            var loaded = await buildService.GetBuildByIdAsync(buildId)
                ?? throw new InvalidOperationException("Saved build was not reloadable.");
            AssertEqual(saved.Items.Count, loaded.Items.Count, "SQL build item round trip");

            var request = await requestService.SendRequestAsync(buildId, buyer.UserId, seller.UserId, "Phase 6 integration request");
            requestId = request.RequestId;
            AssertEqual(RequestStatus.Pending, request.Status, "SQL request status");
            AssertContains(request.RequestPayloadJson, buildId, "SQL request payload");

            var conversation = await chatService.StartBuyerConversationAsync(seller.UserId, buyer.UserId, request.RequestId);
            conversationId = conversation.ConversationId;
            var message = await chatService.SendMessageAsync(conversation.ConversationId, buyer.UserId, "Phase 6 integration message");
            AssertContains(message.MessageText, "Phase 6", "SQL chat message");
        }
        finally
        {
            await CleanupSqlAsync(factory, buildId, requestId, conversationId);
        }
    }

    private static async Task IntegrationSqlSeedAccountLoginAsync()
    {
        var factory = new SqlConnectionFactory(new SqlServerSettings());
        var userRepository = new SqlUserRepository(factory);
        var accountService = new AccountService(userRepository, new Pbkdf2PasswordHasher());

        // Phase 10 handover contract: EVERY active seed account logs in with Password123.
        // (seller_unverified is active but not verified -> login still works; verification only
        //  gates request/chat targeting, not login.)
        var seedLogins = new (string Username, UserRole Role)[]
        {
            ("admin_refactor", UserRole.Admin),
            ("buyer_refactor", UserRole.Buyer),
            ("buyer_second", UserRole.Buyer),
            ("seller_soigear", UserRole.Seller),
            ("seller_keyboardlab", UserRole.Seller),
            ("seller_unverified", UserRole.Seller)
        };

        foreach (var (username, role) in seedLogins)
        {
            var login = await accountService.LoginAsync(username, "Password123");
            AssertTrue(login.Succeeded, $"seed login {username} succeeds");
            AssertEqual(role, login.User!.Role, $"seed login {username} role");
        }

        // Wrong password is rejected against a real seed account.
        var wrong = await accountService.LoginAsync("admin_refactor", "WrongPass1");
        AssertFalse(wrong.Succeeded, "seed wrong password rejected");
        AssertEqual(AccountOperationStatus.InvalidCredentials, wrong.Status, "seed wrong password status");

        // The banned seed account (buyer_inactive, is_active = 0) cannot log in even with Password123.
        var banned = await accountService.LoginAsync("buyer_inactive", "Password123");
        AssertFalse(banned.Succeeded, "banned seed account cannot log in");
        AssertEqual(AccountOperationStatus.InactiveUser, banned.Status, "banned seed account status");
    }

    private static async Task IntegrationSqlVerifyRefactorAsync()
    {
        var factory = new SqlConnectionFactory(new SqlServerSettings());
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync();

        var rowCounts = await QueryRowCountsAsync(connection);
        var expected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = 3,
            ["users"] = 7,
            ["seller_profiles"] = 3,
            ["brands"] = 13,
            ["layouts"] = 4,
            ["keyboard_kits"] = 9,
            ["switches"] = 10,
            ["keycap_sets"] = 5,
            ["stabilizers"] = 4,
            ["accessories"] = 7,
            ["builds"] = 5,
            ["build_items"] = 17,
            ["build_mods"] = 5,
            ["build_requests"] = 2,
            ["audit_log"] = 3,
            ["chat_conversations"] = 2,
            ["chat_messages"] = 4
        };

        var baselineMinimumTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "users",
            "builds",
            "build_items",
            "build_mods",
            "build_requests",
            "chat_conversations",
            "chat_messages"
        };

        foreach (var (table, expectedCount) in expected)
        {
            if (baselineMinimumTables.Contains(table))
            {
                AssertTrue(rowCounts[table] >= expectedCount, $"row count {table} should include seed baseline");
            }
            else
            {
                AssertEqual(expectedCount, rowCounts[table], $"row count {table}");
            }
        }

        await AssertZeroRowsAsync(connection, "User required fields", """
            SELECT user_id
            FROM users
            WHERE LTRIM(RTRIM(username)) = ''
               OR LTRIM(RTRIM(email)) = ''
               OR LTRIM(RTRIM(phone)) = ''
               OR LTRIM(RTRIM(password_hash)) = '';
            """);

        await AssertZeroRowsAsync(connection, "Duplicate usernames", """
            SELECT username
            FROM users
            GROUP BY username
            HAVING COUNT(*) > 1;
            """);

        await AssertZeroRowsAsync(connection, "Duplicate emails", """
            SELECT email
            FROM users
            GROUP BY email
            HAVING COUNT(*) > 1;
            """);

        await AssertZeroRowsAsync(connection, "Duplicate phones", """
            SELECT phone
            FROM users
            GROUP BY phone
            HAVING COUNT(*) > 1;
            """);

        await AssertZeroRowsAsync(connection, "Active user password hash format", """
            SELECT user_id
            FROM users
            WHERE is_active = 1
              AND password_hash NOT LIKE 'PBKDF2-SHA256$100000$%$%';
            """);

        await AssertZeroRowsAsync(connection, "Build total mismatch", """
            SELECT b.build_id
            FROM builds b
            INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
            LEFT JOIN build_items bi ON bi.build_id = b.build_id
            GROUP BY b.build_id, b.total_cost_snapshot, k.price_usd
            HAVING b.total_cost_snapshot <> CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2));
            """);

        await AssertZeroRowsAsync(connection, "Saved/Requested switch quantity mismatch", """
            SELECT b.build_id
            FROM builds b
            INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
            LEFT JOIN build_items bi ON bi.build_id = b.build_id
            WHERE b.status IN ('Saved', 'Requested')
            GROUP BY b.build_id, k.required_switch_quantity
            HAVING SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) <> k.required_switch_quantity;
            """);

        await AssertZeroRowsAsync(connection, "Build item product FK rule", """
            SELECT build_item_id
            FROM build_items
            WHERE
                (CASE WHEN switch_id IS NULL THEN 0 ELSE 1 END) +
                (CASE WHEN keycap_id IS NULL THEN 0 ELSE 1 END) +
                (CASE WHEN stab_id IS NULL THEN 0 ELSE 1 END) +
                (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) <> 1;
            """);

        await AssertZeroRowsAsync(connection, "Seller request verification rule", """
            SELECT br.request_id
            FROM build_requests br
            INNER JOIN users u ON u.user_id = br.seller_user_id
            LEFT JOIN seller_profiles sp ON sp.user_id = u.user_id
            WHERE u.is_active = 0 OR sp.is_verified = 0 OR sp.seller_profile_id IS NULL;
            """);

        await AssertZeroRowsAsync(connection, "Conversation XOR participant rule", """
            SELECT conversation_id
            FROM chat_conversations
            WHERE
                (CASE WHEN buyer_id IS NULL THEN 0 ELSE 1 END) +
                (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) <> 1;
            """);

        await AssertZeroRowsAsync(connection, "FK orphan checks", """
            SELECT fk
            FROM (
                SELECT 'users.role_id' AS fk FROM users u LEFT JOIN roles r ON r.role_id = u.role_id WHERE r.role_id IS NULL
                UNION ALL SELECT 'seller_profiles.user_id' FROM seller_profiles sp LEFT JOIN users u ON u.user_id = sp.user_id WHERE u.user_id IS NULL
                UNION ALL SELECT 'keyboard_kits.brand_id' FROM keyboard_kits k LEFT JOIN brands b ON b.brand_id = k.brand_id WHERE b.brand_id IS NULL
                UNION ALL SELECT 'keyboard_kits.layout_id' FROM keyboard_kits k LEFT JOIN layouts l ON l.layout_id = k.layout_id WHERE l.layout_id IS NULL
                UNION ALL SELECT 'switches.brand_id' FROM switches s LEFT JOIN brands b ON b.brand_id = s.brand_id WHERE b.brand_id IS NULL
                UNION ALL SELECT 'keycap_sets.brand_id' FROM keycap_sets kc LEFT JOIN brands b ON b.brand_id = kc.brand_id WHERE b.brand_id IS NULL
                UNION ALL SELECT 'stabilizers.brand_id' FROM stabilizers st LEFT JOIN brands b ON b.brand_id = st.brand_id WHERE b.brand_id IS NULL
                UNION ALL SELECT 'builds.buyer_id' FROM builds bd LEFT JOIN users u ON u.user_id = bd.buyer_id WHERE u.user_id IS NULL
                UNION ALL SELECT 'builds.kit_id' FROM builds bd LEFT JOIN keyboard_kits k ON k.kit_id = bd.kit_id WHERE k.kit_id IS NULL
                UNION ALL SELECT 'build_items.build_id' FROM build_items bi LEFT JOIN builds bd ON bd.build_id = bi.build_id WHERE bd.build_id IS NULL
                UNION ALL SELECT 'build_items.switch_id' FROM build_items bi LEFT JOIN switches s ON s.switch_id = bi.switch_id WHERE bi.switch_id IS NOT NULL AND s.switch_id IS NULL
                UNION ALL SELECT 'build_items.keycap_id' FROM build_items bi LEFT JOIN keycap_sets kc ON kc.keycap_id = bi.keycap_id WHERE bi.keycap_id IS NOT NULL AND kc.keycap_id IS NULL
                UNION ALL SELECT 'build_items.stab_id' FROM build_items bi LEFT JOIN stabilizers st ON st.stab_id = bi.stab_id WHERE bi.stab_id IS NOT NULL AND st.stab_id IS NULL
                UNION ALL SELECT 'build_items.accessory_id' FROM build_items bi LEFT JOIN accessories a ON a.accessory_id = bi.accessory_id WHERE bi.accessory_id IS NOT NULL AND a.accessory_id IS NULL
                UNION ALL SELECT 'build_mods.build_id' FROM build_mods bm LEFT JOIN builds bd ON bd.build_id = bm.build_id WHERE bd.build_id IS NULL
                UNION ALL SELECT 'build_requests.build_id' FROM build_requests br LEFT JOIN builds bd ON bd.build_id = br.build_id WHERE bd.build_id IS NULL
                UNION ALL SELECT 'build_requests.seller_user_id' FROM build_requests br LEFT JOIN users u ON u.user_id = br.seller_user_id WHERE u.user_id IS NULL
                UNION ALL SELECT 'audit_log.user_id' FROM audit_log al LEFT JOIN users u ON u.user_id = al.user_id WHERE u.user_id IS NULL
                UNION ALL SELECT 'chat_conversations.seller_user_id' FROM chat_conversations c LEFT JOIN users u ON u.user_id = c.seller_user_id WHERE u.user_id IS NULL
                UNION ALL SELECT 'chat_conversations.build_request_id' FROM chat_conversations c LEFT JOIN build_requests br ON br.request_id = c.build_request_id WHERE c.build_request_id IS NOT NULL AND br.request_id IS NULL
                UNION ALL SELECT 'chat_messages.conversation_id' FROM chat_messages m LEFT JOIN chat_conversations c ON c.conversation_id = m.conversation_id WHERE c.conversation_id IS NULL
                UNION ALL SELECT 'chat_messages.sender_user_id' FROM chat_messages m LEFT JOIN users u ON u.user_id = m.sender_user_id WHERE u.user_id IS NULL
            ) errors;
            """);

        await AssertZeroRowsAsync(connection, "Catalog price non-negative", """
            SELECT id
            FROM (
                SELECT kit_id AS id FROM keyboard_kits WHERE price_usd < 0
                UNION ALL SELECT switch_id FROM switches WHERE price_usd < 0
                UNION ALL SELECT keycap_id FROM keycap_sets WHERE price_usd < 0
                UNION ALL SELECT stab_id FROM stabilizers WHERE price_usd < 0
                UNION ALL SELECT accessory_id FROM accessories WHERE price_usd < 0
            ) negatives;
            """);

        await AssertZeroRowsAsync(connection, "Kit required switch quantity positive", """
            SELECT kit_id
            FROM keyboard_kits
            WHERE required_switch_quantity <= 0;
            """);

        await AssertZeroRowsAsync(connection, "Requested build request presence", """
            SELECT b.build_id
            FROM builds b
            WHERE b.status = 'Requested'
              AND NOT EXISTS (SELECT 1 FROM build_requests br WHERE br.build_id = b.build_id);
            """);

        await AssertZeroRowsAsync(connection, "Chat sender participant rule", """
            SELECT m.message_id
            FROM chat_messages m
            INNER JOIN chat_conversations c ON c.conversation_id = m.conversation_id
            WHERE m.sender_user_id NOT IN (
                c.seller_user_id,
                ISNULL(c.buyer_id, -1),
                ISNULL(c.admin_user_id, -1)
            );
            """);
    }

    private static KeyboardBuild StandardBuild()
    {
        return new KeyboardBuild
        {
            BuildId = "BUILD_UNIT_PHASE6",
            BuyerId = 10,
            KitId = "KIT_TEST",
            Name = "Phase 6 unit build",
            Items =
            [
                new BuildItem { SwitchId = "SW_TEST", Quantity = 70 },
                new BuildItem { KeycapId = "KC_TEST", Quantity = 1 },
                new BuildItem { StabilizerId = "ST_TEST", Quantity = 1 },
                new BuildItem { AccessoryId = "ACC_TEST", Quantity = 1 },
                new BuildItem { AccessoryId = "ACC_FOAM", Quantity = 1 }
            ],
            Mods =
            [
                new BuildMod { ModType = "Lube", TargetComponent = "Switch", Notes = "thin coat" }
            ]
        };
    }

    private static async Task<KeyboardBuild> CreateSqlBuildAsync(
        IComponentCatalogService catalog,
        int buyerId,
        string buildId)
    {
        var kits = await catalog.GetAvailableKitsAsync();
        var switches = await catalog.GetAvailableSwitchesAsync();
        var keycaps = await catalog.GetAvailableKeycapSetsAsync();
        var stabilizers = await catalog.GetAvailableStabilizersAsync();
        var accessories = await catalog.GetAvailableAccessoriesAsync();

        var kit = kits.FirstOrDefault(item =>
            switches.Any(sw => Same(sw.SwitchTechnology, item.PcbTechnology) && Same(sw.MountType, item.SwitchMount)))
            ?? throw new InvalidOperationException("Could not find a kit with a matching switch.");
        var selectedSwitch = switches.First(sw => Same(sw.SwitchTechnology, kit.PcbTechnology) && Same(sw.MountType, kit.SwitchMount));
        var keycap = keycaps.FirstOrDefault() ?? throw new InvalidOperationException("Missing keycap seed data.");
        var stabilizer = stabilizers.FirstOrDefault() ?? throw new InvalidOperationException("Missing stabilizer seed data.");
        var accessory = accessories.FirstOrDefault() ?? throw new InvalidOperationException("Missing accessory seed data.");

        return new KeyboardBuild
        {
            BuildId = buildId,
            BuyerId = buyerId,
            KitId = kit.KitId,
            Name = "Phase 6 integration build",
            Notes = "Temporary verification row; should be cleaned up.",
            Items =
            [
                new BuildItem { SwitchId = selectedSwitch.SwitchId, Quantity = kit.RequiredSwitchQuantity },
                new BuildItem { KeycapId = keycap.KeycapId, Quantity = 1 },
                new BuildItem { StabilizerId = stabilizer.StabilizerId, Quantity = 1 },
                new BuildItem { AccessoryId = accessory.AccessoryId, Quantity = 1 }
            ],
            Mods =
            [
                new BuildMod { ModType = "Lube", TargetComponent = "Switch", Notes = "integration" }
            ]
        };
    }

    private static async Task CleanupSqlAsync(
        SqlConnectionFactory factory,
        string? buildId = null,
        string? requestId = null,
        string? conversationId = null)
    {
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync();

        var buildPredicate = buildId is null ? "LIKE 'P6_BUILD_%'" : "= @build_id";
        var requestPredicate = requestId is null ? "LIKE 'REQ_%'" : "= @request_id";
        var conversationPredicate = conversationId is null ? "LIKE 'CONV_%'" : "= @conversation_id";

        await ExecuteNonQueryAsync(connection, $"""
            DELETE FROM chat_messages
            WHERE conversation_id IN (
                SELECT conversation_id
                FROM chat_conversations
                WHERE build_request_id IN (SELECT request_id FROM build_requests WHERE build_id {buildPredicate})
                   OR (@conversation_id IS NOT NULL AND conversation_id {conversationPredicate})
            );

            DELETE FROM chat_conversations
            WHERE build_request_id IN (SELECT request_id FROM build_requests WHERE build_id {buildPredicate})
               OR (@conversation_id IS NOT NULL AND conversation_id {conversationPredicate});

            DELETE FROM build_requests
            WHERE build_id {buildPredicate}
               OR (@request_id IS NOT NULL AND request_id {requestPredicate});

            DELETE FROM build_mods WHERE build_id {buildPredicate};
            DELETE FROM build_items WHERE build_id {buildPredicate};
            DELETE FROM builds WHERE build_id {buildPredicate};
            """,
            command =>
            {
                command.Parameters.AddWithValue("@build_id", (object?)buildId ?? DBNull.Value);
                command.Parameters.AddWithValue("@request_id", (object?)requestId ?? DBNull.Value);
                command.Parameters.AddWithValue("@conversation_id", (object?)conversationId ?? DBNull.Value);
            });
    }

    private static async Task<Dictionary<string, int>> QueryRowCountsAsync(SqlConnection connection)
    {
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 'roles' AS table_name, COUNT(*) AS row_count FROM roles
            UNION ALL SELECT 'users', COUNT(*) FROM users
            UNION ALL SELECT 'seller_profiles', COUNT(*) FROM seller_profiles
            UNION ALL SELECT 'brands', COUNT(*) FROM brands
            UNION ALL SELECT 'layouts', COUNT(*) FROM layouts
            UNION ALL SELECT 'keyboard_kits', COUNT(*) FROM keyboard_kits
            UNION ALL SELECT 'switches', COUNT(*) FROM switches
            UNION ALL SELECT 'keycap_sets', COUNT(*) FROM keycap_sets
            UNION ALL SELECT 'stabilizers', COUNT(*) FROM stabilizers
            UNION ALL SELECT 'accessories', COUNT(*) FROM accessories
            UNION ALL SELECT 'builds', COUNT(*) FROM builds
            UNION ALL SELECT 'build_items', COUNT(*) FROM build_items
            UNION ALL SELECT 'build_mods', COUNT(*) FROM build_mods
            UNION ALL SELECT 'build_requests', COUNT(*) FROM build_requests
            UNION ALL SELECT 'audit_log', COUNT(*) FROM audit_log
            UNION ALL SELECT 'chat_conversations', COUNT(*) FROM chat_conversations
            UNION ALL SELECT 'chat_messages', COUNT(*) FROM chat_messages;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results[reader.GetString(0)] = reader.GetInt32(1);
        }

        return results;
    }

    private static async Task AssertZeroRowsAsync(SqlConnection connection, string label, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM ({sql.Trim().TrimEnd(';')}) phase6_errors;";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        AssertEqual(0, count, label);
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        string sql,
        Action<SqlCommand>? configure = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void AssertTrue(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Expected true: {label}");
        }
    }

    private static void AssertFalse(bool condition, string label)
    {
        if (condition)
        {
            throw new InvalidOperationException($"Expected false: {label}");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void AssertContains(IEnumerable<string> values, string expectedSubstring, string label)
    {
        if (!values.Any(value => value.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{label}: expected one value containing '{expectedSubstring}'");
        }
    }

    private static void AssertContains(string value, string expectedSubstring, string label)
    {
        if (!value.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label}: expected '{value}' to contain '{expectedSubstring}'");
        }
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string label)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}: {label}");
    }
}

internal sealed class FakeCatalog : IComponentCatalogService
{
    public Dictionary<string, KeyboardKit> Kits { get; } = [];
    public Dictionary<string, KeyboardSwitch> Switches { get; } = [];
    public Dictionary<string, KeycapSet> Keycaps { get; } = [];
    public Dictionary<string, Stabilizer> Stabilizers { get; } = [];
    public Dictionary<string, Accessory> Accessories { get; } = [];
    public Dictionary<string, Layout> Layouts { get; } = [];

    public static FakeCatalog Standard()
    {
        var catalog = new FakeCatalog();
        catalog.Layouts["LAYOUT_65"] = new Layout { LayoutId = "LAYOUT_65", LayoutName = "65%", FormFactor = "65", KeyCount = 67 };
        catalog.Kits["KIT_TEST"] = new KeyboardKit
        {
            KitId = "KIT_TEST",
            BrandId = 1,
            LayoutId = "LAYOUT_65",
            KitName = "Phase 6 Kit",
            PcbTechnology = "Mechanical",
            SwitchMount = "MX 5-pin",
            RequiredSwitchQuantity = 70,
            IncludedParts = "case, pcb, plate",
            PriceUsd = 120.00m,
            IsAvailable = true
        };
        catalog.Switches["SW_TEST"] = new KeyboardSwitch
        {
            SwitchId = "SW_TEST",
            BrandId = 1,
            SwitchName = "Phase 6 Switch",
            SwitchTechnology = "Mechanical",
            MountType = "MX 5-pin",
            PriceUsd = 0.40m,
            IsAvailable = true
        };
        catalog.Keycaps["KC_TEST"] = new KeycapSet
        {
            KeycapId = "KC_TEST",
            BrandId = 1,
            KeycapName = "Phase 6 Keycap",
            SupportedFormFactor = "60/65/75/TKL",
            PriceUsd = 50.00m,
            IsAvailable = true
        };
        catalog.Stabilizers["ST_TEST"] = new Stabilizer
        {
            StabilizerId = "ST_TEST",
            BrandId = 1,
            StabilizerName = "Phase 6 Stabilizer",
            SupportedLayouts = "60/65/75/TKL",
            PriceUsd = 10.00m,
            IsAvailable = true
        };
        catalog.Accessories["ACC_TEST"] = new Accessory
        {
            AccessoryId = "ACC_TEST",
            AccessoryName = "Phase 6 Lube",
            AccessoryType = "Lube",
            TargetComponent = "Switch",
            PriceUsd = 7.30m,
            IsAvailable = true
        };
        catalog.Accessories["ACC_FOAM"] = new Accessory
        {
            AccessoryId = "ACC_FOAM",
            AccessoryName = "Phase 6 Foam",
            AccessoryType = "Foam",
            TargetComponent = "Kit",
            PriceUsd = 2.00m,
            IsAvailable = true
        };
        return catalog;
    }

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Brand>>([]);
    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Layout>>(Layouts.Values.ToList());
    public Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default) => Task.FromResult(Layouts.GetValueOrDefault(layoutId));
    public Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeyboardKit>>(Kits.Values.Where(item => item.IsAvailable).ToList());
    public Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default) => Task.FromResult(Kits.GetValueOrDefault(kitId));
    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeyboardSwitch>>(Switches.Values.Where(item => item.IsAvailable).ToList());
    public Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default) => Task.FromResult(Switches.GetValueOrDefault(switchId));
    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeycapSet>>(Keycaps.Values.Where(item => item.IsAvailable).ToList());
    public Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default) => Task.FromResult(Keycaps.GetValueOrDefault(keycapId));
    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stabilizer>>(Stabilizers.Values.Where(item => item.IsAvailable).ToList());
    public Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default) => Task.FromResult(Stabilizers.GetValueOrDefault(stabilizerId));
    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Accessory>>(Accessories.Values.Where(item => item.IsAvailable).ToList());
    public Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default) => Task.FromResult(Accessories.GetValueOrDefault(accessoryId));
}

internal sealed class FakeBuildRepository : IBuildRepository
{
    private readonly Dictionary<string, KeyboardBuild> _builds = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<KeyboardBuild>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KeyboardBuild>>(_builds.Values.Where(build => build.BuyerId == buyerId).Select(CloneBuild).ToList());

    public Task<KeyboardBuild?> GetByIdAsync(string buildId, CancellationToken cancellationToken = default)
        => Task.FromResult(_builds.TryGetValue(buildId, out var build) ? CloneBuild(build) : null);

    public Task<KeyboardBuild> SaveAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(build.BuildId))
        {
            build.BuildId = $"BUILD_UNIT_{Guid.NewGuid():N}";
        }

        var clone = CloneBuild(build);
        clone.CreatedAt = clone.CreatedAt == default ? DateTime.UtcNow : clone.CreatedAt;
        _builds[clone.BuildId] = clone;
        return Task.FromResult(CloneBuild(clone));
    }

    public Task ArchiveAsync(string buildId, CancellationToken cancellationToken = default)
    {
        if (_builds.TryGetValue(buildId, out var build))
        {
            build.Status = BuildStatus.Archived;
        }

        return Task.CompletedTask;
    }

    private static KeyboardBuild CloneBuild(KeyboardBuild build)
    {
        return new KeyboardBuild
        {
            BuildId = build.BuildId,
            BuyerId = build.BuyerId,
            KitId = build.KitId,
            Name = build.Name,
            Notes = build.Notes,
            Status = build.Status,
            TotalCostSnapshot = build.TotalCostSnapshot,
            CreatedAt = build.CreatedAt,
            UpdatedAt = build.UpdatedAt,
            Items = build.Items.Select(item => new BuildItem
            {
                BuildItemId = item.BuildItemId,
                BuildId = item.BuildId,
                SwitchId = item.SwitchId,
                KeycapId = item.KeycapId,
                StabilizerId = item.StabilizerId,
                AccessoryId = item.AccessoryId,
                Quantity = item.Quantity,
                UnitPriceSnapshot = item.UnitPriceSnapshot,
                Notes = item.Notes
            }).ToList(),
            Mods = build.Mods.Select(mod => new BuildMod
            {
                ModId = mod.ModId,
                BuildId = mod.BuildId,
                ModType = mod.ModType,
                TargetComponent = mod.TargetComponent,
                Notes = mod.Notes
            }).ToList()
        };
    }
}

internal sealed class FakeRequestRepository : IRequestRepository
{
    private readonly Dictionary<string, BuildRequest> _requests = new(StringComparer.OrdinalIgnoreCase);

    public Task<BuildRequest?> GetByIdAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult(_requests.TryGetValue(requestId, out var request) ? CloneRequest(request) : null);

    public Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BuildRequest>>(_requests.Values.Select(CloneRequest).ToList());

    public Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BuildRequest>>(_requests.Values.Where(request => request.SellerUserId == sellerUserId).Select(CloneRequest).ToList());

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_requests.Count);

    public Task<BuildRequest> SaveAsync(BuildRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            request.RequestId = $"REQ_UNIT_{Guid.NewGuid():N}";
        }

        if (request.RequestedAt == default)
        {
            request.RequestedAt = DateTime.UtcNow;
        }

        _requests[request.RequestId] = CloneRequest(request);
        return Task.FromResult(CloneRequest(request));
    }

    private static BuildRequest CloneRequest(BuildRequest request)
    {
        return new BuildRequest
        {
            RequestId = request.RequestId,
            BuildId = request.BuildId,
            SellerUserId = request.SellerUserId,
            RequestPayloadJson = request.RequestPayloadJson,
            Status = request.Status,
            Note = request.Note,
            RequestedAt = request.RequestedAt,
            AcceptedAt = request.AcceptedAt,
            CompletedAt = request.CompletedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}

internal sealed class FakeChatRepository : IChatRepository
{
    private readonly Dictionary<string, ChatConversation> _conversations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ChatMessage>> _messages = new(StringComparer.OrdinalIgnoreCase);

    public Task<ChatConversation> GetOrCreateConversationAsync(
        int sellerUserId,
        int? buyerId,
        int? adminUserId,
        string? buildRequestId,
        CancellationToken cancellationToken = default)
    {
        var existing = _conversations.Values.FirstOrDefault(item =>
            item.SellerUserId == sellerUserId
            && item.BuyerId == buyerId
            && item.AdminUserId == adminUserId
            && string.Equals(item.BuildRequestId, buildRequestId, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return Task.FromResult(CloneConversation(existing));
        }

        var conversation = new ChatConversation
        {
            ConversationId = $"CONV_UNIT_{Guid.NewGuid():N}",
            SellerUserId = sellerUserId,
            BuyerId = buyerId,
            AdminUserId = adminUserId,
            BuildRequestId = buildRequestId,
            CreatedAt = DateTime.UtcNow
        };
        _conversations[conversation.ConversationId] = CloneConversation(conversation);
        return Task.FromResult(CloneConversation(conversation));
    }

    public Task<ChatConversation?> GetConversationByIdAsync(string conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_conversations.TryGetValue(conversationId, out var conversation) ? CloneConversation(conversation) : null);

    public Task<IReadOnlyList<ChatConversation>> GetConversationsForUserAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ChatConversation>>(_conversations.Values
            .Where(item => item.SellerUserId == userId || item.BuyerId == userId || item.AdminUserId == userId)
            .Select(CloneConversation)
            .ToList());

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ChatMessage>>(_messages.GetValueOrDefault(conversationId, []).Select(CloneMessage).ToList());

    public Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            message.MessageId = $"MSG_UNIT_{Guid.NewGuid():N}";
        }

        message.SentAt = message.SentAt == default ? DateTime.UtcNow : message.SentAt;
        if (!_messages.ContainsKey(message.ConversationId))
        {
            _messages[message.ConversationId] = [];
        }

        _messages[message.ConversationId].Add(CloneMessage(message));
        return Task.FromResult(CloneMessage(message));
    }

    private static ChatConversation CloneConversation(ChatConversation conversation)
    {
        return new ChatConversation
        {
            ConversationId = conversation.ConversationId,
            SellerUserId = conversation.SellerUserId,
            BuyerId = conversation.BuyerId,
            AdminUserId = conversation.AdminUserId,
            BuildRequestId = conversation.BuildRequestId,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt
        };
    }

    private static ChatMessage CloneMessage(ChatMessage message)
    {
        return new ChatMessage
        {
            MessageId = message.MessageId,
            ConversationId = message.ConversationId,
            SenderUserId = message.SenderUserId,
            MessageText = message.MessageText,
            SentAt = message.SentAt
        };
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();

    public static FakeUserRepository Standard()
    {
        return new FakeUserRepository
        {
            _users =
            {
                [10] = new User { UserId = 10, Role = UserRole.Buyer, Username = "buyer", Email = "buyer@test.local", IsActive = true },
                [20] = new User { UserId = 20, Role = UserRole.Seller, Username = "seller", Email = "seller@test.local", IsActive = true },
                [21] = new User { UserId = 21, Role = UserRole.Seller, Username = "seller_unverified", Email = "seller2@test.local", IsActive = true },
                [30] = new User { UserId = 30, Role = UserRole.Admin, Username = "admin", Email = "admin@test.local", IsActive = true },
                [999] = new User { UserId = 999, Role = UserRole.Buyer, Username = "outsider", Email = "outsider@test.local", IsActive = true }
            }
        };
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.FirstOrDefault(user => user.Username == username));
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.FirstOrDefault(user => user.Email == email));
    public Task<User?> FindByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.FirstOrDefault(user => user.Email == emailOrUsername || user.Username == emailOrUsername));
    public Task<User?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.FirstOrDefault(user => user.Phone == phone));
    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(_users.GetValueOrDefault(userId));
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_users.Count);
    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default) { _users[user.UserId] = user; return Task.FromResult(user); }
    public Task UpdateAsync(User user, CancellationToken cancellationToken = default) { _users[user.UserId] = user; return Task.CompletedTask; }
    public Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default) { _users[userId].IsActive = isActive; return Task.CompletedTask; }
    public Task SetRoleAsync(int userId, UserRole role, CancellationToken cancellationToken = default) { _users[userId].Role = role; return Task.CompletedTask; }
}

internal sealed class FakeSellerRepository : ISellerRepository
{
    private readonly Dictionary<int, SellerProfile> _sellers = new();

    public static FakeSellerRepository Standard()
    {
        return new FakeSellerRepository
        {
            _sellers =
            {
                [20] = new SellerProfile { UserId = 20, ShopName = "Verified Shop", Phone = "090", Address = "HCM", IsVerified = true },
                [21] = new SellerProfile { UserId = 21, ShopName = "Pending Shop", Phone = "091", Address = "HN", IsVerified = false }
            }
        };
    }

    public Task<SellerProfile?> GetBySellerUserIdAsync(int sellerUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(_sellers.GetValueOrDefault(sellerUserId));

    public Task<IReadOnlyList<Custom_keyboard.Models.Admin.AdminSellerProfileRow>> GetAdminSellerProfilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Custom_keyboard.Models.Admin.AdminSellerProfileRow>>([]);

    public Task<IReadOnlyList<SellerProfile>> GetVerifiedSellersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SellerProfile>>(_sellers.Values.Where(seller => seller.IsVerified).ToList());

    public Task<int> GetSellerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_sellers.Count);
    public Task<SellerProfile> SaveAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default) { _sellers[sellerProfile.UserId] = sellerProfile; return Task.FromResult(sellerProfile); }
    public Task SetVerifiedAsync(int sellerUserId, bool isVerified, int adminUserId, CancellationToken cancellationToken = default) { _sellers[sellerUserId].IsVerified = isVerified; return Task.CompletedTask; }
}

internal sealed class AlwaysValidBuildService : IBuildService
{
    private readonly decimal _total;

    public AlwaysValidBuildService(decimal total)
    {
        _total = total;
    }

    public Task<IReadOnlyList<KeyboardBuild>> GetBuyerBuildsAsync(int buyerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeyboardBuild>>([]);
    public Task<KeyboardBuild?> GetBuildByIdAsync(string buildId, CancellationToken cancellationToken = default) => Task.FromResult<KeyboardBuild?>(null);
    public Task<KeyboardBuild> SaveBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default) => Task.FromResult(build);
    public Task ArchiveBuildAsync(string buildId, int buyerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<decimal> CalculateTotalAsync(KeyboardBuild build, CancellationToken cancellationToken = default) => Task.FromResult(_total);
    public Task<BuildValidationResult> ValidateBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
        => Task.FromResult(new BuildValidationResult { TotalCost = _total });
}

internal sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLogEntry> Entries { get; } = [];

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.AsEnumerable().Reverse().Take(take).ToList());

    public Task<AuditLogEntry> AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LogId = Entries.Count + 1;
        entry.ChangedAt = entry.ChangedAt == default ? DateTime.UtcNow : entry.ChangedAt;
        Entries.Add(entry);
        return Task.FromResult(entry);
    }
}

internal sealed class FakeComponentRepository : IComponentRepository
{
    private readonly Dictionary<int, Brand> _brands = new();
    private int _nextBrandId = 1;

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Brand>>(_brands.Values.ToList());
    public Task<Brand?> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default)
        => Task.FromResult(_brands.GetValueOrDefault(brandId));
    public Task<Brand> SaveBrandAsync(Brand brand, CancellationToken cancellationToken = default)
    {
        if (brand.BrandId <= 0)
        {
            brand.BrandId = _nextBrandId++;
        }

        _brands[brand.BrandId] = brand;
        return Task.FromResult(brand);
    }

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Layout>>([]);
    public Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default) => Task.FromResult<Layout?>(null);
    public Task<Layout> SaveLayoutAsync(Layout layout, CancellationToken cancellationToken = default) => Task.FromResult(layout);

    public Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeyboardKit>>([]);
    public Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default) => Task.FromResult<KeyboardKit?>(null);
    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeyboardSwitch>>([]);
    public Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default) => Task.FromResult<KeyboardSwitch?>(null);
    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KeycapSet>>([]);
    public Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default) => Task.FromResult<KeycapSet?>(null);
    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stabilizer>>([]);
    public Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default) => Task.FromResult<Stabilizer?>(null);
    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Accessory>>([]);
    public Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default) => Task.FromResult<Accessory?>(null);

    public Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(AdminComponentType componentType, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdminComponentRecord>>([]);
    public Task<AdminComponentRecord?> GetAdminComponentByIdAsync(AdminComponentType componentType, string componentId, CancellationToken cancellationToken = default) => Task.FromResult<AdminComponentRecord?>(null);
    public Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<AdminComponentRecord> SaveAdminComponentAsync(AdminComponentRecord component, CancellationToken cancellationToken = default) => Task.FromResult(component);
    public Task SetComponentAvailabilityAsync(AdminComponentType componentType, string componentId, bool isAvailable, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeRealtimeNotifier : IRealtimeNotifier
{
    public bool Throw { get; set; }
    public int? LastCreatedSeller { get; private set; }
    public string? LastCreatedRequestId { get; private set; }
    public string? LastStatusRequestId { get; private set; }
    public RequestStatus? LastStatus { get; private set; }

    public Task RequestCreatedAsync(int sellerUserId, string requestId, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("simulated broker failure");
        }

        LastCreatedSeller = sellerUserId;
        LastCreatedRequestId = requestId;
        return Task.CompletedTask;
    }

    public Task RequestStatusChangedAsync(string requestId, RequestStatus status, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("simulated broker failure");
        }

        LastStatusRequestId = requestId;
        LastStatus = status;
        return Task.CompletedTask;
    }
}

internal static class KeyboardSwitchTestExtensions
{
    public static KeyboardSwitch WithValues(this KeyboardSwitch source, string technology, string mount)
    {
        return new KeyboardSwitch
        {
            SwitchId = source.SwitchId,
            BrandId = source.BrandId,
            SwitchName = source.SwitchName,
            SwitchTechnology = technology,
            MountType = mount,
            SwitchType = source.SwitchType,
            ActuationForceG = source.ActuationForceG,
            PriceUsd = source.PriceUsd,
            IsAvailable = source.IsAvailable
        };
    }
}
