using System;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// Raised when we convert the raw VHMI pulse counter into a delta we can credit.
    /// SOURCE: docs/VHMI_Command_Guide.markdown (Pulse Count Event format).
    /// </summary>
    public sealed class PulseDeltaEventArgs : EventArgs
    {
        public PulseDeltaEventArgs(PulseIdentifier identifier, int delta, int rawCount, byte[] uniqueId, DateTime timestampUtc)
        {
            Identifier = identifier;
            Delta = delta;
            RawCount = rawCount;
            // Make a defensive copy to ensure immutability (prevents external modification of the array).
            if (uniqueId == null) throw new ArgumentNullException(nameof(uniqueId));
            UniqueId = (byte[])uniqueId.Clone();
            TimestampUtc = timestampUtc;
        }

        /// <summary>
        /// Which accepter produced the pulses (card/bill per doc enum).
        /// </summary>
        public PulseIdentifier Identifier { get; }

        /// <summary>
        /// The number of new pulses since the last processed packet (or full amount if using pulseCount as dollar amount).
        /// </summary>
        public int Delta { get; }

        /// <summary>
        /// The cumulative counter reported by firmware for traceability.
        /// </summary>
        public int RawCount { get; }

        /// <summary>
        /// Unique transaction identifier (10 bytes) from the PCB to prevent duplicate credits.
        /// </summary>
        public byte[] UniqueId { get; }

        /// <summary>
        /// Local UTC timestamp captured when the delta was computed.
        /// </summary>
        public DateTime TimestampUtc { get; }
    }
}

