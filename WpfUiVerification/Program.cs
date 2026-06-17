using System.Diagnostics;
using System.IO;
using System.Windows.Automation;

var runner = new WpfUiRunner();
await runner.RunAsync(args);

internal sealed class WpfUiRunner
{
    private readonly List<string> _failures = [];
    private int _total;

    public async Task RunAsync(string[] args)
    {
        var appPath = ResolveAppPath(args);
        Console.WriteLine("WPF UI verification started.");
        Console.WriteLine($"App: {appPath}");

        await RunScenarioAsync("UI login smoke: buyer dashboard", appPath, "buyer_refactor", "Password123", "BuyerDashboardRoot");
        await RunScenarioAsync("UI login smoke: seller dashboard", appPath, "seller_soigear", "Password123", "SellerDashboardRoot");
        await RunScenarioAsync("UI login smoke: admin dashboard", appPath, "admin_refactor", "Password123", "AdminDashboardRoot");
        await RunLanguageSwitchScenarioAsync("UI i18n: live language switch + persistence", appPath);
        await RunSellerAnalyticsScenarioAsync("UI flow: seller analytics + chart localization (UC-02.3.1)", appPath);
        await RunAdminTabsScenarioAsync("UI flow: admin tab navigation renders (UC-03.4.x)", appPath);
        await RunSellerSnapshotScenarioAsync("UI flow: seller request snapshot renders (UC-02.3.2)", appPath);
        await RunDeviceQcScenarioAsync("UI flow: seller runs QC and buyer sees summary (Device Layer Phase 7)", appPath);

        Console.WriteLine();
        Console.WriteLine($"Passed: {_total - _failures.Count}");
        Console.WriteLine($"Failed: {_failures.Count}");

        if (_failures.Count > 0)
        {
            foreach (var failure in _failures)
            {
                Console.WriteLine($"FAIL {failure}");
            }

            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("WPF UI verification passed.");
    }

    private async Task RunScenarioAsync(
        string name,
        string appPath,
        string username,
        string password,
        string expectedDashboardAutomationId)
    {
        _total++;
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath) ?? Environment.CurrentDirectory,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Could not start WPF app.");

            var window = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(20));
            SetValue(window, "LoginEmailOrUsernameTextBox", username);
            SetValue(window, "LoginPasswordBox", password);
            Invoke(window, "LoginSubmitButton");

