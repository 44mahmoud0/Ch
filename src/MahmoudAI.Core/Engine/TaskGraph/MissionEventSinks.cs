using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class CompositeMissionEventSink : IMissionEventSink
    {
        private readonly IReadOnlyList<IMissionEventSink> _sinks;

        public CompositeMissionEventSink(IEnumerable<IMissionEventSink> sinks)
        {
            ArgumentNullException.ThrowIfNull(sinks);
            _sinks = new List<IMissionEventSink>(sinks);
        }

        public async ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            foreach (var sink in _sinks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await sink.EmitAsync(evt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public sealed class BufferedMissionEventSink : IMissionEventSink, IAsyncDisposable
    {
        private readonly Channel<MissionTaskEvent> _channel;
        private readonly Task _consumer;
        private readonly IMissionEventSink _inner;
        private readonly CancellationTokenSource _shutdown = new();

        public BufferedMissionEventSink(IMissionEventSink inner, int capacity = 2048)
        {
            ArgumentNullException.ThrowIfNull(inner);
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
            }

            _inner = inner;
            _channel = Channel.CreateBounded<MissionTaskEvent>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            _consumer = ConsumeAsync();
        }

        public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(evt, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Writer.TryComplete();
            await _consumer.ConfigureAwait(false);
            _shutdown.Cancel();
            _shutdown.Dispose();
        }

        private async Task ConsumeAsync()
        {
            try
            {
                await foreach (var evt in _channel.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
                {
                    await _inner.EmitAsync(evt, _shutdown.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }
    }
}
