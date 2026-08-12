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
        private CancellationTokenSource _cts = new();

        public MainWindow()
        {
            this.InitializeComponent();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            _persona = new PersonaStateMachine(loggerFactory.CreateLogger<PersonaStateMachine>());
            _permissions = new AdvancedPermissionBroker(loggerFactory.CreateLogger<AdvancedPermissionBroker>());
            _taskGraph = new TaskGraphEngine(loggerFactory.CreateLogger<TaskGraphEngine>());
            _aiClient = new AiProviderClient(loggerFactory.CreateLogger<AiProviderClient>());

            // Wire interactive WinUI approval dialog delegate with safe UI thread marshaling
            _permissions.ApprovalDelegate = async (capability, scope) =>
            {
                bool approved = false;
                var tcs = new TaskCompletionSource<bool>();

                void ShowDialog()
                {
                    _ = Task.Run(async () =>
                    {
                        try
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
                            tcs.SetResult(result == ContentDialogResult.Primary);
                        }
                        catch (Exception)
                        {
                            tcs.SetResult(false);
                        }
                    });
                }

                if (this.DispatcherQueue.HasThreadAccess)
                {
                    ShowDialog();
                }
                else
                {
                    this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => ShowDialog());
                }

                return await tcs.Task;
            };

            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
            MissionOutputBox.Text = "[System] Mahmoud AI Desktop initialized successfully.\n[Security] AdvancedPermissionBroker and WorkspaceIsolation active.\n";
        }

        private async void RunMissionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string goal = MissionInputBox.Text;
            if (string.IsNullOrWhiteSpace(goal)) return;

            _cts = new CancellationTokenSource();
            MissionOutputBox.Text += $"\n[Mission Start] Goal: {goal}\n";
            StatusTextBlock.Text = "Status: Mission Running (TaskGraph)...";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);

            try
            {
                // Create structured task graph mission DAG
                var graph = new MahmoudAI.Core.Engine.TaskGraph();
                var planningTask = new MahmoudAI.Core.Engine.TaskNode("task-plan", "Deconstruct Mission", async (ct) =>
                {
                    await Task.Delay(200, ct);
                    MissionOutputBox.Text += "[Planner Agent] Deconstructed goal into subtasks.\n";
                });
                var executionTask = new MahmoudAI.Core.Engine.TaskNode("task-exec", "Execute Mission Steps", async (ct) =>
                {
                    await Task.Delay(300, ct);
                    // Example guarded capability request through permission broker
                    bool allowed = await _permissions.RequestCapabilityAsync(CapabilityType.FileWrite, "mission-workspace", TimeSpan.FromMinutes(5), ct);
                    if (allowed)
                    {
                        MissionOutputBox.Text += "[Coding/Tool Agent] Capability granted. Executing mission steps securely.\n";
                    }
                    else
                    {
                        MissionOutputBox.Text += "[Safety Agent] Capability denied by user or security policy.\n";
                    }
                });

                executionTask.AddDependency("task-plan");
                graph.AddNode(planningTask);
                graph.AddNode(executionTask);

                bool success = await _taskGraph.ExecuteGraphAsync(graph, _cts.Token);

                if (success)
                {
                    MissionOutputBox.Text += "[Mission Complete] All task graph nodes executed successfully.\n";
                    StatusTextBlock.Text = "Status: Idle (Secure)";
                    StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                }
                else
                {
                    MissionOutputBox.Text += "[Mission Failed/Cancelled] Task graph execution did not complete successfully.\n";
                    StatusTextBlock.Text = "Status: Failed / Cancelled";
                    StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
            catch (OperationCanceledException)
            {
                MissionOutputBox.Text += "\n[Mission Cancelled] Emergency Stop aborted active mission.\n";
                StatusTextBlock.Text = "Status: Cancelled by Emergency Stop";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            }
            catch (Exception ex)
            {
                MissionOutputBox.Text += $"\n[Mission Error] {ex.Message}\n";
                StatusTextBlock.Text = "Status: Error";
                StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private void EmergencyStopButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _permissions.TriggerEmergencyStop();
            _cts.Cancel();
            StatusTextBlock.Text = "Status: EMERGENCY STOP";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            MissionOutputBox.Text += "\n[CRITICAL] Emergency Stop triggered! All leases revoked and active tasks cancelled.\n";
        }
    }
}
