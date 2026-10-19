using System.Globalization;
using System.Text;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>One raw sample from the controller; calibration happens in ControllerCalibration.</summary>
    public readonly struct ControllerPacket
    {
        /// <summary>Joystick axes, raw 0–1023.</summary>
        public readonly int StickX, StickY;
        /// <summary>Ultrasonic distance in millimetres, or -1 when the sensor got no echo.</summary>
        public readonly int DistanceMm;
        /// <summary>StickButton is the pause control: the stick push switch on v1, MENU on the cockpit (v2).</summary>
        public readonly bool Fire, StickButton;
        /// <summary>Cockpit (v2) only: twist axis raw 0–1023, EMP button, MASTER ARM switch.</summary>
        public readonly bool IsCockpit;
        public readonly int Twist;
        public readonly bool Emp, Armed;

        public ControllerPacket(int stickX, int stickY, int distanceMm, bool fire, bool stickButton)
            : this(stickX, stickY, distanceMm, fire, stickButton, false, 512, false, false) { }

        public ControllerPacket(int stickX, int stickY, int distanceMm, bool fire, bool stickButton,
            bool isCockpit, int twist, bool emp, bool armed)
        {
            StickX = stickX;
            StickY = stickY;
            DistanceMm = distanceMm;
            Fire = fire;
            StickButton = stickButton;
            IsCockpit = isCockpit;
            Twist = twist;
            Emp = emp;
            Armed = armed;
        }
    }

    /// <summary>
    /// Serial protocol v1 (Arduino/README.md). ASCII lines ending in newline, 115200 baud, with an
    /// NMEA-style checksum: XOR of every character before '*', written as two hex digits.
    ///   controller → PC, ~50 Hz:  AX,&lt;x 0-1023&gt;,&lt;y 0-1023&gt;,&lt;mm or -1&gt;,&lt;fire 0/1&gt;,&lt;stick 0/1&gt;*HH
    ///   cockpit (v2) appends:     ...,&lt;twist 0-1023&gt;,&lt;emp 0/1&gt;,&lt;arm 0/1&gt;*HH
    ///   PC → controller:          BZ,&lt;hz&gt;,&lt;ms&gt;*HH   (buzzer tone)
    ///   PC → controller:          LT,&lt;row 0/1&gt;,&lt;text&gt;*HH   (one LCD row, see FormatLcdRow)
    ///   PC → cockpit:             RM,&lt;level 0-255&gt;,&lt;ms&gt;*HH   (rumble, see FormatRumble)
    ///   PC → cockpit:             LA,&lt;0 off | 1 on | 2 blink&gt;*HH   (EMP dome lamp)
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
            if ((f.Length != 6 && f.Length != 9) || f[0] != "AX") return false;
            if (!TryInt(f[1], 0, 1023, out int x) || !TryInt(f[2], 0, 1023, out int y)
                || !TryInt(f[3], -1, MaxDistanceMm, out int mm)
                || !TryInt(f[4], 0, 1, out int fire) || !TryInt(f[5], 0, 1, out int stick))
                return false;

            if (f.Length == 6)
            {
                packet = new ControllerPacket(x, y, mm, fire == 1, stick == 1);
                return true;
            }
            if (!TryInt(f[6], 0, 1023, out int twist) || !TryInt(f[7], 0, 1, out int emp)
                || !TryInt(f[8], 0, 1, out int arm))
                return false;
            packet = new ControllerPacket(x, y, mm, fire == 1, stick == 1, true, twist, emp == 1, arm == 1);
            return true;
        }

        public static string FormatTone(int hz, int milliseconds) =>
            Frame(string.Format(CultureInfo.InvariantCulture, "BZ,{0},{1}", hz, milliseconds));

        /// <summary>Cockpit rumble, level 0–255. milliseconds = 0 sets the steady level, which the
        /// firmware drops 1.5 s after the last one, so resend it while it should keep running.
        /// milliseconds &gt; 0 is a pulse on top of the steady level (up to 2000 ms).</summary>
        public static string FormatRumble(int level, int milliseconds) =>
            Frame(string.Format(CultureInfo.InvariantCulture, "RM,{0},{1}",
                Mathf.Clamp(level, 0, 255), Mathf.Clamp(milliseconds, 0, 2000)));

        public enum Lamp { Off = 0, On = 1, Blink = 2 }

        /// <summary>Cockpit EMP dome lamp. The firmware keeps it lit on standby.</summary>
        public static string FormatLamp(Lamp mode) =>
            Frame(string.Format(CultureInfo.InvariantCulture, "LA,{0}", (int)mode));

        /// <summary>One LCD row. Glyphs (LcdGlyph) are sent as backslash escapes; other characters
        /// outside printable ASCII become '?'. Trailing spaces are dropped: the firmware pads the row.</summary>
        public static string FormatLcdRow(int row, string cells)
        {
            var body = new StringBuilder("LT,").Append(row == 0 ? '0' : '1').Append(',');
            string text = LcdGlyph.Fit(cells).TrimEnd(' ');
            foreach (char c in text)
            {
                if (c <= LcdGlyph.Target) body.Append('\\').Append((char)('0' + c));
                else if (c == LcdGlyph.Right) body.Append("\\>");
                else if (c == LcdGlyph.Left) body.Append("\\<");
                else if (c == LcdGlyph.Full) body.Append("\\#");
                else if (c == '\\') body.Append("\\\\");
                else body.Append(c >= ' ' && c <= '~' ? c : '?');
            }
            return Frame(body.ToString());
        }

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
