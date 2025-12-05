namespace Photobooth.Services.Payment
{
    /// <summary>
    /// Identifiers reported by the VHMI pulse event payload.
    /// SOURCE: docs/VHMI_Command_Guide.markdown (Pulse Count Event table).
    /// </summary>
    public enum PulseIdentifier : byte
    {
        /// <summary>
        /// Card accepter pulses (doc enum value 0).
        /// </summary>
        CardAccepter = 0x00,

        /// <summary>
        /// Bill accepter pulses (doc enum value 1).
        /// </summary>
        BillAccepter = 0x01
    }
}

