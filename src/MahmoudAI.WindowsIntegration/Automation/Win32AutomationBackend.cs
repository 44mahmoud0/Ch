using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Integration;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MahmoudAI.Core.Automation
{
    [SupportedOSPlatform("windows5.0")]
    internal sealed class Win32AutomationBackend : IWindowsAutomationBackend
    {
        private readonly ILogger<Win32AutomationBackend> _logger;

        public Win32AutomationBackend(ILogger<Win32AutomationBackend> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
                    AutomationOperation.Inspect => InspectWindow(request.Target, request.Context),
                    AutomationOperation.Activate => ActivateWindow(request.Target, request.Context),
                    AutomationOperation.Pointer => ClickAtCoordinates(request.Target, request.Context, cancellationToken),
                    AutomationOperation.Keyboard => SendUnicodeText(request.Target, request.Payload, request.Context, cancellationToken),
                    AutomationOperation.SetValue => new AutomationResult(false, null, "SetValue requires a UI Automation backend."),
                    AutomationOperation.Capture => new AutomationResult(false, null, "Capture belongs to the Screen Understanding gateway."),
                    _ => new AutomationResult(false, null, "Unsupported automation operation.")
                };
                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Win32Exception ex)
            {
                _logger.LogWarning(ex, "Win32 automation failed for {Operation} targeting {Target}.", request.Operation, request.Target);
                return Task.FromResult(new AutomationResult(false, null, ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Win32 automation failure for {Operation}.", request.Operation);
                return Task.FromResult(new AutomationResult(false, null, ex.Message));
            }
        }

        private static AutomationResult InspectWindow(string target, AutomationContext? context)
        {
            HWND window = ResolveWindow(target, context);
            if (window == HWND.Null)
            {
                return new AutomationResult(false, null, "Target window was not found or did not match its process identity.");
            }

            var title = ReadWindowTitle(window);
            return new AutomationResult(true, $"hwnd:{((nint)window).ToInt64().ToString(CultureInfo.InvariantCulture)};title:{title}");
        }

        private static AutomationResult ActivateWindow(string target, AutomationContext? context)
        {
            HWND window = ResolveWindow(target, context);
            if (window == HWND.Null)
            {
                return new AutomationResult(false, null, "Target window was not found or did not match its process identity.");
            }

            if (!PInvoke.SetForegroundWindow(window) || PInvoke.GetForegroundWindow() != window || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Target window could not be revalidated as foreground.");
            }

            return new AutomationResult(true, $"hwnd:{((nint)window).ToInt64().ToString(CultureInfo.InvariantCulture)}");
        }

        private static AutomationResult ClickAtCoordinates(
            string target,
            AutomationContext? context,
            CancellationToken cancellationToken)
        {
            if (!TryParsePointerTarget(target, out var windowTarget, out var x, out var y))
            {
                return new AutomationResult(false, null, "Pointer target must be formatted as window|x,y.");
            }

            HWND window = ResolveWindow(windowTarget, context);
            if (window == HWND.Null || PInvoke.GetForegroundWindow() != window || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Pointer target is not the revalidated foreground window.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!PInvoke.SetCursorPos(x, y))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCursorPos failed.");
            }

            if (PInvoke.GetForegroundWindow() != window || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Foreground window changed before pointer input.");
            }

            var inputs = new INPUT[2];
            inputs[0].type = INPUT_TYPE.INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;
            inputs[1].type = INPUT_TYPE.INPUT_MOUSE;
            inputs[1].mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;
            SendInputs(inputs);
            return new AutomationResult(true, $"clicked:{x},{y};hwnd:{((nint)window).ToInt64().ToString(CultureInfo.InvariantCulture)}");
        }

        private static AutomationResult SendUnicodeText(
            string target,
            string? payload,
            AutomationContext? context,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return new AutomationResult(false, null, "Keyboard payload is empty.");
            }

            HWND window = ResolveWindow(target, context);
            if (window == HWND.Null || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Target window was not found or did not match its process identity.");
            }

            if (!PInvoke.SetForegroundWindow(window) || PInvoke.GetForegroundWindow() != window || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Target window could not be revalidated as foreground.");
            }

            var inputs = new INPUT[payload.Length * 2];
            var index = 0;
            foreach (var character in payload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                inputs[index].type = INPUT_TYPE.INPUT_KEYBOARD;
                inputs[index].ki.wScan = character;
                inputs[index].ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;
                index++;
                inputs[index].type = INPUT_TYPE.INPUT_KEYBOARD;
                inputs[index].ki.wScan = character;
                inputs[index].ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
                index++;
            }

            if (PInvoke.GetForegroundWindow() != window || !MatchesContext(window, context))
            {
                return new AutomationResult(false, null, "Foreground window changed before keyboard input.");
            }

            SendInputs(inputs);
            return new AutomationResult(true, $"sent-unicode-chars:{payload.Length};hwnd:{((nint)window).ToInt64().ToString(CultureInfo.InvariantCulture)}");
        }

        private static HWND ResolveWindow(string target, AutomationContext? context)
        {
            if (string.IsNullOrWhiteSpace(target) || target.Equals("foreground", StringComparison.OrdinalIgnoreCase))
            {
                var foreground = PInvoke.GetForegroundWindow();
                return MatchesContext(foreground, context) ? foreground : HWND.Null;
            }

            if (target.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(target[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var handle))
            {
                var window = (HWND)(nint)handle;
                return PInvoke.IsWindowVisible(window) && MatchesContext(window, context) ? window : HWND.Null;
            }

            HWND match = HWND.Null;
            PInvoke.EnumWindows((window, _) =>
            {
                if (!PInvoke.IsWindowVisible(window) || !MatchesContext(window, context))
                {
                    return true;
                }

                var title = ReadWindowTitle(window);
                if (title.Equals(target, StringComparison.Ordinal))
                {
                    match = window;
                    return false;
                }

                return true;
            }, 0);
            return match;
        }

        private static bool MatchesContext(HWND window, AutomationContext? context)
        {
            if (window == HWND.Null || !PInvoke.IsWindow(window))
            {
                return false;
            }

            if (context is null)
            {
                return true;
            }

            if (PInvoke.GetWindowThreadProcessId(window, out var processId) == 0)
            {
                return false;
            }

            if (context.TargetProcessId is int targetProcessId && processId != targetProcessId)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.TargetProcessName))
            {
                try
                {
                    using var process = Process.GetProcessById((int)processId);
                    if (!process.ProcessName.Equals(context.TargetProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return true;
        }

        private static unsafe string ReadWindowTitle(HWND window)
        {
            var length = PInvoke.GetWindowTextLength(window);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = stackalloc char[length + 1];
            var written = PInvoke.GetWindowText(window, new PWSTR(buffer), length + 1);
            return written > 0 ? new string(buffer, 0, written) : string.Empty;
        }

        private static bool TryParsePointerTarget(string value, out string windowTarget, out int x, out int y)
        {
            windowTarget = "foreground";
            x = 0;
            y = 0;
            var separator = value.LastIndexOf('|');
            var coordinates = separator >= 0 ? value[(separator + 1)..] : value;
            if (separator >= 0)
            {
                windowTarget = value[..separator];
            }

            var parts = coordinates.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        private static unsafe void SendInputs(INPUT[] inputs)
        {
            var sent = PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput did not inject all requested input events.");
            }
        }
    }
}
