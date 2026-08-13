using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Automation
{
    public enum UiaControlKind
    {
        Button,
        Edit,
        CheckBox,
        ComboBox,
        List,
        ListItem,
        Text,
        Window,
        Pane,
        Custom
    }

    public enum UiaSearchScope
    {
        Descendants,
        Children,
        Element
    }

    public enum UiaMatchMode
    {
        Exact,
        Contains,
        StartsWith
    }

    public enum UiaMatchStatus
    {
        Found,
        NotFound,
        Ambiguous,
        ProcessMismatch,
        Timeout,
        Cancelled,
        Denied,
        UnsupportedPattern,
        ProviderError
    }

    public sealed record UiaSelector(
        string? AutomationId = null,
        string? Name = null,
        UiaControlKind? ControlType = null,
        string? ClassName = null,
        string? FrameworkId = null,
        int? ProcessId = null,
        UiaSearchScope Scope = UiaSearchScope.Descendants,
        UiaMatchMode NameMatch = UiaMatchMode.Exact);

    public sealed record UiaSelectorPath(IReadOnlyList<UiaSelector> Segments)
    {
        public UiaSelectorPath(params UiaSelector[] segments)
            : this((IReadOnlyList<UiaSelector>)segments)
        {
        }
    }

    public sealed record UiaQueryLimits(
        int MaxDepth = 32,
        int MaxNodesVisited = 2_000,
        TimeSpan? Timeout = null)
    {
        public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(2);
    }

    public sealed record UiaQueryRequest(
        string WindowTarget,
        UiaSelectorPath Path,
        int? TargetProcessId = null,
        UiaQueryLimits? Limits = null)
    {
        public UiaQueryLimits EffectiveLimits => Limits ?? new UiaQueryLimits();
    }

    public sealed record UiaElementSnapshot(
        string AutomationId,
        string Name,
        string ControlType,
        string ClassName,
        string FrameworkId,
        int ProcessId,
        int BoundingX,
        int BoundingY,
        int BoundingWidth,
        int BoundingHeight,
        bool IsEnabled,
        bool IsOffscreen,
        IReadOnlyList<string> SupportedPatterns);

    public sealed record UiaQueryResult(
        UiaMatchStatus Status,
        IReadOnlyList<UiaElementSnapshot> Candidates,
        string? Error = null);

    public enum UiaActionType
    {
        Invoke,
        SetValue,
        Toggle,
        Select,
        Focus
    }

    public sealed record UiaActionRequest(
        UiaQueryRequest Query,
        UiaActionType Action,
        string? Value = null);

    public sealed record UiaActionResult(
        bool Succeeded,
        UiaMatchStatus Status,
        string? Message = null,
        UiaElementSnapshot? TargetElement = null);

    public interface IUiaSemanticAutomation
    {
        Task<UiaQueryResult> QueryAsync(
            UiaQueryRequest request,
            CancellationToken cancellationToken);

        Task<UiaActionResult> ExecuteAsync(
            UiaActionRequest request,
            CancellationToken cancellationToken);
    }
}
