using System.Globalization;

namespace GeometricFlight.Input
{
    /// <summary>One raw sample from the controller; calibration happens in ControllerCalibration.</summary>
    public readonly struct ControllerPacket
    {
        /// <summary>Joystick axes, raw 0–1023.</summary>
        public readonly int StickX, StickY;
        /// <summary>Ultrasonic distance in millimetres, or -1 when the sensor got no echo.</summary>
        public readonly int DistanceMm;
        public readonly bool Fire, StickButton;

        public ControllerPacket(int stickX, int stickY, int distanceMm, bool fire, bool stickButton)
        {
            StickX = stickX;
            StickY = stickY;
            DistanceMm = distanceMm;
            Fire = fire;
            StickButton = stickButton;
        }
    }

    /// <summary>
    /// Serial protocol v1 (Arduino/README.md). ASCII lines ending in newline, 115200 baud, with an
    /// NMEA-style checksum: XOR of every character before '*', written as two hex digits.
    ///   controller → PC, ~50 Hz:  AX,&lt;x 0-1023&gt;,&lt;y 0-1023&gt;,&lt;mm or -1&gt;,&lt;fire 0/1&gt;,&lt;stick 0/1&gt;*HH
    ///   PC → controller:          BZ,&lt;hz&gt;,&lt;ms&gt;*HH   (buzzer tone)
    /// Anything that fails the checksum, field count, or range checks is rejected.
    /// </summary>
    public static class ControllerProtocol
    {
        public const int BaudRate = 115200;
        public const int MaxDistanceMm = 4000;

        public static bool TryParse(string line, out ControllerPacket packet)
        {
            packet = default;
            if (string.IsNullOrEmpty(line)) return false;
            line = line.Trim();
            int star = line.LastIndexOf('*');
            if (star < 0 || star != line.Length - 3) return false;

            string body = line.Substring(0, star);
            if (!byte.TryParse(line.Substring(star + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out byte expected) || Checksum(body) != expected)
                return false;

            string[] f = body.Split(',');
            if (f.Length != 6 || f[0] != "AX") return false;
            if (!TryInt(f[1], 0, 1023, out int x) || !TryInt(f[2], 0, 1023, out int y)
                || !TryInt(f[3], -1, MaxDistanceMm, out int mm)
                || !TryInt(f[4], 0, 1, out int fire) || !TryInt(f[5], 0, 1, out int stick))
                return false;

            packet = new ControllerPacket(x, y, mm, fire == 1, stick == 1);
            return true;
        }

        public static string FormatTone(int hz, int milliseconds) =>
            Frame(string.Format(CultureInfo.InvariantCulture, "BZ,{0},{1}", hz, milliseconds));

        /// <summary>Appends '*' and the checksum to a message body.</summary>
        public static string Frame(string body) =>
            body + "*" + Checksum(body).ToString("X2", CultureInfo.InvariantCulture);

        public static byte Checksum(string body)
        {
            byte checksum = 0;
            foreach (char c in body) checksum ^= (byte)c;
            return checksum;
        }

        private static bool TryInt(string s, int min, int max, out int value) =>
            int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
            && value >= min && value <= max;
    }
}
