using System;

namespace BNet.WebSocket.Server
{
    public class EventHandlers
    {
        #region OnReceived
        public class ReceivedEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        public event EventHandler<ReceivedEventArgs> OnReceived;
        public void SetOnReceived(string message)
        {
            OnReceived?.Invoke(this, new ReceivedEventArgs { Message = message });
        }
        #endregion

        #region OnConnectedClient
        public class ConnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public event EventHandler<ConnectedClientEventArgs> OnConnectedClient;
        public void SetOnConnectedClient(int count)
        {
            OnConnectedClient?.Invoke(this, new ConnectedClientEventArgs { Count = count });
        }
        #endregion

        #region OnDisconnectedClient
        public class DisconnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public event EventHandler<DisconnectedClientEventArgs> OnDisconnectedClient;
        public void SetOnDisconnectedClient(int count)
        {
            OnDisconnectedClient?.Invoke(this, new DisconnectedClientEventArgs { Count = count });
        }
        #endregion

        #region OnError
        public class ErrorEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        public event EventHandler<ErrorEventArgs> OnError;
        public void SetOnError(string message)
        {
            OnError?.Invoke(this, new ErrorEventArgs { Message = message });
        }
        #endregion
    }
}
