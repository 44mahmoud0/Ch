using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class MissionEventHub : IMissionEventSink
    {
        private readonly ConcurrentDictionary<Guid, Func<MissionTaskEvent, ValueTask>> _subscriptions = new();

        public IDisposable Subscribe(Func<MissionTaskEvent, ValueTask> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            var subscriptionId = Guid.NewGuid();
            _subscriptions[subscriptionId] = handler;
            return new Subscription(() => _subscriptions.TryRemove(subscriptionId, out _));
        }

        public async ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _subscriptions.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler(evt).ConfigureAwait(false);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _unsubscribe;

            public Subscription(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
            }
        }
    }
}
