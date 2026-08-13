using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using MahmoudAI.Core.Integration;

namespace MahmoudAI.WindowsIntegration.Automation
{
    [SupportedOSPlatform("windows")]
    internal sealed class Uia3AutomationBackend : IWindowsAutomationBackend, IDisposable
    {
        private readonly UIA3Automation _automation = new();

        public Task<AutomationResult> ExecuteAsync(
            AutomationRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = request.Operation switch
                {
                    AutomationOperation.Inspect => Inspect(request.Target, request.Context),
                    AutomationOperation.Activate => Activate(request.Target, request.Context),
                    AutomationOperation.SetValue => SetValue(request.Target, request.Payload, request.Context),
                    AutomationOperation.Pointer => new AutomationResult(false, null, "UIA3 does not synthesize physical pointer input; use the guarded Win32 fallback."),
                    AutomationOperation.Keyboard => new AutomationResult(false, null, "UIA3 does not synthesize physical keyboard input; use the guarded Win32 fallback."),
                    AutomationOperation.Capture => new AutomationResult(false, null, "Screen capture belongs to the Screen Understanding gateway."),
                    _ => new AutomationResult(false, null, "Unsupported UIA3 operation.")
                };
                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AutomationResult(false, null, ex.Message));
            }
        }

        public void Dispose()
        {
            _automation.Dispose();
        }

        private AutomationResult Inspect(string target, AutomationContext? context)
        {
            var element = Resolve(target, context);
            if (element is null)
            {
                return new AutomationResult(false, null, "UIA3 element was not found or did not match the target process.");
            }

            return new AutomationResult(true, $"name:{element.Name};controlType:{element.ControlType}");
        }

        private AutomationResult Activate(string target, AutomationContext? context)
        {
            var element = Resolve(target, context);
            if (element is null)
            {
                return new AutomationResult(false, null, "UIA3 element was not found or did not match the target process.");
            }

            var window = element.AsWindow();
            window.Focus();
            return new AutomationResult(true, $"name:{element.Name}");
        }

        private AutomationResult SetValue(string target, string? payload, AutomationContext? context)
        {
            if (payload is null)
            {
                return new AutomationResult(false, null, "UIA3 SetValue requires a payload.");
            }

            var element = Resolve(target, context);
            if (element is null)
            {
                return new AutomationResult(false, null, "UIA3 element was not found or did not match the target process.");
            }

            var valuePattern = element.Patterns.Value.PatternOrDefault;
            if (valuePattern is null)
            {
                return new AutomationResult(false, null, "Target does not expose the UIA Value pattern.");
            }

            valuePattern.SetValue(payload);
            return new AutomationResult(true, "value-set");
        }

        private AutomationElement? Resolve(string target, AutomationContext? context)
        {
            if (!target.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase)
                || !long.TryParse(target[5..], out var handle))
            {
                return null;
            }

            var element = _automation.FromHandle(new IntPtr(handle));
            if (element is null || !MatchesProcess(element, context))
            {
                return null;
            }

            return element;
        }

        private static bool MatchesProcess(AutomationElement element, AutomationContext? context)
        {
            if (context?.TargetProcessId is int processId && element.Properties.ProcessId.ValueOrDefault != processId)
            {
                return false;
            }

            return true;
        }
    }
}
