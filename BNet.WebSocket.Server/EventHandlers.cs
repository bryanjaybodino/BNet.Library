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
        public event ReceivedEventHandler Received;
        public void OnReceived(string message)
        {
            Received?.Invoke(new ReceivedEventArgs { Message = message });
        }
        #endregion

        #region OnConnectedClient
        public class ConnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public delegate void ConnectedClientEventHandler(ConnectedClientEventArgs e);
        public event ConnectedClientEventHandler ConnectedClient;
        public void OnConnectedClient(int count)
        {
            ConnectedClient?.Invoke(new ConnectedClientEventArgs { Count = count });
        }
        #endregion

        #region OnDisconnectedClient
        public class DisconnectedClientEventArgs : EventArgs
        {
            public int Count { get; set; }
        }

        public delegate void DisconnectedClientEventHandler(DisconnectedClientEventArgs e);
        public event DisconnectedClientEventHandler DisconnectedClient;
        public void OnDisconnectedClient(int count)
        {
            DisconnectedClient?.Invoke(new DisconnectedClientEventArgs { Count = count });
        }
        #endregion
    }
}
