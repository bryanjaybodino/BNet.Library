# BNet.WebSocket.Server

A lightweight, easy-to-use WebSocket server library for .NET. Built on raw TCP with no heavy dependencies — just clean WebSocket handling with room support, SSL/TLS, and async events.

---

## 📦 Installation

Clone or reference the library directly from the GitHub repository:

- **Library source:** [BNet.WebSocket.Server](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.WebSocket.Server)
- **Sample project:** [BNet.WebSocket.Server.Sample](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.WebSocket.Server.Sample)

---

## 🚀 Quick Start

```csharp
using BNet.WebSocket.Server;

var server = new Connection(8080);

server.OnConnectedClient += (sender, e) =>
    Console.WriteLine($"Client connected. Total: {e.Count}");

server.OnDisconnectedClient += (sender, e) =>
    Console.WriteLine($"Client disconnected. Total: {e.Count}");

server.OnReceived += (sender, e) =>
    Console.WriteLine($"Message received: {e.Message}");

server.OnError += (sender, e) =>
    Console.WriteLine($"Error: {e.Message}");

await server.StartAsync();
```

---

## 🔌 Connecting from a Client

Connect using the standard browser WebSocket API or any WebSocket client library.

**Plain WebSocket (ws://):**
```js
const socket = new WebSocket("ws://localhost:8080");
```

**Secure WebSocket (wss://) — requires a loaded certificate:**
```js
const socket = new WebSocket("wss://yourdomain.com:8080");
```

---

## 🏠 Rooms

Rooms allow you to broadcast messages only to a specific group of connected clients. A client joins a room by appending a query string to the WebSocket URL when connecting.

**URL format:**
```
ws://localhost:8080?roomName=value
```

**Examples:**
```
ws://localhost:8080?chat=general
ws://localhost:8080?game=room42
ws://localhost:8080?channel=updates
```

> Any query key and value are supported — the full query string (e.g. `?chat=general`) becomes the room identifier.

**Sending a message to a specific room from the server:**
```csharp
await server.SendMessageToRoomAsync("?chat=general", "Hello, room!");
```

**Clients connected without a room** receive all broadcast messages sent via `SendMessageAsync`.

---

## 📡 API Reference

### `Connection`

| Member | Description |
|---|---|
| `Connection(int port)` | Creates a server on the given port |
| `StartAsync()` | Starts listening for connections |
| `StopAsync()` | Gracefully stops the server and disconnects all clients |
| `SendMessageAsync(string message)` | Broadcasts a message to **all** connected clients |
| `SendMessageToRoomAsync(string roomId, string message)` | Sends a message to all clients in a specific room |
| `LoadCertificate(string path, string password)` | Loads a TLS certificate from a `.pfx` file path |
| `LoadCertificate(byte[] rawData, string password)` | Loads a TLS certificate from raw bytes |
| `IsRunning` | `bool` — whether the server is currently active |

### Events (from `EventHandlers`)

| Event | Args | Description |
|---|---|---|
| `OnReceived` | `ReceivedEventArgs.Message` | Fired when a message is received from any client |
| `OnConnectedClient` | `ConnectedClientEventArgs.Count` | Fired when a client connects; includes current total count |
| `OnDisconnectedClient` | `DisconnectedClientEventArgs.Count` | Fired when a client disconnects; includes remaining count |
| `OnError` | `ErrorEventArgs.Message` | Fired when an internal error occurs |

---

## 🔒 SSL / TLS Support

To enable secure WebSocket (`wss://`), load a certificate before starting the server:

```csharp
var server = new Connection(8443);
server.LoadCertificate("path/to/certificate.pfx", "your-password");

await server.StartAsync();
```

The server uses **TLS 1.2** and handles the SSL handshake automatically for each incoming connection. If no certificate is loaded, the server runs in plain `ws://` mode.

---

## ⚠️ Limitations

- **Base64 / binary image data:** Not recommended. Due to WebSocket framing constraints, only payloads up to approximately **35 KB** are reliably supported. Larger binary payloads (e.g. full-resolution images as Base64 strings) may be truncated or fail.
- **Text messages only:** The server is optimized for UTF-8 text frames. Binary frames are accepted but decoded as UTF-8.

---

## 🎬 Video Tutorial

Watch the setup walkthrough on YouTube:
[https://www.youtube.com/watch?v=1qIYJg2WNOk](https://www.youtube.com/watch?v=1qIYJg2WNOk)
