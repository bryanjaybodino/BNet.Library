# ZKTecoADMS

A lightweight, async, event-driven HTTP server that speaks the **ZKTeco ADMS / Push protocol**. Built on raw `HttpListener` with no third-party dependencies — just clean attendance data handling with photo support, heartbeat monitoring, and async events.

---

## 📦 Installation

Clone or reference the library directly from the GitHub repository:

- **Library source:** [BNet.ZKTecoADMS](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ZKTecoADMS)
- **Sample project:** [BNet.ZKTecoADMS.Sample](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ZKTecoADMS.Sample)

---

## 🚀 Quick Start

```csharp
using ZKTecoADMS;

var server = new ZKTecoServer
{
    Port               = 4780,
    PhotoSaveDirectory = @"C:\ZKPhotos",
    Delay              = 10,
    ErrorDelay         = 30,
    TimeZone           = 8
};

server.OnHandshake += (sender, e) =>
    Console.WriteLine($"Device connected: {e.SN}");

server.OnAttendance += (sender, e) =>
    Console.WriteLine($"Punch: User={e.UserId} Time={e.PunchTime} Verify={e.VerifyMode}");

server.OnPhotoReceived += (sender, e) =>
    Console.WriteLine($"Photo saved: {e.SavedPath}");

server.OnHeartbeat += (sender, e) =>
    Console.WriteLine($"Heartbeat from {e.SN} — pending photos: {e.PhotoCount}");

server.OnError += (sender, e) =>
    Console.WriteLine($"Error [{e.Source}]: {e.Exception?.Message}");

await server.StartAsync();
```

---

## 🔌 Pointing Your ZKTeco Device

On the device admin panel, set the **Push Server** (ADMS/Cloud) address to your server's IP and port.

**Plain HTTP:**
```
http://192.168.1.100:4780
```

**Common menu path on the device:**
```
Menu → Comm → Cloud Server Settings → Server Address + Port
```

> The device will call `/iclock/cdata` for handshake, `/iclock/getrequest` for heartbeats, and POST to `/iclock/cdata?table=ATTLOG` / `ATTPHOTO` to push records.

---

## 📡 API Reference

### `ZKTecoServer`

| Member | Description |
|---|---|
| `ZKTecoServer()` | Creates a server instance with default settings |
| `StartAsync(CancellationToken)` | Starts listening for device connections asynchronously |
| `Start(CancellationToken)` | Fire-and-forget synchronous wrapper for `StartAsync` |
| `StopAsync()` | Gracefully stops the server |
| `Stop()` | Synchronous wrapper for `StopAsync` |
| `Port` | TCP port to listen on (default `4780`) |
| `PhotoSaveDirectory` | Directory where attendance photos are saved (default `C:\ZKPhotos`) |
| `PhotoStamp` | `ATTPHOTOSTAMP` sent during handshake; `0` = resend all photos |
| `Delay` | Seconds between device push cycles (default `10`) |
| `ErrorDelay` | Seconds the device waits after a failure (default `30`) |
| `TimeZone` | Device timezone offset in hours (default `8` = UTC+8) |

### Events

| Event | Args | Description |
|---|---|---|
| `OnHandshake` | `HandshakeEventArgs` | Fired when a device completes its initial handshake |
| `OnAttendance` | `AttendanceEventArgs` | Fired for every punch record in an ATTLOG upload |
| `OnPhotoReceived` | `PhotoEventArgs` | Fired when an attendance photo is processed |
| `OnHeartbeat` | `HeartbeatEventArgs` | Fired on every device heartbeat |
| `OnError` | `ErrorEventArgs` | Fired when a non-fatal internal error occurs |
| `OnRawRequest` | `RawRequestEventArgs` | Fired for every raw HTTP request — useful for debugging |

---

## 📋 Event Args Reference

### `HandshakeEventArgs`

| Property | Type | Description |
|---|---|---|
| `SN` | `string` | Device serial number |
| `Timestamp` | `DateTime` | When the handshake was received |

### `HeartbeatEventArgs`

| Property | Type | Description |
|---|---|---|
| `SN` | `string` | Device serial number |
| `Info` | `string` | Raw INFO string from the device query string |
| `PhotoCount` | `int` | Number of pending attendance photos on the device |
| `Timestamp` | `DateTime` | When the heartbeat was received |

### `AttendanceEventArgs`

| Property | Type | Description |
|---|---|---|
| `SN` | `string` | Device serial number |
| `UserId` | `string` | Enrolled employee / user ID |
| `PunchTime` | `DateTime` | Parsed punch date and time |
| `RawTime` | `string` | Raw time string as received from the device |
| `VerifyMode` | `int` | `1`=Fingerprint, `4`=Password, `15`=Face, `-1`=Unknown |
| `PunchState` | `int` | Raw punch state from the device (column [3]) |
| `PunchType` | `PunchType` | Resolved logical punch type (CheckIn, CheckOut, OvertimeIn, OvertimeOut) |
| `WorkCode` | `int` | Work-code field (firmware-dependent; `0` when absent) |
| `RawLine` | `string` | Original tab-delimited line from the device |
| `Timestamp` | `DateTime` | When the record was received by the server |

#### `PunchType` Enum

