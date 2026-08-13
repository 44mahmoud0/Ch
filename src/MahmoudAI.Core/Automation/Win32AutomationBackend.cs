using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Integration;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    [SupportedOSPlatform("windows")]
    public sealed class Win32AutomationBackend : IWindowsAutomationBackend
    {
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;
        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;
        private const uint KeyEventUnicode = 0x0004;
        private const uint KeyEventKeyUp = 0x0002;

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
                    AutomationOperation.Inspect => InspectWindow(request.Target),
                    AutomationOperation.Activate => ActivateWindow(request.Target),
                    AutomationOperation.Pointer => ClickAtCoordinates(request.Target, cancellationToken),
                    AutomationOperation.Keyboard => SendUnicodeText(request.Target, request.Payload, cancellationToken),
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

        private static AutomationResult InspectWindow(string target)
        {
            nint window = ResolveWindow(target);
            if (window == nint.Zero)
            {
                return new AutomationResult(false, null, "Target window was not found.");
            }

            var title = ReadWindowTitle(window);
            return new AutomationResult(
                true,
                $"hwnd:{window.ToInt64().ToString(CultureInfo.InvariantCulture)};title:{title}");
        }

        private static AutomationResult ActivateWindow(string target)
        {
            nint window = ResolveWindow(target);
            if (window == nint.Zero)
            {
                return new AutomationResult(false, null, "Target window was not found.");
            }

            if (!SetForegroundWindow(window))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetForegroundWindow failed.");
            }

            return new AutomationResult(true, $"hwnd:{window.ToInt64().ToString(CultureInfo.InvariantCulture)}");
        }

        private static AutomationResult ClickAtCoordinates(string target, CancellationToken cancellationToken)
        {
            if (!TryParseCoordinates(target, out var x, out var y))
            {
                return new AutomationResult(false, null, "Pointer target must be formatted as x,y.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!SetCursorPos(x, y))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCursorPos failed.");
            }

            var inputs = new[]
            {
                new Input
                {
                    Type = InputMouse,
                    Data = new InputUnion
                    {
                        Mouse = new MouseInput { Flags = MouseLeftDown }
                    }
                },
                new Input
                {
                    Type = InputMouse,
                    Data = new InputUnion
                    {
                        Mouse = new MouseInput { Flags = MouseLeftUp }
                    }
                }
            };
            SendInputs(inputs);
            return new AutomationResult(true, $"clicked:{x},{y}");
        }

        private static AutomationResult SendUnicodeText(
            string target,
            string? payload,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return new AutomationResult(false, null, "Keyboard payload is empty.");
            }

            nint window = ResolveWindow(target);
            if (window == nint.Zero)
            {
                return new AutomationResult(false, null, "Target window was not found.");
            }

            if (!SetForegroundWindow(window))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetForegroundWindow failed.");
            }

            var inputs = new Input[payload.Length * 2];
            var index = 0;
            foreach (var character in payload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                inputs[index++] = new Input
                {
                    Type = InputKeyboard,
                    Data = new InputUnion
                    {
                        Keyboard = new KeyboardInput
                        {
                            ScanCode = character,
                            Flags = KeyEventUnicode
                        }
                    }
                };
                inputs[index++] = new Input
                {
                    Type = InputKeyboard,
                    Data = new InputUnion
                    {
                        Keyboard = new KeyboardInput
                        {
                            ScanCode = character,
                            Flags = KeyEventUnicode | KeyEventKeyUp
                        }
                    }
                };
            }

            SendInputs(inputs);
            return new AutomationResult(true, $"sent-unicode-chars:{payload.Length}");
        }

        private static nint ResolveWindow(string target)
        {
            if (string.IsNullOrWhiteSpace(target) || target.Equals("foreground", StringComparison.OrdinalIgnoreCase))
            {
                return GetForegroundWindow();
            }

            if (target.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(target[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var handle))
            {
                return new nint(handle);
            }

            nint match = nint.Zero;
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window))
                {
                    return true;
                }

                var title = ReadWindowTitle(window);
                if (title.Equals(target, StringComparison.OrdinalIgnoreCase)
                    || title.Contains(target, StringComparison.OrdinalIgnoreCase))
                {
                    match = window;
                    return false;
                }

                return true;
            }, nint.Zero);
            return match;
        }

        private static string ReadWindowTitle(nint window)
        {
            var length = GetWindowTextLength(window);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            GetWindowText(window, builder, builder.Capacity);
            return builder.ToString();
        }

        private static bool TryParseCoordinates(string value, out int x, out int y)
        {
            x = 0;
            y = 0;
            var parts = value.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        private static void SendInputs(Input[] inputs)
        {
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            if (sent != inputs.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput did not inject all requested input events.");
            }
        }

        private delegate bool EnumWindowsCallback(nint window, nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(nint window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(nint window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(nint window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(nint window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInput);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Mouse;

            [FieldOffset(0)]
            public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }
    }
}
