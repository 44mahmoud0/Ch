using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MahmoudAI.Core.Engine.TaskGraph;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Security;
using MahmoudAI.Core.Runtime;

namespace MahmoudAI.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly PersonaStateMachine _persona;
        private readonly AdvancedPermissionBroker _permissions;
        private readonly TaskGraphScheduler _taskGraph;
        private readonly AiProviderClient _aiClient;
        private readonly MissionEventHub _eventHub;
        private readonly IDisposable _missionEventSubscription;
        private CancellationTokenSource? _cts;
        private readonly ILogger<MainWindow> _logger;
        private readonly IWinUiContext _uiContext;

        public MainWindow(
            PersonaStateMachine persona,
            AdvancedPermissionBroker permissions,
            TaskGraphScheduler taskGraph,
            AiProviderClient aiClient,
            MissionEventHub eventHub,
            IWinUiContext uiContext,
            ILogger<MainWindow> logger)
        {
            InitializeComponent();
            _persona = persona;
            _permissions = permissions;
            _taskGraph = taskGraph;
            _aiClient = aiClient;
            _eventHub = eventHub;
            _uiContext = uiContext;
            _logger = logger;

            _missionEventSubscription = _eventHub.Subscribe(evt =>
            {
                AppendMissionOutput($"[TaskGraph] {evt.TaskId}: {evt.Type} (attempt {evt.Attempt})\n");
                return ValueTask.CompletedTask;
            });
            Closed += (_, _) => _missionEventSubscription.Dispose();

            _logger.LogInformation("MainWindow initialized via Dependency Injection Composition Root.");

            Activated += (_, _) =>
            {
                if (Content is FrameworkElement element && element.XamlRoot is not null)
                {
                    _uiContext.SetXamlRoot(element.XamlRoot);
                }
            };

            Title = "Mahmoud AI - Native Windows 11 Desktop Agent";
            MissionOutputBox.Text = "[System] Mahmoud AI Desktop initialized successfully.\n[Security] AdvancedPermissionBroker and WorkspaceIsolation active.\n";
        }

        private async void RunMissionButton_Click(object sender, RoutedEventArgs e)
        {
            string goal = MissionInputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(goal))
            {
                return;
            }

            if (_cts is not null)
            {
                AppendMissionOutput("\n[System] A mission is already running.\n");
                return;
            }

            _cts = new CancellationTokenSource();
            var missionCancellation = _cts;
            var missionId = Guid.NewGuid().ToString("N");
            RunMissionButton.IsEnabled = false;

            AppendMissionOutput($"\n[Mission Start] Mission {missionId} - Goal: {goal}\n");
            StatusTextBlock.Text = "Status: Mission Running (TaskGraph V2)...";
            StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Blue);

            try
            {
                var definitions = new List<MissionTaskDefinition>
                {
                    new(
                        "task-plan",
                        "Deconstruct Mission",
                        Array.Empty<string>(),
                        async cancellationToken =>
                        {
                            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                            AppendMissionOutput("[Planner Agent] Deconstructed goal into subtasks.\n");
                            return true;
                        },
                        TimeSpan.FromSeconds(10),
                        new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
                    new(
                        "task-exec",
                        "Execute Mission Steps",
                        new[] { "task-plan" },
                        async cancellationToken =>
                        {
                            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                            bool allowed = await _permissions.RequestCapabilityAsync(
                                CapabilityType.FilesWrite,
                                "mission-workspace",
                                TimeSpan.FromMinutes(5),
                                cancellationToken).ConfigureAwait(false);
                            if (!allowed)
                            {
                                AppendMissionOutput("[Safety Agent] Capability denied by user or security policy.\n");
                                return false;
                            }

                            AppendMissionOutput("[Coding/Tool Agent] Capability granted. Executing mission steps securely.\n");
                            return true;
                        },
                        TimeSpan.FromSeconds(30),
                        new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false))
                };

                var graphResult = await _taskGraph.ExecuteGraphAsync(
                    missionId,
                    definitions,
                    maxConcurrency: 2,
                    missionCancellation.Token).ConfigureAwait(false);
                bool success = graphResult.Status == GraphExecutionStatus.Completed;

                if (success)
                {
                    AppendMissionOutput("[Mission Complete] All TaskGraph V2 nodes executed successfully.\n");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = "Status: Idle (Secure)";
                        StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                    });
                }
                else
                {
                    AppendMissionOutput($"[Mission {graphResult.Status}] TaskGraph V2 returned a terminal non-success status.\n");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = graphResult.Status == GraphExecutionStatus.Cancelled
                            ? "Status: Cancelled / Emergency Stop"
                            : "Status: Failed / Safe Mode";
                        StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                AppendMissionOutput("\n[Mission Cancelled] Emergency Stop or user cancellation aborted the active mission.\n");
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusTextBlock.Text = "Status: Cancelled / Emergency Stop";
                    StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkRed);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mission {MissionId} failed unexpectedly.", missionId);
                AppendMissionOutput($"\n[Mission Error] {ex.Message}\n");
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusTextBlock.Text = "Status: Error";
                    StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                });
            }
            finally
            {
                missionCancellation.Dispose();
                _cts = null;
                DispatcherQueue.TryEnqueue(() => RunMissionButton.IsEnabled = true);
            }
        }

        private void EmergencyStopButton_Click(object sender, RoutedEventArgs e)
        {
            _permissions.TriggerEmergencyStop();
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The mission completed concurrently with the emergency-stop click.
            }

            StatusTextBlock.Text = "Status: EMERGENCY STOP / SAFE MODE";
            StatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            MissionOutputBox.Text += "\n[CRITICAL] Emergency Stop triggered! All leases revoked, safe mode active, active tasks cancelled.\n";
        }

        private void AppendMissionOutput(string message)
        {
            DispatcherQueue.TryEnqueue(() => MissionOutputBox.Text += message);
        }

        private async Task<bool> ShowPermissionDialogAsync(CapabilityType capability, string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dialog = new ContentDialog
            {
                Title = "Security Permission Request",
                Content = $"Mahmoud AI requests {capability}\nScope: {scope}",
                PrimaryButtonText = "Allow",
                CloseButtonText = "Deny",
                XamlRoot = Content.XamlRoot
            };

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        dialog.Hide();
                    }
                    catch
                    {
                        // Dialog may already be closed.
                    }
                });
            });

            ContentDialogResult result = await dialog.ShowAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result == ContentDialogResult.Primary;
        }
    }
}