| Value | Description |
|---|---|
| `CheckIn` | Check-In (`VerifyMode=0` on MB460 Plus, or `PunchState=0` on standard firmware) |
| `CheckOut` | Check-Out (`VerifyMode=1` on MB460 Plus, or `PunchState=1` on standard firmware) |
| `OvertimeIn` | Overtime start (`VerifyMode=4` on MB460 Plus, or `PunchState=2` on standard firmware) |
| `OvertimeOut` | Overtime end (`VerifyMode=5` on MB460 Plus, or `PunchState=3` on standard firmware) |
| `Unknown` | State could not be determined (`-1`) |

> **MB460 Plus firmware note:** This device always sends `PunchState=4` via ADMS Push. The actual punch type selected on the device is carried in the `VerifyMode` field and resolved automatically by the server.

### `PhotoEventArgs`

| Property | Type | Description |
|---|---|---|
| `SN` | `string` | Device serial number |
| `UserId` | `string` | User ID extracted from the photo filename |
| `SavedPath` | `string?` | Full path where the JPEG was saved; `null` on failure |
| `ImageBytes` | `byte[]?` | Raw JPEG bytes — process them yourself if needed |
| `Success` | `bool` | `true` if the JPEG was extracted and saved successfully |
| `Timestamp` | `DateTime` | When the photo was received |

### `ErrorEventArgs`

| Property | Type | Description |
|---|---|---|
| `Source` | `string` | Descriptive context of where the error occurred |
| `Exception` | `Exception` | The exception that was caught |
| `Timestamp` | `DateTime` | When the error occurred |

### `RawRequestEventArgs`

| Property | Type | Description |
|---|---|---|
| `SN` | `string` | Device serial number |
| `Method` | `string` | HTTP method (`GET` / `POST`) |
| `Url` | `string` | Raw request URL including query string |
| `Table` | `string` | Value of the `table` query parameter (`ATTLOG`, `ATTPHOTO`, etc.) |
| `Body` | `string` | UTF-8 decoded request body |
| `RawBody` | `byte[]` | Raw request body bytes |
| `Timestamp` | `DateTime` | When the request was received |

---

## 🔤 `ZKTecoHelper`

### `VerifyLabel(int verifyMode)`

Returns a human-readable label for a `VerifyMode` value.

| Value | Label | Notes |
|---|---|---|
| `0` | `CheckIn` | MB460 Plus punch type |
| `1` | `CheckOut` | MB460 Plus punch type |
| `4` | `OvertimeIn` | MB460 Plus punch type |
| `5` | `OvertimeOut` | MB460 Plus punch type |
| `15` | `Face` | Standard firmware |
| `255` | `Face(FF)` | Standard firmware — face extended (0xFF) |
| other | `Mode:{n}` | Unrecognized value |

---

## 📸 Attendance Photos

The server automatically extracts and saves JPEG photos from `ATTPHOTO` uploads. When a device sends a photo:

1. **Extracts the JPEG** by scanning for the `FF D8 FF` magic bytes after the `CMD=uploadphoto` marker.
2. **Saves the file** to `PhotoSaveDirectory` with the filename format `{userId}_{yyyy-MM-dd_HHmmss}.jpg`.
3. **Fires `OnPhotoReceived`** with the saved path and raw bytes so you can do additional processing.

**Accessing the raw bytes** (e.g. to save to a database instead of disk):

```csharp
server.OnPhotoReceived += (sender, e) =>
{
    if (e.Success && e.ImageBytes != null)
    {
        // e.g. upload to cloud storage, store in DB blob column...
        await myStorage.UploadAsync(e.UserId, e.ImageBytes);
    }
};
```

**Controlling which photos the device sends** via `PhotoStamp`:

```csharp
// 0 = resend every photo from the beginning (default)
server.PhotoStamp = 0;

// Set to a specific stamp value to only receive photos newer than that point
server.PhotoStamp = 1234567890;
```

---

## 🛑 Graceful Shutdown

Use a `CancellationToken` to stop the server cleanly:

```csharp
using var cts = new CancellationTokenSource();

// Start in background
server.Start(cts.Token);

// Later — stop gracefully
cts.Cancel();
await server.StopAsync();
```

Or use `using` for automatic cleanup (on .NET Core 3+ / .NET 5+):

```csharp
await using var server = new ZKTecoServer { Port = 4780 };
await server.StartAsync();
```

---

## 🔍 Raw Request Debugging

Subscribe to `OnRawRequest` to inspect every HTTP call from the device before any parsing occurs:

```csharp
server.OnRawRequest += (sender, e) =>
    Console.WriteLine($"{e.Method} {e.Url} — body {e.RawBody?.Length} bytes");
```

---

## ⚠️ Limitations

- **`HttpListener` requires elevated privileges** on Windows when binding to a non-localhost prefix. Run as Administrator, or register the URL prefix with `netsh`:
  ```
  netsh http add urlacl url=http://+:4780/ user=DOMAIN\username
  ```
- **Single-read per request:** Very large photo payloads that exceed the TCP receive buffer in a single read may be truncated. Keeping individual photo sizes under **~35 KB** is recommended.
- **No built-in TLS:** The ADMS Push protocol typically runs over plain HTTP on the local network. If you need HTTPS, place a reverse proxy (nginx, IIS, Caddy) in front.
- **`netstandard2.0` / `netstandard2.1`** targets require the `System.Net.HttpListener` NuGet package — this is included automatically when referencing this library.