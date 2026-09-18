using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace BNet.WebSocket.Server
{
    public class EventHandlers
    {
        public class ReceivedEventArgs : EventArgs { public string Message { get; set; } }
        public class BinaryReceivedEventArgs : EventArgs { public byte[] Data { get; set; } }
        public class ConnectedClientEventArgs : EventArgs { public int Count { get; set; } }
        public class DisconnectedClientEventArgs : EventArgs { public int Count { get; set; } }
        public class ErrorEventArgs : EventArgs { public string Message { get; set; } }

        private readonly ConcurrentDictionary<Guid, EventHandler<ReceivedEventArgs>> _onReceivedHandlers = new ConcurrentDictionary<Guid, EventHandler<ReceivedEventArgs>>();
        private readonly ConcurrentDictionary<Guid, EventHandler<BinaryReceivedEventArgs>> _onBinaryReceivedHandlers = new ConcurrentDictionary<Guid, EventHandler<BinaryReceivedEventArgs>>();
        private readonly ConcurrentDictionary<Guid, EventHandler<ConnectedClientEventArgs>> _onConnectedClientHandlers = new ConcurrentDictionary<Guid, EventHandler<ConnectedClientEventArgs>>();
        private readonly ConcurrentDictionary<Guid, EventHandler<DisconnectedClientEventArgs>> _onDisconnectedClientHandlers = new ConcurrentDictionary<Guid, EventHandler<DisconnectedClientEventArgs>>();
        private readonly ConcurrentDictionary<Guid, EventHandler<ErrorEventArgs>> _onErrorHandlers = new ConcurrentDictionary<Guid, EventHandler<ErrorEventArgs>>();

        public event EventHandler<ReceivedEventArgs> OnReceived
        {
            add => _onReceivedHandlers[Guid.NewGuid()] = value;
            remove { var item = _onReceivedHandlers.FirstOrDefault(k => k.Value == value); if (item.Key != Guid.Empty) _onReceivedHandlers.TryRemove(item.Key, out _); }
        }

        public event EventHandler<BinaryReceivedEventArgs> OnBinaryReceived
        {
            add => _onBinaryReceivedHandlers[Guid.NewGuid()] = value;
            remove { var item = _onBinaryReceivedHandlers.FirstOrDefault(k => k.Value == value); if (item.Key != Guid.Empty) _onBinaryReceivedHandlers.TryRemove(item.Key, out _); }
        }

        public event EventHandler<ConnectedClientEventArgs> OnConnectedClient
        {
            add => _onConnectedClientHandlers[Guid.NewGuid()] = value;
            remove { var item = _onConnectedClientHandlers.FirstOrDefault(k => k.Value == value); if (item.Key != Guid.Empty) _onConnectedClientHandlers.TryRemove(item.Key, out _); }
        }

        public event EventHandler<DisconnectedClientEventArgs> OnDisconnectedClient
        {
            add => _onDisconnectedClientHandlers[Guid.NewGuid()] = value;
            remove { var item = _onDisconnectedClientHandlers.FirstOrDefault(k => k.Value == value); if (item.Key != Guid.Empty) _onDisconnectedClientHandlers.TryRemove(item.Key, out _); }
        }

        public event EventHandler<ErrorEventArgs> OnError
        {
            add => _onErrorHandlers[Guid.NewGuid()] = value;
            remove { var item = _onErrorHandlers.FirstOrDefault(k => k.Value == value); if (item.Key != Guid.Empty) _onErrorHandlers.TryRemove(item.Key, out _); }
        }

        protected async Task SetOnReceived(string message)
        {
            var args = new ReceivedEventArgs { Message = message };
            await Task.WhenAll(_onReceivedHandlers.Values.Select(h => Task.Run(() => h?.Invoke(this, args))));
        }

        protected async Task SetOnBinaryReceived(byte[] data)
        {
            var args = new BinaryReceivedEventArgs { Data = data };
            await Task.WhenAll(_onBinaryReceivedHandlers.Values.Select(h => Task.Run(() => h?.Invoke(this, args))));
        }

        protected async Task SetOnConnectedClient(int count)
        {
            var args = new ConnectedClientEventArgs { Count = count };
            await Task.WhenAll(_onConnectedClientHandlers.Values.Select(h => Task.Run(() => h?.Invoke(this, args))));
        }

        protected async Task SetOnDisconnectedClient(int count)
        {
            var args = new DisconnectedClientEventArgs { Count = count };
            await Task.WhenAll(_onDisconnectedClientHandlers.Values.Select(h => Task.Run(() => h?.Invoke(this, args))));
        }

        protected async Task SetOnError(string message)
        {
            var args = new ErrorEventArgs { Message = message };
            await Task.WhenAll(_onErrorHandlers.Values.Select(h => Task.Run(() => h?.Invoke(this, args))));
        }
    }
}