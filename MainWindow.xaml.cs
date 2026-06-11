using System.Windows;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Repositories.SqlServer;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Security;
using Custom_keyboard.ViewModels;

namespace Custom_keyboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var connectionFactory = new SqlConnectionFactory();
            var userRepository = new SqlUserRepository(connectionFactory);
            var sellerRepository = new SqlSellerRepository(connectionFactory);
            var componentRepository = new SqlComponentRepository(connectionFactory);
            var requestRepository = new SqlRequestRepository(connectionFactory);
            var buildRepository = new SqlBuildRepository(connectionFactory);
            var auditLogRepository = new SqlAuditLogRepository(connectionFactory);
            var passwordHasher = new Pbkdf2PasswordHasher();
            var accountService = new AccountService(userRepository, passwordHasher);
            var componentCatalogService = new ComponentCatalogService(componentRepository);
            var buildService = new BuildService(buildRepository, componentCatalogService);
            var requestService = new RequestService(
                buildRepository,
                sellerRepository,
                requestRepository,
                buildService,
                componentCatalogService);
            var adminService = new AdminService(
                userRepository,
                sellerRepository,
                componentRepository,
                requestRepository,
                auditLogRepository);

            DataContext = new MainShellViewModel(
                accountService,
                adminService,
                componentCatalogService,
                buildService,
                requestService);
        }
    }
}
