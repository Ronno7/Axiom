using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace GeometricFlight.Input
{
    /// <summary>A line-based connection to a controller (serial port or simulator).</summary>
    public interface ILineTransport : IDisposable
    {
        string Name { get; }
        /// <summary>Returns false with a reason, for example when another program holds the port.</summary>
        bool Open(out string error);
        /// <summary>Blocks briefly; returns null if nothing arrived. Throws if the device is gone.</summary>
        string ReadLine();
        void WriteLine(string line);
    }

    public sealed class SerialLineTransport : ILineTransport
    {
        private SerialPort port;
        public string Name { get; }

        public SerialLineTransport(string portName) => Name = portName;

        public bool Open(out string error)
        {
            try
            {
                // DTR resets an Uno on open; the handshake window covers its ~2 s boot.
                port = new SerialPort(Name, ControllerProtocol.BaudRate)
                    { ReadTimeout = 100, WriteTimeout = 100, NewLine = "\n", DtrEnable = true };
                port.Open();
                error = null;
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = Name + " is in use by another program (close the Arduino Serial Monitor)";
            }
            catch (Exception e)
            {
                error = Name + ": " + e.Message;
            }
            Dispose();
            return false;
        }

        public string ReadLine()
        {
            try { return port.ReadLine(); }
            catch (TimeoutException) { return null; }
        }

        public void WriteLine(string line) => port.WriteLine(line);

        public void Dispose()
        {
            try { port?.Close(); }
            catch (IOException) { }
            port = null;
        }
    }

    /// <summary>In-memory controller for development and tests without hardware. Emits packets from
    /// settable values at ~50 Hz; can inject malformed lines, drop echoes, or simulate unplugging.</summary>
    public sealed class SimulatedLineTransport : ILineTransport
    {
        private readonly object gate = new object();
        private readonly List<string> sent = new List<string>();
        private int stickX = 512, stickY = 512, distanceMm = 180;
        private bool fire, stickButton, unplugged;
        private string injected;

        public string Name => "Simulator";

        public void Set(int x, int y, int mm, bool fireHeld, bool stickPressed = false)
        {
            lock (gate) { stickX = x; stickY = y; distanceMm = mm; fire = fireHeld; stickButton = stickPressed; }
        }

        public void SetUnplugged(bool value) { lock (gate) unplugged = value; }

        /// <summary>The next line read is this raw text (for malformed-packet tests).</summary>
        public void Inject(string rawLine) { lock (gate) injected = rawLine; }

        public string[] SentLines() { lock (gate) return sent.ToArray(); }

        public bool Open(out string error)
        {
            lock (gate)
            {
                error = unplugged ? "Simulator unplugged" : null;
                return !unplugged;
            }
        }

        public string ReadLine()
        {
            Thread.Sleep(20);
            lock (gate)
            {
                if (unplugged) throw new IOException("Simulator unplugged");
                if (injected != null)
                {
                    string line = injected;
                    injected = null;
                    return line;
                }
                return ControllerProtocol.Frame(
                    $"AX,{stickX},{stickY},{distanceMm},{(fire ? 1 : 0)},{(stickButton ? 1 : 0)}");
            }
        }

        public void WriteLine(string line) { lock (gate) sent.Add(line); }

        public void Dispose() { }
    }

    public enum LinkState { Stopped, Searching, Connected }

    /// <summary>Thread-safe view of the link for the main thread.</summary>
    public readonly struct LinkSnapshot
    {
        public readonly LinkState State;
        public readonly string PortName;
        public readonly string LastError;
        public readonly ControllerPacket Packet;
        /// <summary>Increments with every accepted packet; compare to detect fresh data.</summary>
        public readonly long PacketCount;
        public readonly long RejectedCount;

        public LinkSnapshot(LinkState state, string portName, string lastError, ControllerPacket packet,
            long packetCount, long rejectedCount)
        {
            State = state;
            PortName = portName;
            LastError = lastError;
            Packet = packet;
            PacketCount = packetCount;
            RejectedCount = rejectedCount;
        }
    }

    /// <summary>
    /// Owns the controller connection on a background thread so serial I/O never blocks rendering.
    /// Searching: tries each candidate transport and accepts the first that sends a valid packet
    /// within the handshake window. Connected: reads and validates lines, sends queued commands, and
    /// drops back to searching on an I/O error or when packets stop (unplug), so replugging reconnects.
    /// </summary>
    public sealed class ControllerLink : IDisposable
    {
        public float HandshakeSeconds = 3f;
        public float SilenceSeconds = 1f;
        public float RetrySeconds = 2f;

        private readonly Func<IEnumerable<ILineTransport>> candidates;
        private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object gate = new object();
        private Thread thread;
        private volatile bool running;

        private LinkState state = LinkState.Stopped;
        private string portName;
        private string lastError;
        private ControllerPacket latest;
        private long packetCount;
        private long rejectedCount;
        private double lastPacketTime;

        public ControllerLink(Func<IEnumerable<ILineTransport>> candidates) => this.candidates = candidates;

        /// <summary>Serial ports to try, with the last known controller port first.</summary>
        public static IEnumerable<ILineTransport> SerialCandidates(string preferredPort)
        {
            var names = new List<string>(SerialPort.GetPortNames());
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(preferredPort) && names.Remove(preferredPort)) names.Insert(0, preferredPort);
            foreach (string name in names) yield return new SerialLineTransport(name);
        }

        public void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "Axiom controller link" };
            thread.Start();
        }

        public void Dispose()
        {
            running = false;
            thread?.Join(2000);
            thread = null;
            lock (gate) state = LinkState.Stopped;
        }

        public LinkSnapshot Snapshot()
        {
            lock (gate) return new LinkSnapshot(state, portName, lastError, latest, packetCount, rejectedCount);
        }

        /// <summary>Queues a framed command; dropped if no controller is connected.</summary>
        public void Send(string line)
        {
            lock (gate) if (state != LinkState.Connected) return;
            outgoing.Enqueue(line);
        }

        private double Now => clock.Elapsed.TotalSeconds;

        private void Run()
        {
            while (running)
            {
                lock (gate) { state = LinkState.Searching; portName = null; }
                ILineTransport connected = null;
                foreach (ILineTransport candidate in candidates())
                {
                    if (!running) { candidate.Dispose(); break; }
                    if (TryHandshake(candidate)) { connected = candidate; break; }
                    candidate.Dispose();
                }
                if (connected == null)
                {
                    Wait(RetrySeconds);
                    continue;
                }
                Pump(connected);
                connected.Dispose();
            }
            lock (gate) state = LinkState.Stopped;
        }

        private bool TryHandshake(ILineTransport transport)
        {
            if (!transport.Open(out string error))
            {
                SetError(error);
                return false;
            }
            double deadline = Now + HandshakeSeconds;
            while (running && Now < deadline)
            {
                string line;
                try { line = transport.ReadLine(); }
                catch (Exception e) { SetError(transport.Name + ": " + e.Message); return false; }
                if (line == null || !ControllerProtocol.TryParse(line, out ControllerPacket packet)) continue;
                while (outgoing.TryDequeue(out _)) { }
                lock (gate)
                {
                    state = LinkState.Connected;
                    portName = transport.Name;
                    lastError = null;
                    Accept(packet);
                }
                return true;
            }
            SetError(transport.Name + ": no controller data");
            return false;
        }

        private void Pump(ILineTransport transport)
        {
            while (running)
            {
                try
                {
                    while (outgoing.TryDequeue(out string command)) transport.WriteLine(command);
                    string line = transport.ReadLine();
                    if (line != null)
                    {
                        lock (gate)
                        {
                            if (ControllerProtocol.TryParse(line, out ControllerPacket packet)) Accept(packet);
                            else rejectedCount++;
                        }
                    }
                }
                catch (Exception e)
                {
                    SetError(transport.Name + " disconnected: " + e.Message);
                    return;
                }
                double since;
                lock (gate) since = Now - lastPacketTime;
                if (since > SilenceSeconds)
                {
                    SetError(transport.Name + " stopped sending");
                    return;
                }
            }
        }

        private void Accept(ControllerPacket packet)
        {
            latest = packet;
            packetCount++;
            lastPacketTime = Now;
        }

        private void SetError(string message)
        {
            lock (gate) lastError = message;
        }

        private void Wait(float seconds)
        {
            double until = Now + seconds;
            while (running && Now < until) Thread.Sleep(50);
        }
    }
}
