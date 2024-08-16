using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BNet.WebSocket.Server
{
    public class EventHandlers
    {
        #region OnReceived
        public class ReceivedEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, EventHandler<ReceivedEventArgs>> _onReceivedHandlers = new ConcurrentDictionary<Guid, EventHandler<ReceivedEventArgs>>();

        public event EventHandler<ReceivedEventArgs> OnReceived
        {
            add
            {
                var key = Guid.NewGuid();
                _onReceivedHandlers[key] = value;
            }
            remove
            {
                var itemToRemove = _onReceivedHandlers.FirstOrDefault(kvp => kvp.Value == value);
                if (itemToRemove.Key != Guid.Empty)
                {
                    _onReceivedHandlers.TryRemove(itemToRemove.Key, out _);
                }
            }
        }

        public async Task SetOnReceived(string message)
        {
            var args = new ReceivedEventArgs { Message = message };
            var handlers = _onReceivedHandlers.Values.ToList();

            var tasks = handlers.Select(handler =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        handler?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking OnReceived handler: {ex.Message}");
                    }
                });
            });

            await Task.WhenAll(tasks);
        }
        #endregion

        #region OnConnectedClient
        public class ConnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, EventHandler<ConnectedClientEventArgs>> _onConnectedClientHandlers = new ConcurrentDictionary<Guid, EventHandler<ConnectedClientEventArgs>>();

        public event EventHandler<ConnectedClientEventArgs> OnConnectedClient
        {
            add
            {
                var key = Guid.NewGuid();
                _onConnectedClientHandlers[key] = value;
            }
            remove
            {
                var itemToRemove = _onConnectedClientHandlers.FirstOrDefault(kvp => kvp.Value == value);
                if (itemToRemove.Key != Guid.Empty)
                {
                    _onConnectedClientHandlers.TryRemove(itemToRemove.Key, out _);
                }
            }
        }

        public async Task SetOnConnectedClient(int count)
        {
            var args = new ConnectedClientEventArgs { Count = count };
            var handlers = _onConnectedClientHandlers.Values.ToList();

            var tasks = handlers.Select(handler =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        handler?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking OnConnectedClient handler: {ex.Message}");
                    }
                });
            });

            await Task.WhenAll(tasks);
        }
        #endregion

        #region OnDisconnectedClient
        public class DisconnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, EventHandler<DisconnectedClientEventArgs>> _onDisconnectedClientHandlers = new ConcurrentDictionary<Guid, EventHandler<DisconnectedClientEventArgs>>();

        public event EventHandler<DisconnectedClientEventArgs> OnDisconnectedClient
        {
            add
            {
                var key = Guid.NewGuid();
                _onDisconnectedClientHandlers[key] = value;
            }
            remove
            {
                var itemToRemove = _onDisconnectedClientHandlers.FirstOrDefault(kvp => kvp.Value == value);
                if (itemToRemove.Key != Guid.Empty)
                {
                    _onDisconnectedClientHandlers.TryRemove(itemToRemove.Key, out _);
                }
            }
        }

        public async Task SetOnDisconnectedClient(int count)
        {
            var args = new DisconnectedClientEventArgs { Count = count };
            var handlers = _onDisconnectedClientHandlers.Values.ToList();

            var tasks = handlers.Select(handler =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        handler?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking OnDisconnectedClient handler: {ex.Message}");
                    }
                });
            });

            await Task.WhenAll(tasks);
        }
        #endregion

        #region OnError
        public class ErrorEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, EventHandler<ErrorEventArgs>> _onErrorHandlers = new ConcurrentDictionary<Guid, EventHandler<ErrorEventArgs>>();

        public event EventHandler<ErrorEventArgs> OnError
        {
            add
            {
                var key = Guid.NewGuid();
                _onErrorHandlers[key] = value;
            }
            remove
            {
                var itemToRemove = _onErrorHandlers.FirstOrDefault(kvp => kvp.Value == value);
                if (itemToRemove.Key != Guid.Empty)
                {
                    _onErrorHandlers.TryRemove(itemToRemove.Key, out _);
                }
            }
        }

        public async Task SetOnError(string message)
        {
            var args = new ErrorEventArgs { Message = message };
            var handlers = _onErrorHandlers.Values.ToList();

            var tasks = handlers.Select(handler =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        handler?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking OnError handler: {ex.Message}");
                    }
                });
            });

            await Task.WhenAll(tasks);
        }
        #endregion
    }
}