            WaitForDashboard(window, expectedDashboardAutomationId, username, TimeSpan.FromSeconds(20));
            OpenProfileMenu(process.Id, window, TimeSpan.FromSeconds(10));
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                await Task.Delay(500);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
    }

    private async Task RunLanguageSwitchScenarioAsync(string name, string appPath)
    {
        _total++;
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath) ?? Environment.CurrentDirectory,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Could not start WPF app.");

            var window = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(20));

            // 1) Default language on the login screen should be Vietnamese.
            var loginBtn = WaitForElement(window, "LoginSubmitButton", TimeSpan.FromSeconds(10));
            var loginText = loginBtn.Current.Name?.Trim() ?? string.Empty;
            Console.WriteLine($"[i18n] login button (default): '{loginText}'");

            SetValue(window, "LoginEmailOrUsernameTextBox", "admin_refactor");
            SetValue(window, "LoginPasswordBox", "Password123");
            Invoke(window, "LoginSubmitButton");
            WaitForDashboard(window, "AdminDashboardRoot", "admin_refactor", TimeSpan.FromSeconds(20));

            // Dashboard title is a ViewModel-computed Tr() property: a strong live-refresh signal.
            const string titleVi = "Bảng điều khiển Admin";
            const string titleEn = "Admin dashboard";
            var titleViBefore = FindByName(window, titleVi) is not null;
            var titleEnBefore = FindByName(window, titleEn) is not null;
            Console.WriteLine($"[i18n] dashboard title before switch -> VI:{titleViBefore} EN:{titleEnBefore}");

            // 2) Open the user menu, expand Preferences, switch to English; title must change live.
            InvokeOrToggle(WaitForElement(window, "UserMenuButton", TimeSpan.FromSeconds(10)), "UserMenuButton");
            InvokeOrToggle(WaitForElementInProcess(process.Id, "UserMenuPreferencesButton", TimeSpan.FromSeconds(10)), "UserMenuPreferencesButton");
            InvokeOrToggle(WaitForElementInProcess(process.Id, "LanguageEnglishOption", TimeSpan.FromSeconds(10)), "LanguageEnglishOption");
            Thread.Sleep(800);
            var titleViAfterEn = FindByName(window, titleVi) is not null;
            var titleEnAfterEn = FindByName(window, titleEn) is not null;
            Console.WriteLine($"[i18n] dashboard title after EN -> VI:{titleViAfterEn} EN:{titleEnAfterEn}");

            var prefPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CustomKeyboard", "preferences.txt");
            var prefAfterEn = File.Exists(prefPath) ? File.ReadAllText(prefPath).Trim() : "(missing)";
            Console.WriteLine($"[i18n] preferences.txt after EN: '{prefAfterEn}'");

            // 3) Switch back to Vietnamese; title and persistence must revert.
            InvokeOrToggle(WaitForElementInProcess(process.Id, "LanguageVietnameseOption", TimeSpan.FromSeconds(10)), "LanguageVietnameseOption");
            Thread.Sleep(800);
            var titleViAfterVi = FindByName(window, titleVi) is not null;
            var prefAfterVi = File.Exists(prefPath) ? File.ReadAllText(prefPath).Trim() : "(missing)";
            Console.WriteLine($"[i18n] dashboard title after back-to-VI -> VI:{titleViAfterVi}");
            Console.WriteLine($"[i18n] preferences.txt after VI: '{prefAfterVi}'");

            var problems = new List<string>();
            if (loginText.Contains("Sign in", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("login screen defaulted to English, expected Vietnamese");
            }
            if (!titleViBefore)
            {
                problems.Add("dashboard title was not Vietnamese by default");
            }
            if (!titleEnAfterEn || titleViAfterEn)
            {
                problems.Add("dashboard title did not switch to English live");
            }
            if (prefAfterEn != "English")
            {
                problems.Add($"preferences.txt not persisted as English (got '{prefAfterEn}')");
            }
            if (!titleViAfterVi)
            {
                problems.Add("dashboard title did not revert to Vietnamese");
            }
            if (prefAfterVi != "Vietnamese")
            {
                problems.Add($"preferences.txt not persisted back to Vietnamese (got '{prefAfterVi}')");
            }

            if (problems.Count == 0)
            {
                Console.WriteLine($"PASS {name}");
            }
            else
            {
                _failures.Add($"{name}: {string.Join("; ", problems)}");
            }
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                await Task.Delay(500);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
    }

    // UC-02.3.1: seller opens the Analytics tab; charts (localized series) must render and survive a
    // live language switch (exercises ChartFactory + the OnLanguageChangedCore chart rebuild).
    private async Task RunSellerAnalyticsScenarioAsync(string name, string appPath)
    {
        _total++;
        Process? process = null;
        try
        {
            process = StartApp(appPath);
            var window = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(20));
            LoginAndWaitDashboard(window, "seller_soigear", "SellerDashboardRoot");

            ClickNav(window, "NavAnalytics", "Phân tích");
            Thread.Sleep(800);
            var viAnalytics = FindByName(window, "Doanh thu & số đơn") is not null
                              || FindByName(window, "Tỉ lệ trạng thái") is not null;
            Console.WriteLine($"[seller-analytics] VI analytics labels visible: {viAnalytics}");

            SwitchLanguage(process.Id, window, "LanguageEnglishOption");
            Thread.Sleep(900);
            var alive = !process.HasExited && FindByAutomationId(window, "SellerDashboardRoot") is not null;
            var enAnalytics = FindByName(window, "Revenue & orders") is not null
                              || FindByName(window, "Status breakdown") is not null;
            Console.WriteLine($"[seller-analytics] after EN switch -> alive:{alive} EN labels:{enAnalytics}");

            SwitchLanguage(process.Id, window, "LanguageVietnameseOption");
            Thread.Sleep(300);

            var problems = new List<string>();
            if (!viAnalytics) problems.Add("Vietnamese analytics labels did not render");
            if (!alive) problems.Add("app crashed after switching language on the analytics tab (chart rebuild)");
            if (!enAnalytics) problems.Add("analytics labels did not localize to English after switch");
            Report(name, problems);
        }
        catch (Exception ex) { _failures.Add($"{name}: {ex.Message}"); }
        finally { await CloseAppAsync(process); }
    }

    // UC-03.4.x: admin navigates every sidebar tab; each must render without a binding crash, and a
    // language switch on the chart-heavy Overview tab must not bring the app down.
    private async Task RunAdminTabsScenarioAsync(string name, string appPath)
    {
        _total++;
        Process? process = null;
        try
        {
            process = StartApp(appPath);
            var window = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(20));
            LoginAndWaitDashboard(window, "admin_refactor", "AdminDashboardRoot");

            var tabs = new (string Id, string Fallback)[]
            {
                ("NavUser", "Người dùng"),
                ("NavSeller", "Seller"),
                ("NavApplications", "Đơn xin Seller"),
                ("NavBrand", "Brand & Layout"),
                ("NavComponent", "Linh kiện"),
                ("NavAudit", "Audit log"),
                ("NavOverview", "Tổng quan"),
            };

            var problems = new List<string>();
            foreach (var (id, fallback) in tabs)
            {
                ClickNav(window, id, fallback);
                Thread.Sleep(500);
                if (process.HasExited || FindByAutomationId(window, "AdminDashboardRoot") is null)
                {
                    problems.Add($"crash/blank after opening tab {id}");
                    break;
                }

                Console.WriteLine($"[admin-tabs] {id} rendered");
            }

            if (!process.HasExited)
            {
                SwitchLanguage(process.Id, window, "LanguageEnglishOption");
                Thread.Sleep(900);
                var alive = !process.HasExited && FindByAutomationId(window, "AdminDashboardRoot") is not null;
                var enOverview = FindByName(window, "Platform revenue (USD)") is not null
                                 || FindByName(window, "Revenue over time") is not null;
                Console.WriteLine($"[admin-tabs] after EN switch on overview -> alive:{alive} EN:{enOverview}");
                if (!alive) problems.Add("app crashed switching language on admin overview (chart rebuild)");
                SwitchLanguage(process.Id, window, "LanguageVietnameseOption");
                Thread.Sleep(300);
            }

            Report(name, problems);
        }
        catch (Exception ex) { _failures.Add($"{name}: {ex.Message}"); }
        finally { await CloseAppAsync(process); }
    }

    // UC-02.3.2: seller selects an assigned request; the build snapshot (localized formatter) must
    // render without crashing.
    private async Task RunSellerSnapshotScenarioAsync(string name, string appPath)
    {
        _total++;
        Process? process = null;
        try
        {
            process = StartApp(appPath);
            var window = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(20));
            LoginAndWaitDashboard(window, "seller_soigear", "SellerDashboardRoot");

            var grid = WaitForElement(window, "SellerRequestsGrid", TimeSpan.FromSeconds(10));
            var firstRow = grid.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));
            if (firstRow is null)
            {
                Console.WriteLine($"[seller-snapshot] no requests assigned; snapshot path not exercised");
                Console.WriteLine($"PASS {name} (no data)");
                return;
            }

            if (firstRow.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sel))
            {
                ((SelectionItemPattern)sel).Select();
            }

            Thread.Sleep(800);
            var alive = !process.HasExited && FindByAutomationId(window, "SellerDashboardRoot") is not null;
            var snapshotShown = FindByNameContains(window, "Kit") is not null
                                || FindByNameContains(window, "Tổng giá") is not null
                                || FindByNameContains(window, "Build") is not null;
            Console.WriteLine($"[seller-snapshot] alive:{alive} snapshot text present:{snapshotShown}");

            var problems = new List<string>();
            if (!alive) problems.Add("app crashed rendering the build snapshot");
            if (!snapshotShown) problems.Add("snapshot text did not render after selecting a request");
            Report(name, problems);
        }
        catch (Exception ex) { _failures.Add($"{name}: {ex.Message}"); }
        finally { await CloseAppAsync(process); }
    }

    private async Task RunDeviceQcScenarioAsync(string name, string appPath)
    {
        _total++;
        Process? sellerProcess = null;
        Process? buyerProcess = null;
        try
        {
            sellerProcess = StartApp(appPath);
            var sellerWindow = WaitForMainWindow(sellerProcess.Id, TimeSpan.FromSeconds(20));
            LoginAndWaitDashboard(sellerWindow, "seller_soigear", "SellerDashboardRoot");

            var requestsGrid = WaitForElement(sellerWindow, "SellerRequestsGrid", TimeSpan.FromSeconds(10));
            SelectDataGridRowByText(requestsGrid, "REQ_REF_BOOG75_PENDING");

            Thread.Sleep(500);
            InvokeIfEnabled(sellerWindow, "SellerAcceptButton");
            Thread.Sleep(900);
            InvokeIfEnabled(sellerWindow, "SellerInProgressButton");
            Thread.Sleep(900);

            ClickNav(sellerWindow, "NavQc", "Kiểm tra QC");
            Thread.Sleep(500);
            var startButton = WaitForElement(sellerWindow, "QcStartButton", TimeSpan.FromSeconds(10));
            if (!startButton.Current.IsEnabled)
            {
                throw new InvalidOperationException("QC start button is disabled; selected request is not In_progress.");
            }

            InvokeOrToggle(startButton, "QcStartButton");
            var qcRow = WaitForDataGridRow(sellerWindow, "QcKeyResultsGrid", TimeSpan.FromSeconds(45));
            var sellerSummary = WaitForElement(sellerWindow, "SellerQcSummaryTitle", TimeSpan.FromSeconds(10));
            Console.WriteLine($"[device-qc] seller row:{qcRow is not null} summary:{sellerSummary is not null}");

            await CloseAppAsync(sellerProcess);
            sellerProcess = null;

            buyerProcess = StartApp(appPath);
            var buyerWindow = WaitForMainWindow(buyerProcess.Id, TimeSpan.FromSeconds(20));
            LoginAndWaitDashboard(buyerWindow, "buyer_refactor", "BuyerDashboardRoot");
            ClickNav(buyerWindow, "BuyerSentRequestsNavButton", "Request đã gửi");

            var buyerGrid = WaitForElement(buyerWindow, "BuyerRequestsGrid", TimeSpan.FromSeconds(10));
            SelectDataGridRowByText(buyerGrid, "REQ_REF_BOOG75_PENDING");

            var buyerSummary = WaitForElement(buyerWindow, "BuyerQcSummaryStatusLabel", TimeSpan.FromSeconds(15));
            Console.WriteLine($"[device-qc] buyer summary:{buyerSummary is not null}");
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
        }
        finally
        {
            await CloseAppAsync(sellerProcess);
            await CloseAppAsync(buyerProcess);
        }
    }

    private void Report(string name, List<string> problems)
    {
        if (problems.Count == 0)
        {
            Console.WriteLine($"PASS {name}");
        }
        else
        {
            _failures.Add($"{name}: {string.Join("; ", problems)}");
        }
    }

    private static Process StartApp(string appPath)
        => Process.Start(new ProcessStartInfo
        {
            FileName = appPath,
            WorkingDirectory = Path.GetDirectoryName(appPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Could not start WPF app.");

    private static async Task CloseAppAsync(Process? process)
    {
        if (process is not null && !process.HasExited)
        {
            process.CloseMainWindow();
            await Task.Delay(500);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private void LoginAndWaitDashboard(AutomationElement window, string username, string dashboardAutomationId)
    {
        SetValue(window, "LoginEmailOrUsernameTextBox", username);
        SetValue(window, "LoginPasswordBox", "Password123");
        Invoke(window, "LoginSubmitButton");
        WaitForDashboard(window, dashboardAutomationId, username, TimeSpan.FromSeconds(20));
    }

    private void ClickNav(AutomationElement window, string automationId, string fallbackName)
    {
        var element = FindByAutomationId(window, automationId)
                      ?? (string.IsNullOrEmpty(fallbackName) ? null : FindByName(window, fallbackName));
        if (element is null)
        {
            throw new InvalidOperationException($"Nav element '{automationId}'/'{fallbackName}' not found.");
        }

        InvokeOrToggle(element, automationId);
    }

    private static void InvokeIfEnabled(AutomationElement root, string automationId)
    {
        var element = FindByAutomationId(root, automationId);
        if (element is null || !element.Current.IsEnabled)
        {
            return;
        }

        InvokeOrToggle(element, automationId);
    }

    private static AutomationElement WaitForDataGridRow(AutomationElement root, string gridAutomationId, TimeSpan timeout)
    {
        var grid = WaitForElement(root, gridAutomationId, timeout);
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var row = grid.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));
            if (row is not null)
            {
                return row;
            }

            Thread.Sleep(300);
        }

        throw new TimeoutException($"Timed out waiting for rows in '{gridAutomationId}'.");
    }

    private static AutomationElement SelectDataGridRowByText(AutomationElement grid, string text)
    {
        var rows = grid.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));

        foreach (AutomationElement row in rows)
        {
            var rowName = row.Current.Name ?? string.Empty;
            if (rowName.Contains(text, StringComparison.OrdinalIgnoreCase) || FindByNameContains(row, text) is not null)
            {
                if (row.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
                {
                    ((SelectionItemPattern)selection).Select();
                    return row;
                }
            }
        }

        throw new InvalidOperationException($"Could not find row containing '{text}'.");
    }

    private void SwitchLanguage(int processId, AutomationElement window, string optionAutomationId)
    {
        // Make sure the user menu + preferences panel are open, then pick the language radio button.
        if (FindByAutomationIdInProcess(processId, optionAutomationId) is null)
        {
            InvokeOrToggle(WaitForElement(window, "UserMenuButton", TimeSpan.FromSeconds(10)), "UserMenuButton");
            InvokeOrToggle(WaitForElementInProcess(processId, "UserMenuPreferencesButton", TimeSpan.FromSeconds(10)), "UserMenuPreferencesButton");
        }

        InvokeOrToggle(WaitForElementInProcess(processId, optionAutomationId, TimeSpan.FromSeconds(10)), optionAutomationId);
    }

    private static AutomationElement? FindByNameContains(AutomationElement root, string substring)
    {
        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement element in all)
        {
            var elementName = element.Current.Name;
            if (!string.IsNullOrEmpty(elementName) &&
                elementName.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }

    private static string ResolveAppPath(string[] args)
    {
        var explicitPath = args.FirstOrDefault(arg => arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase));
        if (explicitPath is not null)
        {
            var value = explicitPath["--app=".Length..].Trim('"');
            return RequireFile(Path.GetFullPath(value));
        }

        var fromEnv = Environment.GetEnvironmentVariable("CUSTOM_KEYBOARD_APP_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return RequireFile(Path.GetFullPath(fromEnv));
        }

        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Custom_keyboard.csproj")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Cannot locate Custom_keyboard.csproj. Pass --app=<path-to-exe>.");
        }

        return RequireFile(Path.Combine(
            current.FullName,
            "bin",
            "Debug",
            "net10.0-windows",
            "Custom_keyboard.exe"));
    }

    private static string RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"App executable not found: {path}");
        }

        return path;
    }

    private static AutomationElement WaitForMainWindow(int processId, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var window = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ProcessIdProperty, processId),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)));

            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Timed out waiting for main window.");
    }

    private static AutomationElement WaitForElement(AutomationElement root, string automationId, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var element = FindByAutomationId(root, automationId);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for element '{automationId}'.");
    }

    private static AutomationElement WaitForElementInProcess(int processId, string automationId, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var element = FindByAutomationIdInProcess(processId, automationId);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for process element '{automationId}'.");
    }

    private static void WaitForDashboard(
        AutomationElement root,
        string dashboardAutomationId,
        string username,
        TimeSpan timeout)
    {
        var title = dashboardAutomationId switch
        {
            "BuyerDashboardRoot" => "Buyer dashboard",
            "SellerDashboardRoot" => "Seller dashboard",
            "AdminDashboardRoot" => "Admin dashboard",
            _ => string.Empty
        };

        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            if (FindByAutomationId(root, dashboardAutomationId) is not null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(title) && FindByName(root, title) is not null)
            {
                return;
            }

            var loginError = FindByAutomationId(root, "LoginErrorMessage");
            var errorText = loginError?.Current.Name;
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                throw new InvalidOperationException($"Login failed for '{username}': {errorText}");
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Timed out waiting for dashboard '{dashboardAutomationId}'. Visible elements: {DescribeVisibleElements(root)}");
    }

    private static AutomationElement? FindByAutomationId(AutomationElement root, string automationId)
        => root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

    private static AutomationElement? FindByAutomationIdInProcess(int processId, string automationId)
    {
        var roots = AutomationElement.RootElement.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, processId));

        foreach (AutomationElement root in roots)
        {
            if (root.Current.AutomationId == automationId)
            {
                return root;
            }

            var found = FindByAutomationId(root, automationId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static AutomationElement? FindByName(AutomationElement root, string name)
        => root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));

    private static string DescribeVisibleElements(AutomationElement root)
    {
        var walker = TreeWalker.ControlViewWalker;
        var items = new List<string>();
        Collect(root, walker, items, depth: 0);
        return string.Join(" | ", items.Take(40));
    }

    private static void Collect(AutomationElement element, TreeWalker walker, List<string> items, int depth)
    {
        if (depth > 4 || items.Count >= 40)
        {
            return;
        }

        var automationId = element.Current.AutomationId;
        var name = element.Current.Name;
        var controlType = element.Current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty);
        if (!string.IsNullOrWhiteSpace(automationId) || !string.IsNullOrWhiteSpace(name))
        {
            items.Add($"{controlType}:{automationId}:{name}");
        }

        var child = walker.GetFirstChild(element);
        while (child is not null && items.Count < 40)
        {
            Collect(child, walker, items, depth + 1);
            child = walker.GetNextSibling(child);
        }
    }

    private static void SetValue(AutomationElement root, string automationId, string value)
    {
        var element = WaitForElement(root, automationId, TimeSpan.FromSeconds(10));
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
        {
            throw new InvalidOperationException($"Element '{automationId}' does not support ValuePattern.");
        }

        ((ValuePattern)pattern).SetValue(value);
    }

    private static void Invoke(AutomationElement root, string automationId)
    {
        var element = WaitForElement(root, automationId, TimeSpan.FromSeconds(10));
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
        {
            throw new InvalidOperationException($"Element '{automationId}' does not support InvokePattern.");
        }

        ((InvokePattern)pattern).Invoke();
    }

    private static void OpenProfileMenu(int processId, AutomationElement window, TimeSpan timeout)
    {
        var menuButton = WaitForElement(window, "UserMenuButton", timeout);
        InvokeOrToggle(menuButton, "UserMenuButton");

        var profileButton = WaitForElementInProcess(processId, "UserMenuProfileButton", timeout);
        InvokeOrToggle(profileButton, "UserMenuProfileButton");

        _ = WaitForElementInProcess(processId, "UserMenuProfileDetails", timeout);
    }

    private static void InvokeOrToggle(AutomationElement element, string automationId)
    {
        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            return;
        }

        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var togglePattern))
        {
            ((TogglePattern)togglePattern).Toggle();
            return;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        throw new InvalidOperationException($"Element '{automationId}' supports neither Invoke, Toggle, nor SelectionItem.");
    }
}
