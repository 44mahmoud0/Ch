using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Security;
using MahmoudAI.Core.Runtime;

namespace MahmoudAI.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly PersonaStateMachine _persona;
        private readonly PermissionBroker _permissions;
        private readonly HealthMonitor _health;

        public MainWindow()
        {
            this.InitializeComponent();
            _persona = new PersonaStateMachine(Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonaStateMachine>.Instance);
            _permissions = new PermissionBroker(Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionBroker>.Instance);
            _health = new HealthMonitor(Microsoft.Extensions.Logging.Abstractions.NullLogger<HealthMonitor>.Instance);
            
            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
        }
    }
}
