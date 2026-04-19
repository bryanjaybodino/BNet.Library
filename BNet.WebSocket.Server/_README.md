# BNet.WebSocket.Server

A lightweight, easy-to-use WebSocket server library for .NET. Built on raw TCP with no heavy dependencies — just clean WebSocket handling with room support, SSL/TLS, binary frames, and async events.

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

server.OnBinaryReceived += (sender, e) =>
    Console.WriteLine($"Binary received: {e.Data.Length} bytes");

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

Rooms allow you to broadcast messages only to a specific group of connected clients. A client joins a room by appending a `room` query parameter to the WebSocket URL when connecting.

**URL format:**
```
ws://localhost:8080?room=value
```

**Examples:**
```
ws://localhost:8080?room=general
ws://localhost:8080?room=room42
ws://localhost:8080?room=updates
```

> The `room` query key is required — the value (e.g. `general`) becomes the room identifier used for targeted broadcasts.

**Sending a text message to a specific room from the server:**
```csharp
await server.SendMessageToRoomAsync("general", "Hello, room!");
```

**Sending a binary message to a specific room from the server:**
```csharp
await server.SendBinaryToRoomAsync("general", myByteArray);
```

**Clients connected without a room** receive all broadcast messages sent via `SendMessageAsync` or `SendBinaryAsync`.

---

## 📡 API Reference

### `Connection`

| Member | Description |
|---|---|
| `Connection(int port)` | Creates a server on the given port |
| `StartAsync()` | Starts listening for connections |
| `StopAsync()` | Gracefully stops the server and disconnects all clients |
| `SendMessageAsync(string message)` | Broadcasts a text message to **all** connected clients |
| `SendMessageToRoomAsync(string roomId, string message)` | Sends a text message to all clients in a specific room |
| `SendBinaryAsync(byte[] data)` | Broadcasts a binary message to **all** connected clients |
| `SendBinaryToRoomAsync(string roomId, byte[] data)` | Sends a binary message to all clients in a specific room |
| `LoadCertificate(string path, string password)` | Loads a TLS certificate from a `.pfx` file path |
| `LoadCertificate(byte[] rawData, string password)` | Loads a TLS certificate from raw bytes |
| `IsRunning` | `bool` — whether the server is currently active |

### Events (from `EventHandlers`)

| Event | Args | Description |
|---|---|---|
| `OnReceived` | `ReceivedEventArgs.Message` | Fired when a **text** message is received from any client |
| `OnBinaryReceived` | `BinaryReceivedEventArgs.Data` | Fired when a **binary** frame is received from any client; `Data` is the raw `byte[]` payload |
| `OnConnectedClient` | `ConnectedClientEventArgs.Count` | Fired when a client connects; includes current total count |
| `OnDisconnectedClient` | `DisconnectedClientEventArgs.Count` | Fired when a client disconnects; includes remaining count |
| `OnError` | `ErrorEventArgs.Message` | Fired when an internal error occurs |

---

## 📨 Binary Frames

The server natively handles WebSocket binary frames (opcode `0x02`). When a binary frame arrives from a client, the server:

1. **Immediately broadcasts** the payload back to the sender's room (or all clients if no room), using a proper binary WebSocket frame — so other clients receive it as `Blob` / `ArrayBuffer`, not text.
2. **Fires `OnBinaryReceived`** in the background, giving your server-side code access to the raw `byte[]` for side-effects such as saving to a database, sending push notifications, etc.

**Sending binary from the browser:**
```js
const socket = new WebSocket("ws://localhost:8080?room=general");

// Send a typed array
const buffer = new Uint8Array([1, 2, 3, 4]);
socket.send(buffer.buffer);

// Receive binary back
socket.binaryType = "arraybuffer";
socket.onmessage = (e) => {
    const view = new Uint8Array(e.data);
    console.log("Received binary:", view);
};
```

**Handling binary on the server:**
```csharp
server.OnBinaryReceived += (sender, e) =>
{
    // e.Data is the raw byte[] payload sent by the client
    Console.WriteLine($"Binary received: {e.Data.Length} bytes");
    // e.g. save to DB, process image, forward to another service...
};
```

**Sending binary from the server:**
```csharp
// Broadcast to all clients
byte[] payload = File.ReadAllBytes("data.bin");
await server.SendBinaryAsync(payload);

// Broadcast to a specific room
await server.SendBinaryToRoomAsync("general", payload);
```

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

- **Payload size:** The receive buffer processes a single read per frame. Very large payloads that exceed the TCP receive buffer size in a single read may be truncated. Keeping individual message payloads under **~35 KB** is recommended for reliability.
- **Text messages:** The `OnReceived` event delivers UTF-8 decoded strings. For structured data, consider serializing to JSON before sending.
- **Binary messages:** Binary frames are broadcast immediately and delivered as raw `byte[]` via `OnBinaryReceived`. Avoid sending very large binary payloads in a single frame — split them on the client side if needed.

---

## 🎬 Video Tutorial

Watch the setup walkthrough on YouTube:
[https://www.youtube.com/watch?v=1qIYJg2WNOk](https://www.youtube.com/watch?v=1qIYJg2WNOk)