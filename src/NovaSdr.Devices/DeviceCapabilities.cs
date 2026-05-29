namespace NovaSdr.Devices;

/// <summary>
/// Capabilities van een SDR-device. Gebruikt door de UI om
/// relevante controls te tonen of te verbergen.
/// </summary>
[Flags]
public enum DeviceCapabilities : long
{
    None          = 0,
    Receive       = 1L << 0,   // RX mogelijk
    Transmit      = 1L << 1,   // TX mogelijk
    FullDuplex    = 1L << 2,   // Simultaan RX+TX
    DualRx        = 1L << 3,   // Twee onafhankelijke DDC-paden
    PureSignal    = 1L << 4,   // PS 2.0 loopback feedback
    VariableRate  = 1L << 5,   // Sample rate switchbaar
    HardwareAtt   = 1L << 6,   // Stepped hardware attenuator
    DiversityRx   = 1L << 7,   // Phase-coherente diversity ontvangst
    HwAGC         = 1L << 8,   // Hardware AGC (bijv. SDRplay RSP)
    BiasTee       = 1L << 9,   // Bias-T voeding op antenne-poort
    DirectSample  = 1L << 10,  // HF direct-sampling (RTL-SDR V3+)
    WideFreq      = 1L << 11,  // Breed bereik >30 MHz (bijv. PlutoPlus)
}
