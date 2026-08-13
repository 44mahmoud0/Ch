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

    public sealed record AutomationContext(
        string? MissionId = null,
        string? TaskId = null,
        string? TargetProcessName = null,
        int? TargetProcessId = null,
        bool IsGame = false,
        bool IsSensitive = false);

    public sealed record AutomationRequest(
        CapabilityType RequiredCapability,
        string Scope,
        AutomationOperation Operation,
        string Target,
        string? Payload = null,
        AutomationContext? Context = null);

    public interface IAutomationRiskPolicy
    {
        bool IsAllowed(AutomationRequest request, out string? reason);
    }

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
        string DeclaredScope,
        string ManifestId = "");

    public sealed record TrustedMcpToolManifest(
        string ManifestId,
        string ServerId,
        string ToolName,
        CapabilityType Capability,
        string ApprovedScope);

    public sealed record McpAuthorizationDecision(
        bool Allowed,
        CapabilityType Capability,
        string Scope,
        string? Reason = null);

    public interface IMcpToolPolicy
    {
        McpAuthorizationDecision Authorize(McpToolCallRequest request);
    }

    public sealed class ManifestMcpToolPolicy : IMcpToolPolicy
    {
        private readonly IReadOnlyDictionary<string, TrustedMcpToolManifest> _manifests;

        public ManifestMcpToolPolicy(IEnumerable<TrustedMcpToolManifest> manifests)
        {
            var map = new Dictionary<string, TrustedMcpToolManifest>(StringComparer.OrdinalIgnoreCase);
            foreach (var manifest in manifests)
            {
                var key = CreateKey(manifest.ManifestId, manifest.ServerId, manifest.ToolName);
                if (!map.TryAdd(key, manifest))
                {
                    throw new ArgumentException($"Duplicate MCP manifest key '{key}'.", nameof(manifests));
                }
            }

            _manifests = map;
        }

        public McpAuthorizationDecision Authorize(McpToolCallRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var key = CreateKey(request.Tool.ManifestId, request.Tool.ServerId, request.Tool.ToolName);
            if (!_manifests.TryGetValue(key, out var manifest))
            {
                return new McpAuthorizationDecision(false, CapabilityType.PluginExecution, string.Empty, "No trusted MCP manifest matched the tool identity.");
            }

            return new McpAuthorizationDecision(true, manifest.Capability, manifest.ApprovedScope);
        }

        private static string CreateKey(string manifestId, string serverId, string toolName)
        {
            return $"{manifestId}\u001f{serverId}\u001f{toolName}";
        }
    }

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
