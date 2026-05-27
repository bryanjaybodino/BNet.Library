using System;

namespace BNet.ZKTecoADMS
{
    public static class ZKTecoEventArgs
    {
        // ── Punch Type ─────────────────────────────────────────────────────────

        /// <summary>
        /// Logical punch type as resolved by the server.
        ///
        /// MB460 Plus firmware always sends PunchState=4 via ADMS Push.
        /// The actual punch type selected on the device is carried in the
        /// VerifyMode field (column [2]) and resolved by ResolvePunchType().
        /// </summary>
        public enum PunchType
        {
            /// <summary>Check-In (VerifyMode=0 on MB460 Plus, or PunchState=0 on standard firmware).</summary>
            CheckIn = 0,

            /// <summary>Check-Out (VerifyMode=1 on MB460 Plus, or PunchState=1 on standard firmware).</summary>
            CheckOut = 1,

            /// <summary>Overtime start (VerifyMode=2 on MB460 Plus, or PunchState=2 on standard firmware).</summary>
            OvertimeIn = 2,

            /// <summary>Overtime end (VerifyMode=3 on MB460 Plus, or PunchState=3 on standard firmware).</summary>
            OvertimeOut = 3,

            /// <summary>State could not be determined.</summary>
            Unknown = -1
        }

        // ── Handshake ──────────────────────────────────────────────────────────

        /// <summary>Raised when a ZKTeco device completes its initial handshake.</summary>
        public class HandshakeEventArgs : EventArgs
        {
            /// <summary>Serial number of the device.</summary>
            public string SN { get; set; }

            /// <summary>When the handshake was received.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                string.Format("[Handshake] SN={0} at {1:HH:mm:ss}", SN, Timestamp);
        }

        // ── Heartbeat ──────────────────────────────────────────────────────────

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
                string.Format("[Heartbeat] SN={0} Photos={1} at {2:HH:mm:ss}", SN, PhotoCount, Timestamp);
        }

        // ── Attendance punch ───────────────────────────────────────────────────

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
            /// Raw value of column [2] from the ATTLOG line.
            ///
            /// MB460 Plus: this field carries the punch type the employee selected
            /// on the device (0=CheckIn, 1=CheckOut, 2=OvertimeIn, 3=OvertimeOut).
            ///
            /// Standard firmware: this field is the authentication method
            /// (1=Fingerprint, 4=Password, 5=Palm, 15=Face, 255=Face extended).
            ///
            /// Use <see cref="PunchType"/> for the resolved logical punch type.
            /// Use <see cref="ZKTecoHelper.VerifyLabel"/> to get a readable label.
            /// </summary>
            public int VerifyMode { get; set; }

            /// <summary>
            /// Raw numeric punch state as received from the device (column [3]).
            /// MB460 Plus always sends 4 here regardless of the punch type set on the device.
            /// Standard firmware sends 0-3 directly.
            /// Use <see cref="PunchType"/> for the resolved logical type.
            /// </summary>
            public int PunchState { get; set; }

            // Backing field set explicitly by the server after resolving the punch type.
            private PunchType? _resolvedPunchType;

            /// <summary>
            /// Resolved logical punch type.
            /// Always set explicitly by the server via ResolvePunchType().
            /// Falls back to deriving from <see cref="PunchState"/> only if not set.
            /// </summary>
            public PunchType PunchType
            {
                get
                {
                    if (_resolvedPunchType.HasValue)
                        return _resolvedPunchType.Value;

                    switch (PunchState)
                    {
                        case 0: return ZKTecoEventArgs.PunchType.CheckIn;
                        case 1: return ZKTecoEventArgs.PunchType.CheckOut;
                        case 2: return ZKTecoEventArgs.PunchType.OvertimeIn;
                        case 3: return ZKTecoEventArgs.PunchType.OvertimeOut;
                        default: return ZKTecoEventArgs.PunchType.Unknown;
                    }
                }
                set { _resolvedPunchType = value; }
            }

            /// <summary>Work-code field (firmware-dependent; 0 when absent).</summary>
            public int WorkCode { get; set; }

            /// <summary>Original tab-delimited line from the device.</summary>
            public string RawLine { get; set; }

            /// <summary>When the record was received by the server.</summary>
            public DateTime Timestamp { get; set; }

            public override string ToString() =>
                string.Format("[Attendance] SN={0} User={1} Time={2:yyyy-MM-dd HH:mm:ss} Type={3} Verify={4}",
                    SN, UserId, PunchTime, PunchType, ZKTecoHelper.VerifyLabel(VerifyMode));
        }

        // ── Photo ──────────────────────────────────────────────────────────────

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
                    ? string.Format("[Photo] SN={0} User={1} -> {2}", SN, UserId, SavedPath)
                    : string.Format("[Photo] SN={0} User={1} FAILED to extract JPEG", SN, UserId);
        }

        // ── Error ──────────────────────────────────────────────────────────────

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
                string.Format("[Error] {0}: {1}", Source, Exception?.Message);
        }

        // ── Raw request (debug) ────────────────────────────────────────────────

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
                string.Format("[Raw] {0} {1} SN={2} Table={3} BodyLen={4}",
                    Method, Url, SN, Table, RawBody?.Length);
        }
    }
}