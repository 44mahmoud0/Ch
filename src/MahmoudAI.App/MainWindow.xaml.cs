using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Security;
using MahmoudAI.Core.Runtime;
using MahmoudAI.Core.Engine;

namespace MahmoudAI.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly PersonaStateMachine _persona;
        private readonly AdvancedPermissionBroker _permissions;
        private readonly TaskGraphEngine _taskGraph;
        private readonly AiProviderClient _aiClient;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            this.InitializeComponent();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            _persona = new PersonaStateMachine(loggerFactory.CreateLogger<PersonaStateMachine>());
            _permissions = new AdvancedPermissionBroker(loggerFactory.CreateLogger<AdvancedPermissionBroker>());
            _taskGraph = new TaskGraphEngine(loggerFactory.CreateLogger<TaskGraphEngine>());
            _aiClient = new AiProviderClient(loggerFactory.CreateLogger<AiProviderClient>());

            // Wire interactive WinUI approval dialog delegate with true UI thread marshaling and CancellationToken support
            _permissions.ApprovalDelegate = async (capability, scope, ct) =>
            {
                if (DispatcherQueue.HasThreadAccess)
                {
                    return await ShowPermissionDialogAsync(capability, scope, ct);
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                bool queued = DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        bool result = await ShowPermissionDialogAsync(capability, scope, ct);
                        tcs.TrySetResult(result);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(ct);
                    }
                    catch (Exception)
                    {
                        tcs.TrySetResult(false);
                    }
                });

                if (!queued) return false;

                return await tcs.Task.WaitAsync(ct);
            };

            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
            MissionOutputBox.Text = "[System] Mahmoud AI Desktop initialized successfully.\n[Security] AdvancedPermissionBroker and WorkspaceIsolation active.\n";
        }

        private async void RunMissionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string goal = MissionInputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(goal)) return;

            if (_cts is not null)
            {
                MissionOutputBox.Text += "\n[System] A mission is already running.\n";
                return;
            }

            _cts = new CancellationTokenSource();
            RunMissionButton.IsEnabled = false;

            MissionOutputBox.Text += $"\n[Mission Start] Goal: {goal}\n";
            StatusTextBlock.Text = "Status: Mission Running (TaskGraph)...";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);

            void AppendLog(string message)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    MissionOutputBox.Text += message;
                });
            }

            try
            {
                var tasks = new List<MissionTask>
                {
                    new MissionTask
                    {
                        Id = "task-plan",
                        Name = "Deconstruct Mission",
                        Action = async ct =>
                        {
                            await Task.Delay(200, ct);
                            AppendLog("[Planner Agent] Deconstructed goal into subtasks.\n");
                            return true;
                        }
                    },
                    new MissionTask
                    {
                        Id = "task-exec",
                        Name = "Execute Mission Steps",
                        Dependencies = { "task-plan" },
                        Action = async ct =>
                        {
                            await Task.Delay(300, ct);
                            bool allowed = await _permissions.RequestCapabilityAsync(CapabilityType.FilesWrite, "mission-workspace", TimeSpan.FromMinutes(5), ct);
                            if (allowed)
                            {
                                AppendLog("[Coding/Tool Agent] Capability granted. Executing mission steps securely.\n");
                                return true;
                            }
                            else
                            {
                                AppendLog("[Safety Agent] Capability denied by user or security policy.\n");
                                return false;
                            }
                        }
                    }
                };

                bool success = await _taskGraph.ExecuteGraphAsync(tasks, _cts.Token);

                if (success)
                {
                    AppendLog("[Mission Complete] All task graph nodes executed successfully.\n");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = "Status: Idle (Secure)";
                        StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    });
                }
                else
                {
                    AppendLog("[Mission Failed/Cancelled] Task graph execution did not complete successfully.\n");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = "Status: Failed / Cancelled";
                        StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("\n[Mission Cancelled] Emergency Stop or user cancellation aborted active mission.\n");
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusTextBlock.Text = "Status: Cancelled / Emergency Stop";
                    StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
                });
            }
            catch (Exception ex)
            {
                AppendLog($"\n[Mission Error] {ex.Message}\n");
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusTextBlock.Text = "Status: Error";
                    StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                });
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                DispatcherQueue.TryEnqueue(() =>
                {
                    RunMissionButton.IsEnabled = true;
                });
            }
        }

        private void EmergencyStopButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _permissions.TriggerEmergencyStop();
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // Ignore if already disposed
            }
            StatusTextBlock.Text = "Status: EMERGENCY STOP / SAFE MODE";
            StatusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            MissionOutputBox.Text += "\n[CRITICAL] Emergency Stop triggered! All leases revoked, safe mode active, active tasks cancelled.\n";
        }

        private async Task<bool> ShowPermissionDialogAsync(CapabilityType capability, string scope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var dialog = new ContentDialog
            {
                Title = "Security Permission Request",
                Content = $"Mahmoud AI requests {capability}\nScope: {scope}",
                PrimaryButtonText = "Allow",
                CloseButtonText = "Deny",
                XamlRoot = Content.XamlRoot
            };

            using CancellationTokenRegistration registration = ct.Register(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        dialog.Hide();
                    }
                    catch
                    {
                        // Dialog may already be closed
                    }
                });
            });

            ContentDialogResult result = await dialog.ShowAsync();
            ct.ThrowIfCancellationRequested();

            return result == ContentDialogResult.Primary;
        }
    }
}
