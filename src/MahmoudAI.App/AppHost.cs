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

        public static void Initialize()
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
                    services.AddSingleton<IUserApprovalService>(sp => 
                        new WinUIUserApprovalService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), () => null));
                    services.AddSingleton<AdvancedPermissionBroker>(sp => 
                        new AdvancedPermissionBroker(sp.GetRequiredService<ILogger<AdvancedPermissionBroker>>(), sp.GetRequiredService<IUserApprovalService>()));
                    services.AddSingleton<TaskGraphEngine>();
                    services.AddSingleton<PersonaStateMachine>();
                    services.AddSingleton<AiProviderClient>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }

        public static T GetRequiredService<T>() where T : notnull
        {
            if (_host == null)
            {
                Initialize();
            }
            return _host!.Services.GetRequiredService<T>();
        }
    }
}
