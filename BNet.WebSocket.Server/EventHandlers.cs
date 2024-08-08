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

        public delegate void ReceivedEventHandler(ReceivedEventArgs e);
        public event ReceivedEventHandler OnReceived;
        public void SetOnReceived(string message)
        {
            OnReceived?.Invoke(new ReceivedEventArgs { Message = message });
        }
        #endregion

        #region OnConnectedClient
        public class ConnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public delegate void ConnectedClientEventHandler(ConnectedClientEventArgs e);
        public event ConnectedClientEventHandler OnConnectedClient;
        public void SetOnConnectedClient(int count)
        {
            OnConnectedClient?.Invoke(new ConnectedClientEventArgs { Count = count });
        }
        #endregion

        #region OnDisconnectedClient
        public class DisconnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public delegate void DisconnectedClientEventHandler(DisconnectedClientEventArgs e);
        public event DisconnectedClientEventHandler OnDisconnectedClient;
        public void SetOnDisconnectedClient(int count)
        {
            OnDisconnectedClient?.Invoke(new DisconnectedClientEventArgs { Count = count });
        }
        #endregion

        #region OnError
        public class ErrorEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        public delegate void ErrorEventHandler(ErrorEventArgs e);
        public event ErrorEventHandler OnError;
        public void SetOnError(string message)
        {
            OnError?.Invoke(new ErrorEventArgs { Message = message });
        }
        #endregion
    }
}
