using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MahmoudAI.Core.Engine;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Runtime;
using MahmoudAI.Core.Security;

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
                    services.AddSingleton<TaskGraphEngine>();
                    services.AddSingleton<PersonaStateMachine>();
                    services.AddSingleton<AiProviderClient>();
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
