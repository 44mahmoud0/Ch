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

        private readonly AiProviderClient _aiClient;

        public MainWindow()
        {
            this.InitializeComponent();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            _persona = new PersonaStateMachine(loggerFactory.CreateLogger<PersonaStateMachine>());
            _permissions = new AdvancedPermissionBroker(loggerFactory.CreateLogger<AdvancedPermissionBroker>());
            _taskGraph = new TaskGraphEngine(loggerFactory.CreateLogger<TaskGraphEngine>());
            _aiClient = new AiProviderClient(loggerFactory.CreateLogger<AiProviderClient>());

            // Wire interactive WinUI approval dialog delegate
            _permissions.ApprovalDelegate = async (capability, scope) =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Security Permission Request",
                    Content = $"Agent requests capability [{capability}] on scope [{scope}]. Allow execution?",
                    PrimaryButtonText = "Allow",
                    CloseButtonText = "Deny",
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            };

            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
            MissionOutputBox.Text = "[System] Mahmoud AI Desktop initialized successfully.\n[Security] AdvancedPermissionBroker and WorkspaceIsolation active.\n";
        }

        private async void RunMissionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string prompt = MissionInputBox.Text;
            if (string.IsNullOrWhiteSpace(prompt)) return;

            MissionOutputBox.Text += $"\n[User Prompt] {prompt}\n";
            StatusTextBlock.Text = "Status: AI Provider Query...";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);

            try
            {
                // Query truthful AI provider (e.g. local Ollama or configured endpoint)
                string response = await _aiClient.GenerateCompletionAsync("llama3", prompt, "http://localhost:11434", null, CancellationToken.None);
                MissionOutputBox.Text += $"[AI Response] {response}\n";
                StatusTextBlock.Text = "Status: Idle (Secure)";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            catch (Exception ex)
            {
                MissionOutputBox.Text += $"[Provider Error] {ex.Message}\n[Info] Ensure local Ollama is running or configure valid endpoint.\n";
                StatusTextBlock.Text = "Status: Provider Error";
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
