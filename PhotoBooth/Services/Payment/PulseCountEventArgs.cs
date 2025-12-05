using System;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// Event payload emitted whenever the ESP32 reports an updated pulse count.
    /// SOURCE: docs/VHMI_Command_Guide.markdown (Pulse Count Event section).
    /// </summary>
    public sealed class PulseCountEventArgs : EventArgs
    {
        public PulseCountEventArgs(PulseIdentifier identifier, int pulseCount, byte[] uniqueId, DateTime timestampUtc)
        {
            // identifier tells us which accepter reported pulses (card/bill) per doc table.
            Identifier = identifier;
            // pulseCount is the raw little-endian counter described in the VHMI guide.
            PulseCount = pulseCount;
            // uniqueId is a 10-byte unique transaction identifier added by the PCB engineer.
            // Make a defensive copy to ensure immutability (prevents external modification of the array).
            if (uniqueId == null) throw new ArgumentNullException(nameof(uniqueId));
            UniqueId = (byte[])uniqueId.Clone();
            // timestampUtc records when we decoded the packet so downstream services can order events.
            TimestampUtc = timestampUtc;
        }

        /// <summary>
        /// Hardware source of the pulses (card vs bill) straight from doc enum.
        /// </summary>
        public PulseIdentifier Identifier { get; }

        /// <summary>
        /// Cumulative pulse count exactly as reported by the ESP32 event payload.
        /// </summary>
        public int PulseCount { get; }

        /// <summary>
        /// Unique transaction identifier (10 bytes) provided by the PCB to prevent duplicate credits.
        /// </summary>
        public byte[] UniqueId { get; }

        /// <summary>
        /// UTC timestamp captured locally when this packet was parsed for auditability.
        /// </summary>
        public DateTime TimestampUtc { get; }
    }
}

