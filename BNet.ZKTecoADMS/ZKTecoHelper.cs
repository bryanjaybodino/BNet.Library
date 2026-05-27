namespace BNet.ZKTecoADMS
{
    /// <summary>
    /// Utility helpers for the ZKTeco ADMS library.
    /// </summary>
    public static class ZKTecoHelper
    {
        /// <summary>
        /// Returns a human-readable label for a ZKTeco VerifyMode value.
        ///
        /// MB460 Plus firmware note:
        ///   On this device the VerifyMode field (column [2]) carries the punch
        ///   type selected by the employee, NOT the authentication method:
        ///     0 = Check-In
        ///     1 = Check-Out
        ///     2 = Overtime-In
        ///     3 = Overtime-Out
        ///
        /// Standard firmware uses this field for the authentication method:
        ///     4 = Password
        ///     5 = Palm / Vein scan
        ///    15 = Face
        ///   255 = Face extended (0xFF)
        /// </summary>
        public static string VerifyLabel(int verifyMode)
        {
            switch (verifyMode)
            {
                case 0: return "CheckIn";
                case 1: return "CheckOut";
                case 2: return "OvertimeIn";
                case 3: return "OvertimeOut";
                case 4: return "Password";
                case 5: return "Palm";
                case 15: return "Face";
                case 255: return "Face(FF)";
                default: return "Mode:" + verifyMode;
            }
        }
    }
}