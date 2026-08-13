using System;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public interface IMissionEventSink
    {
        ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default);
    }

    public sealed class DelegateMissionEventSink : IMissionEventSink
    {
        private readonly Action<MissionTaskEvent> _handler;

        public DelegateMissionEventSink(Action<MissionTaskEvent> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _handler(evt);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class NullMissionEventSink : IMissionEventSink
    {
        public static NullMissionEventSink Instance { get; } = new();

        private NullMissionEventSink()
        {
        }

        public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
