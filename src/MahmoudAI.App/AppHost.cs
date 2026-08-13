using System;
using System.IO;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Engine;
using MahmoudAI.Core.Engine.TaskGraph;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Runtime;
using MahmoudAI.Core.Security;
using MahmoudAI.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.App
{
    public static class AppHost
    {
        private static IHost? _host;

        public static void Initialize(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IWinUiContext>(new WinUiContext(dispatcherQueue));
                    services.AddSingleton<IUserApprovalService, WinUIUserApprovalService>();
                    services.AddSingleton<AdvancedPermissionBroker>();
                    services.AddSingleton<IAutomationRiskPolicy, ConservativeAutomationRiskPolicy>();
                    services.AddSingleton<IWindowsAutomationBackend>(serviceProvider =>
                        WindowsAutomationComposition.CreateGuardedBackend(
                            serviceProvider.GetRequiredService<AdvancedPermissionBroker>(),
                            serviceProvider.GetRequiredService<ILoggerFactory>(),
                            serviceProvider.GetRequiredService<IAutomationRiskPolicy>()));
                    services.AddSingleton<IUiaSemanticAutomation>(serviceProvider =>
                        WindowsAutomationComposition.CreateGuardedSemanticAutomation(
                            serviceProvider.GetRequiredService<AdvancedPermissionBroker>(),
                            serviceProvider.GetRequiredService<ILoggerFactory>(),
                            serviceProvider.GetRequiredService<IAutomationRiskPolicy>()));
                    services.AddSingleton<IScreenCaptureBackend>(serviceProvider =>
                        WindowsAutomationComposition.CreateGuardedScreenCaptureBackend(
                            serviceProvider.GetRequiredService<AdvancedPermissionBroker>(),
                            serviceProvider.GetRequiredService<IAutomationRiskPolicy>()));
                    services.AddSingleton<WindowsAutomationEngine>();
                    services.AddSingleton<IScreenPrivacyFilter, DefaultScreenPrivacyFilter>();
                    services.AddSingleton<IOcrEngine, WindowsMediaOcrEngine>();
                    services.AddSingleton<OcrPipeline>(sp => new OcrPipeline(
                        sp.GetRequiredService<IOcrEngine>(),
                        null,
                        sp.GetRequiredService<ILogger<OcrPipeline>>()));
                    services.AddSingleton<ScreenFusionEngine>();
                    services.AddSingleton<IScreenObservationService, ScreenObservationService>();
                    services.AddSingleton<PersonaStateMachine>();
                    services.AddSingleton<AiProviderClient>();

                    services.AddSingleton<MissionEventHub>();
                    services.AddSingleton<SqliteMissionStore>(serviceProvider =>
                    {
                        var dataDirectory = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "MahmoudAI");
                        Directory.CreateDirectory(dataDirectory);
                        var databasePath = Path.Combine(dataDirectory, "mahmoud-ai.db");
                        return new SqliteMissionStore(
                            databasePath,
                            serviceProvider.GetRequiredService<ILogger<SqliteMissionStore>>());
                    });
                    services.AddSingleton<IMissionEventSink>(serviceProvider =>
                    {
                        var durableSink = new SqliteMissionEventSink(
                            serviceProvider.GetRequiredService<SqliteMissionStore>());
                        var eventHub = serviceProvider.GetRequiredService<MissionEventHub>();
                        return new CompositeMissionEventSink(new IMissionEventSink[]
                        {
                            durableSink,
                            eventHub
                        });
                    });
                    services.AddSingleton<TaskExecutor>();
                    services.AddSingleton<ITaskExecutor>(serviceProvider =>
                        serviceProvider.GetRequiredService<TaskExecutor>());
                    services.AddSingleton<TaskGraphScheduler>();

                    // Retained for legacy callers while the WinUI mission path uses TaskGraph V2.
                    services.AddSingleton<TaskGraphEngine>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        public static T GetRequiredService<T>() where T : notnull
        {
            if (_host is null)
            {
                throw new InvalidOperationException("AppHost must be initialized from the WinUI UI thread before resolving services.");
            }

            return _host.Services.GetRequiredService<T>();
        }
    }
}
