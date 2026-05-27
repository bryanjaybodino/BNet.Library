using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BNet.ZKTecoADMS
{
    public class ZKTecoEventArgs
    {
        // ── Handshake ──────────────────────────────────────────────────────────────

        /// <summary>Raised when a ZKTeco device completes its initial handshake.</summary>
        public class HandshakeEventArgs : EventArgs
        {
            /// <summary>Serial number of the device.</summary>
            public string SN { get; set; }

            /// <summary>When the handshake was received.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                $"[Handshake] SN={SN} at {Timestamp:HH:mm:ss}";
        }

        // ── Heartbeat ──────────────────────────────────────────────────────────────

        /// <summary>Raised on every device heartbeat that carries an INFO payload.</summary>
        public class HeartbeatEventArgs : EventArgs
        {
            /// <summary>Serial number of the device.</summary>
            public string SN { get; set; }

            /// <summary>Raw INFO string from the query string.</summary>
            public string Info { get; set; }

            /// <summary>Number of pending attendance photos on the device.</summary>
            public int PhotoCount { get; set; }

            /// <summary>When the heartbeat was received.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                $"[Heartbeat] SN={SN} Photos={PhotoCount} at {Timestamp:HH:mm:ss}";
        }

        // ── Attendance punch ───────────────────────────────────────────────────────

        /// <summary>Raised for every attendance record in an ATTLOG upload.</summary>
        public class AttendanceEventArgs : EventArgs
        {
            /// <summary>Serial number of the device that sent the record.</summary>
            public string SN { get; set; }

            /// <summary>Employee / enrolled user ID.</summary>
            public string UserId { get; set; }

            /// <summary>Parsed punch date and time.</summary>
            public DateTime PunchTime { get; set; }

            /// <summary>Raw time string as received from the device.</summary>
            public string RawTime { get; set; }

            /// <summary>
            /// Verification mode reported by the device.
            /// Common values: 1=Fingerprint, 4=Password, 15=Face, -1=Unknown.
            /// </summary>
            public int VerifyMode { get; set; }

            /// <summary>Work-code field (firmware-dependent; 0 when absent).</summary>
            public int WorkCode { get; set; }

            /// <summary>Original tab-delimited line from the device.</summary>
            public string RawLine { get; set; }

            /// <summary>When the record was received by the server.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                $"[Attendance] SN={SN} User={UserId} Time={PunchTime:yyyy-MM-dd HH:mm:ss} Verify={VerifyMode}";
        }

        // ── Photo ──────────────────────────────────────────────────────────────────

        /// <summary>Raised when an attendance photo upload is processed.</summary>
        public class PhotoEventArgs : EventArgs
        {
            /// <summary>Serial number of the device.</summary>
            public string SN { get; set; }

            /// <summary>User ID extracted from the PIN filename.</summary>
            public string UserId { get; set; }

            /// <summary>
            /// Full path where the JPEG was saved.
            /// <c>null</c> if <see cref="Success"/> is <c>false</c>.
            /// </summary>
            public string SavedPath { get; set; }

            /// <summary>
            /// Raw JPEG bytes as received from the device.
            /// Useful if you want to save / process the image yourself
            /// without relying on the auto-save behaviour.
            /// </summary>
            public byte[] ImageBytes { get; set; }

            /// <summary>True if the JPEG was extracted and saved successfully.</summary>
            public bool Success { get; set; }

            /// <summary>When the photo was received.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                Success
                    ? $"[Photo] SN={SN} User={UserId} → {SavedPath}"
                    : $"[Photo] SN={SN} User={UserId} FAILED to extract JPEG";
        }

        // ── Error ──────────────────────────────────────────────────────────────────

        /// <summary>Raised when a non-fatal internal error occurs.</summary>
        public class ErrorEventArgs : EventArgs
        {
            /// <summary>Descriptive source / context of the error.</summary>
            public string Source { get; set; }

            /// <summary>The exception that was caught.</summary>
            public Exception Exception { get; set; }

            /// <summary>When the error occurred.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                $"[Error] {Source}: {Exception?.Message}";
        }

        // ── Raw request (debug) ────────────────────────────────────────────────────

        /// <summary>
        /// Raised for every incoming HTTP request before any parsing.
        /// Useful for low-level debugging or custom protocol extensions.
        /// </summary>
        public class RawRequestEventArgs : EventArgs
        {
            public string SN { get; set; }
            public string Method { get; set; }
            public string Url { get; set; }
            public string Table { get; set; }
            public string Body { get; set; }
            public byte[] RawBody { get; set; }
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                $"[Raw] {Method} {Url} SN={SN} Table={Table} BodyLen={RawBody?.Length}";
        }
    }
}