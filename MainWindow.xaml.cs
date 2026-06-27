using System.Windows;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Realtime;
using Custom_keyboard.Realtime.Devices;
using Custom_keyboard.Repositories.SqlServer;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Devices;
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
            var chatRepository = new SqlChatRepository(connectionFactory);
            var statsRepository = new SqlStatsRepository(connectionFactory);
            var sellerApplicationRepository = new SqlSellerApplicationRepository(connectionFactory);
            var passwordHasher = new Pbkdf2PasswordHasher();
            var mqttSettings = new MqttSettings();
            var realtimeService = new MqttRealtimeService(mqttSettings);
            var accountService = new AccountService(userRepository, passwordHasher);
            var componentCatalogService = new ComponentCatalogService(componentRepository);
            var buildService = new BuildService(buildRepository, componentCatalogService);

            // Device QC layer (telemetry transport + service + simulator).
            var deviceRepository = new SqlDeviceRepository(connectionFactory);
            var deviceSessionRepository = new SqlDeviceTestSessionRepository(connectionFactory);
            var deviceKeyResultRepository = new SqlDeviceKeyTestResultRepository(connectionFactory);
            var deviceService = new DeviceService(
                deviceRepository,
                deviceSessionRepository,
                deviceKeyResultRepository);

            var requestService = new RequestService(
                buildRepository,
                sellerRepository,
                requestRepository,
                buildService,
                componentCatalogService,
                deviceService,
                realtimeService);
            var adminService = new AdminService(
                userRepository,
                sellerRepository,
                componentRepository,
                requestRepository,
                auditLogRepository);
            var chatService = new ChatService(chatRepository, userRepository, sellerRepository);
            var statsService = new StatsService(statsRepository, userRepository, sellerRepository);
            var sellerApplicationService = new SellerApplicationService(
                userRepository,
                sellerRepository,
                sellerApplicationRepository,
                auditLogRepository);

            // MQTT is the main transport; when it is off we use a Null publisher so the simulator
            // records QC results directly via DeviceService (no broker, no data loss).
            IDeviceTelemetryPublisher deviceTelemetryPublisher = mqttSettings.Enabled
                ? new MqttDeviceTelemetryPublisher(mqttSettings)
                : new NullDeviceTelemetryPublisher();

            var deviceSimulator = new DeviceSimulator(deviceService, deviceTelemetryPublisher);

            // The MQTT path needs a subscriber to persist what the publisher emits; without it, the
            // published telemetry would have no consumer. Best-effort start; null when MQTT is disabled.
            IDeviceTelemetrySubscriber? deviceTelemetrySubscriber = mqttSettings.Enabled
                ? new MqttDeviceTelemetrySubscriber(mqttSettings, deviceService)
                : null;
            _ = deviceTelemetrySubscriber?.StartAsync();

            DataContext = new MainShellViewModel(
                accountService,
                adminService,
                componentCatalogService,
                buildService,
                requestService,
                chatService,
                statsService,
                sellerApplicationService,
                realtimeService,
                deviceService,
                deviceSimulator);

            // Tear the realtime + telemetry clients down when the shell closes (best-effort).
            Closed += async (_, _) =>
            {
                if (deviceTelemetrySubscriber is not null)
                {
                    await deviceTelemetrySubscriber.StopAsync();
                }

                if (deviceTelemetrySubscriber is IAsyncDisposable subscriberDisposable)
                {
                    await subscriberDisposable.DisposeAsync();
                }

                if (deviceTelemetryPublisher is IAsyncDisposable publisherDisposable)
                {
                    await publisherDisposable.DisposeAsync();
                }

                await realtimeService.DisposeAsync();
            };
        }
    }
}
