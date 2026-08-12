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
        private readonly AdvancedPermissionBroker _permissions;
        private readonly TaskGraphEngine _taskGraph;

        public MainWindow()
        {
            this.InitializeComponent();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            _persona = new PersonaStateMachine(loggerFactory.CreateLogger<PersonaStateMachine>());
            _permissions = new AdvancedPermissionBroker(loggerFactory.CreateLogger<AdvancedPermissionBroker>());
            _taskGraph = new TaskGraphEngine(loggerFactory.CreateLogger<TaskGraphEngine>());

            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
            MissionOutputBox.Text = "[System] Mahmoud AI Desktop initialized successfully.\n[Security] AdvancedPermissionBroker and WorkspaceIsolation active.\n";
        }

        private async void RunMissionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string objective = MissionInputBox.Text;
            if (string.IsNullOrWhiteSpace(objective)) return;

            MissionOutputBox.Text += $"\n[Mission Started] Objective: {objective}\n";
            StatusTextBlock.Text = "Status: Executing Mission...";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);

            var tasks = new List<MissionTask>
            {
                new MissionTask { Id = "t1", Name = "Planner Analysis", Action = async ct => { await Task.Delay(500, ct); return true; } },
                new MissionTask { Id = "t2", Name = "Execution & Verification", Dependencies = { "t1" }, Action = async ct => { await Task.Delay(500, ct); return true; } }
            };

            bool success = await _taskGraph.ExecuteGraphAsync(tasks, CancellationToken.None);

            if (success)
            {
                MissionOutputBox.Text += "[Mission Completed] All tasks executed successfully.\n";
                StatusTextBlock.Text = "Status: Idle (Secure)";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                MissionOutputBox.Text += "[Mission Failed] Execution halted or cancelled.\n";
                StatusTextBlock.Text = "Status: Failed / Stopped";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private void EmergencyStopButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _permissions.TriggerEmergencyStop();
            StatusTextBlock.Text = "Status: EMERGENCY STOP";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            MissionOutputBox.Text += "\n[CRITICAL] Emergency Stop triggered! All capability leases revoked.\n";
        }
    }
}
