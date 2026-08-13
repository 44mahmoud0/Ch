using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Integration;

namespace MahmoudAI.WindowsIntegration.Automation
{
    [SupportedOSPlatform("windows")]
    internal sealed class Uia3AutomationBackend : IWindowsAutomationBackend, IUiaSemanticAutomation, IDisposable
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

        public Task<UiaQueryResult> QueryAsync(
            UiaQueryRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new UiaQueryResult(UiaMatchStatus.Cancelled, Array.Empty<UiaElementSnapshot>(), "UIA query was cancelled."));
            }

            try
            {
                var result = QueryInternal(request, cancellationToken);
                return Task.FromResult(result.Result);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(new UiaQueryResult(UiaMatchStatus.Cancelled, Array.Empty<UiaElementSnapshot>(), "UIA query was cancelled."));
            }
            catch (TimeoutException ex)
            {
                return Task.FromResult(new UiaQueryResult(UiaMatchStatus.Timeout, Array.Empty<UiaElementSnapshot>(), ex.Message));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new UiaQueryResult(UiaMatchStatus.NotFound, Array.Empty<UiaElementSnapshot>(), ex.Message));
            }
        }

        public Task<UiaActionResult> ExecuteAsync(
            UiaActionRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new UiaActionResult(false, UiaMatchStatus.Cancelled, "UIA action was cancelled."));
            }

            try
            {
                var resolved = QueryInternal(request.Query, cancellationToken);
                if (resolved.Element is null)
                {
                    return Task.FromResult(new UiaActionResult(
                        false,
                        resolved.Result.Status,
                        resolved.Result.Error));
                }

                var result = ExecutePattern(
                    resolved.Element,
                    request.Action,
                    request.Value,
                    resolved.Result.Candidates[0]);
                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(new UiaActionResult(false, UiaMatchStatus.Cancelled, "UIA action was cancelled."));
            }
            catch (TimeoutException ex)
            {
                return Task.FromResult(new UiaActionResult(false, UiaMatchStatus.Timeout, ex.Message));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new UiaActionResult(false, UiaMatchStatus.NotFound, ex.Message));
            }
        }

        public void Dispose()
        {
            _automation.Dispose();
        }

        private AutomationResult Inspect(string target, AutomationContext? context)
        {
            var element = ResolveWindow(target, context);
            if (element is null)
            {
                return new AutomationResult(false, null, "UIA3 element was not found or did not match the target process.");
            }

            return new AutomationResult(true, $"name:{element.Name};controlType:{element.ControlType}");
        }

        private AutomationResult Activate(string target, AutomationContext? context)
        {
            var element = ResolveWindow(target, context);
            if (element is null)
            {
                return new AutomationResult(false, null, "UIA3 element was not found or did not match the target process.");
            }

            element.AsWindow().Focus();
            return new AutomationResult(true, $"name:{element.Name}");
        }

        private AutomationResult SetValue(string target, string? payload, AutomationContext? context)
        {
            if (payload is null)
            {
                return new AutomationResult(false, null, "UIA3 SetValue requires a payload.");
            }

            var element = ResolveWindow(target, context);
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

        private (UiaQueryResult Result, AutomationElement? Element) QueryInternal(
            UiaQueryRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryParseHwnd(request.WindowTarget, out var handle))
            {
                return (Failure(UiaMatchStatus.NotFound, "UIA query requires a valid hwnd:<handle> window target."), null);
            }

            var root = _automation.FromHandle(handle);
            if (root is null)
            {
                return (Failure(UiaMatchStatus.NotFound, "The target window does not exist."), null);
            }

            var rootProcessId = root.Properties.ProcessId.ValueOrDefault;
            var expectedProcessId = request.TargetProcessId;
            if (expectedProcessId is int expected && rootProcessId != expected)
            {
                return (Failure(UiaMatchStatus.ProcessMismatch, "The target window process does not match the requested process."), null);
            }

            var deadline = Stopwatch.GetTimestamp() +
                (long)(request.EffectiveLimits.EffectiveTimeout.TotalSeconds * Stopwatch.Frequency);
            var current = new List<AutomationElement> { root };
            var processMismatch = false;

            for (var index = 0; index < request.Path.Segments.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selector = request.Path.Segments[index];
                var matches = new List<AutomationElement>();
                var nodesVisited = 0;

                foreach (var parent in current)
                {
                    var candidates = EnumerateCandidates(parent, selector.Scope, request.EffectiveLimits, deadline, cancellationToken, out var visited);
                    nodesVisited += visited;
                    if (nodesVisited > request.EffectiveLimits.MaxNodesVisited)
                    {
                        throw new TimeoutException("UIA query exceeded its node budget.");
                    }

                    foreach (var candidate in candidates)
                    {
                        if (!MatchesSelector(candidate, selector))
                        {
                            continue;
                        }

                        var candidateProcessId = candidate.Properties.ProcessId.ValueOrDefault;
                        var candidateExpectedProcessId = selector.ProcessId ?? expectedProcessId ?? rootProcessId;
                        if (candidateExpectedProcessId is int required && candidateProcessId != required)
                        {
                            processMismatch = true;
                            continue;
                        }

                        matches.Add(candidate);
                    }
                }

                if (matches.Count == 0)
                {
                    var status = processMismatch ? UiaMatchStatus.ProcessMismatch : UiaMatchStatus.NotFound;
                    return (Failure(status, status == UiaMatchStatus.ProcessMismatch
                        ? "The matching UIA element crossed the target process boundary."
                        : "No UIA element matched the selector."), null);
                }

                current = matches;
            }

            if (current.Count > 1)
            {
                return (new UiaQueryResult(
                    UiaMatchStatus.Ambiguous,
                    current.Select(ToSnapshot).ToArray(),
                    $"Selector matched {current.Count} UIA elements; refusing to choose an arbitrary element."), null);
            }

            var snapshot = ToSnapshot(current[0]);
            return (new UiaQueryResult(UiaMatchStatus.Found, new[] { snapshot }), current[0]);
        }

        private static IReadOnlyList<AutomationElement> EnumerateCandidates(
            AutomationElement root,
            UiaSearchScope scope,
            UiaQueryLimits limits,
            long deadline,
            CancellationToken cancellationToken,
            out int nodesVisited)
        {
            nodesVisited = 0;
            if (scope == UiaSearchScope.Element)
            {
                return new[] { root };
            }

            var result = new List<AutomationElement>();
            var queue = new Queue<(AutomationElement Element, int Depth)>();
            foreach (var child in root.FindAllChildren())
            {
                queue.Enqueue((child, 1));
            }

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Stopwatch.GetTimestamp() > deadline)
                {
                    throw new TimeoutException("UIA query exceeded its timeout budget.");
                }

                var (element, depth) = queue.Dequeue();
                nodesVisited++;
                if (nodesVisited > limits.MaxNodesVisited)
                {
                    throw new TimeoutException("UIA query exceeded its node budget.");
                }

                result.Add(element);
                if (scope == UiaSearchScope.Children || depth >= limits.MaxDepth)
                {
                    continue;
                }

                foreach (var child in element.FindAllChildren())
                {
                    queue.Enqueue((child, depth + 1));
                }
            }

            return result;
        }

        private static bool MatchesSelector(AutomationElement element, UiaSelector selector)
        {
            if (selector.AutomationId is not null &&
                !string.Equals(element.Properties.AutomationId.ValueOrDefault, selector.AutomationId, StringComparison.Ordinal))
            {
                return false;
            }

            if (selector.Name is not null && !MatchesText(element.Properties.Name.ValueOrDefault, selector.Name, selector.NameMatch))
            {
                return false;
            }

            if (selector.ClassName is not null &&
                !string.Equals(element.Properties.ClassName.ValueOrDefault, selector.ClassName, StringComparison.Ordinal))
            {
                return false;
            }

            if (selector.FrameworkId is not null &&
                !string.Equals(element.Properties.FrameworkId.ValueOrDefault, selector.FrameworkId, StringComparison.Ordinal))
            {
                return false;
            }

            if (selector.ControlType is not null && !MatchesControlType(element, selector.ControlType.Value))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesText(string? actual, string expected, UiaMatchMode mode)
        {
            actual ??= string.Empty;
            return mode switch
            {
                UiaMatchMode.Exact => string.Equals(actual, expected, StringComparison.Ordinal),
                UiaMatchMode.Contains => actual.Contains(expected, StringComparison.Ordinal),
                UiaMatchMode.StartsWith => actual.StartsWith(expected, StringComparison.Ordinal),
                _ => false
            };
        }

        private static bool MatchesControlType(AutomationElement element, UiaControlKind expected)
        {
            var actual = element.ControlType.ToString();
            var expectedName = expected switch
            {
                UiaControlKind.CheckBox => "CheckBox",
                UiaControlKind.ListItem => "ListItem",
                _ => expected.ToString()
            };
            return string.Equals(actual, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private static UiaElementSnapshot ToSnapshot(AutomationElement element)
        {
            var bounds = element.Properties.BoundingRectangle.ValueOrDefault;
            var patterns = new List<string>();
            if (element.Patterns.Invoke.PatternOrDefault is not null) patterns.Add("Invoke");
            if (element.Patterns.Value.PatternOrDefault is not null) patterns.Add("Value");
            if (element.Patterns.Toggle.PatternOrDefault is not null) patterns.Add("Toggle");
            if (element.Patterns.SelectionItem.PatternOrDefault is not null) patterns.Add("SelectionItem");
            if (element.Properties.IsEnabled.ValueOrDefault) patterns.Add("Focus");

            return new UiaElementSnapshot(
                element.Properties.AutomationId.ValueOrDefault ?? string.Empty,
                element.Properties.Name.ValueOrDefault ?? string.Empty,
                element.ControlType.ToString(),
                element.Properties.ClassName.ValueOrDefault ?? string.Empty,
                element.Properties.FrameworkId.ValueOrDefault ?? string.Empty,
                element.Properties.ProcessId.ValueOrDefault,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                element.Properties.IsEnabled.ValueOrDefault,
                element.Properties.IsOffscreen.ValueOrDefault,
                patterns);
        }

        private static UiaActionResult ExecutePattern(
            AutomationElement element,
            UiaActionType action,
            string? value,
            UiaElementSnapshot snapshot)
        {
            switch (action)
            {
                case UiaActionType.Invoke:
                    var invoke = element.Patterns.Invoke.PatternOrDefault;
                    if (invoke is null) return Unsupported(snapshot, "Invoke");
                    invoke.Invoke();
                    return Success(snapshot, "invoked");
                case UiaActionType.SetValue:
                    if (value is null) return new UiaActionResult(false, UiaMatchStatus.UnsupportedPattern, "SetValue requires a value.", snapshot);
                    var valuePattern = element.Patterns.Value.PatternOrDefault;
                    if (valuePattern is null) return Unsupported(snapshot, "Value");
                    valuePattern.SetValue(value);
                    return Success(snapshot, "value-set");
                case UiaActionType.Toggle:
                    var toggle = element.Patterns.Toggle.PatternOrDefault;
                    if (toggle is null) return Unsupported(snapshot, "Toggle");
                    toggle.Toggle();
                    return Success(snapshot, "toggled");
                case UiaActionType.Select:
                    var selection = element.Patterns.SelectionItem.PatternOrDefault;
                    if (selection is null) return Unsupported(snapshot, "SelectionItem");
                    selection.Select();
                    return Success(snapshot, "selected");
                case UiaActionType.Focus:
                    element.Focus();
                    return Success(snapshot, "focused");
                default:
                    return new UiaActionResult(false, UiaMatchStatus.UnsupportedPattern, "Unsupported UIA action.", snapshot);
            }
        }

        private static UiaActionResult Success(UiaElementSnapshot snapshot, string message)
        {
            return new UiaActionResult(true, UiaMatchStatus.Found, message, snapshot);
        }

        private static UiaActionResult Unsupported(UiaElementSnapshot snapshot, string pattern)
        {
            return new UiaActionResult(false, UiaMatchStatus.UnsupportedPattern, $"Target does not expose the UIA {pattern} pattern.", snapshot);
        }

        private AutomationElement? ResolveWindow(string target, AutomationContext? context)
        {
            if (!TryParseHwnd(target, out var handle))
            {
                return null;
            }

            var element = _automation.FromHandle(handle);
            return element is not null && MatchesProcess(element, context) ? element : null;
        }

        private static bool TryParseHwnd(string target, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            return target.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(target[5..], out var value)
                && (handle = new IntPtr(value)) != IntPtr.Zero;
        }

        private static bool MatchesProcess(AutomationElement element, AutomationContext? context)
        {
            if (context?.TargetProcessId is int processId && element.Properties.ProcessId.ValueOrDefault != processId)
            {
                return false;
            }

            return true;
        }

        private static UiaQueryResult Failure(UiaMatchStatus status, string message)
        {
            return new UiaQueryResult(status, Array.Empty<UiaElementSnapshot>(), message);
        }
    }
}
