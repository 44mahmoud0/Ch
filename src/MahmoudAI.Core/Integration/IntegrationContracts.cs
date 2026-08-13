using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Security;

namespace MahmoudAI.Core.Integration
{
    public enum ProviderFailureKind
    {
        Unavailable,
        Authentication,
        RateLimited,
        InvalidRequest,
        Cancelled,
        Timeout,
        Protocol,
        Unknown
    }

    public sealed record ModelMessage(string Role, string Content);

    public sealed record ModelRequest(
        string Model,
        IReadOnlyList<ModelMessage> Messages,
        IReadOnlyList<ToolSchema> Tools);

    public sealed record ToolSchema(string Name, string Description, string JsonSchema);

    public sealed record ModelDelta(string Text, bool IsFinal);

    public sealed record ModelResponse(
        string Text,
        string Model,
        TimeSpan Duration,
        ProviderFailureKind? Failure = null,
        string? Error = null);

    public sealed record ProviderHealth(
        bool IsHealthy,
        string Provider,
        string? Version,
        string? Error = null);

    public interface IModelGateway
    {
        Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);

        IAsyncEnumerable<ModelDelta> StreamAsync(
            ModelRequest request,
            CancellationToken cancellationToken);

        Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken);
    }

    public enum AutomationOperation
    {
        Inspect,
        Activate,
        SetValue,
        Pointer,
        Keyboard,
        Capture
    }

    public sealed record AutomationRequest(
        CapabilityType RequiredCapability,
        string Scope,
        AutomationOperation Operation,
        string Target,
        string? Payload = null);

    public sealed record AutomationResult(
        bool Succeeded,
        string? Output,
        string? Error = null);

    public interface IWindowsAutomationBackend
    {
        Task<AutomationResult> ExecuteAsync(
            AutomationRequest request,
            CancellationToken cancellationToken);
    }

    public sealed record McpToolDescriptor(
        string ServerId,
        string ToolName,
        string Description,
        string JsonSchema,
        string RequiredScope);

    public sealed record McpToolCallRequest(
        McpToolDescriptor Tool,
        string ArgumentsJson,
        CapabilityType RequiredCapability = CapabilityType.PluginExecution);

    public sealed record McpToolCallResult(
        bool Succeeded,
        string ContentJson,
        string? Error = null);

    public interface IMcpToolGateway
    {
        Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken);

        Task<McpToolCallResult> CallToolAsync(
            McpToolCallRequest request,
            CancellationToken cancellationToken);
    }

    public sealed record SpeechRecognitionResult(
        string Text,
        bool IsFinal,
        string Language,
        TimeSpan AudioDuration);

    public interface ISpeechGateway
    {
        IAsyncEnumerable<SpeechRecognitionResult> RecognizeAsync(
            IAsyncEnumerable<ReadOnlyMemory<byte>> pcmChunks,
            string language,
            CancellationToken cancellationToken);

        Task<ReadOnlyMemory<byte>> SynthesizeAsync(
            string text,
            string language,
            CancellationToken cancellationToken);
    }

    public sealed record ScreenObservation(
        string Source,
        string? WindowTitle,
        string? AccessibleText,
        IReadOnlyList<ScreenTextRegion> TextRegions,
        DateTimeOffset CapturedAt);

    public sealed record ScreenTextRegion(
        string Text,
        int Left,
        int Top,
        int Width,
        int Height,
        float Confidence);

    public interface IScreenUnderstandingGateway
    {
        Task<ScreenObservation> ObserveAsync(
            string scope,
            CancellationToken cancellationToken);
    }

    public sealed record MissionTelemetryEvent(
        string MissionId,
        string Name,
        DateTimeOffset Timestamp,
        IReadOnlyDictionary<string, string> Attributes);

    public interface IMissionTelemetrySink
    {
        ValueTask RecordAsync(
            MissionTelemetryEvent telemetryEvent,
            CancellationToken cancellationToken = default);
    }
}
