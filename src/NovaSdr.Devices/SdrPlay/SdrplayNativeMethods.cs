using System.Runtime.InteropServices;

namespace NovaSdr.Devices.SdrPlay;

/// <summary>
/// P/Invoke bindings voor SDRplay API 3.x.
/// Gebruiker installeert de API zelf via https://www.sdrplay.com/api/
/// Windows: sdrplay_api.dll  |  Linux: libsdrplay_api.so  |  macOS: libsdrplay_api.dylib
/// </summary>
internal static partial class SdrplayNative
{
    private const string Lib = "sdrplay_api";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_Open")]
    internal static partial SdrplayError Open();

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_Close")]
    internal static partial SdrplayError Close();

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_ApiVersion")]
    internal static partial SdrplayError ApiVersion(out float version);

    // ── Device enumeration ───────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_GetDevices")]
    internal static partial SdrplayError GetDevices(
        [Out] SdrplayDeviceT[] devices,
        out uint numDevs,
        uint maxNumDevs);

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_SelectDevice")]
    internal static partial SdrplayError SelectDevice(ref SdrplayDeviceT device);

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_ReleaseDevice")]
    internal static partial SdrplayError ReleaseDevice(ref SdrplayDeviceT device);

    // ── Parameters ───────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_GetDeviceParams")]
    internal static partial SdrplayError GetDeviceParams(
        nint dev,
        out nint deviceParams);

    // ── Streaming ────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_Init")]
    internal static partial SdrplayError Init(
        nint dev,
        ref SdrplayCallbackFns callbackFns,
        nint cbContext);

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_Uninit")]
    internal static partial SdrplayError Uninit(nint dev);

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_Update")]
    internal static partial SdrplayError Update(
        nint dev,
        SdrplayTunerSelect tuner,
        SdrplayReasonForUpdate reasonForUpdate,
        SdrplayReasonForUpdateExt1 reasonForUpdateExt1);

    // ── Debug ────────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "sdrplay_api_DebugEnable")]
    internal static partial SdrplayError DebugEnable(nint dev, uint enable);
}

// ── Enums ────────────────────────────────────────────────────────────────────

internal enum SdrplayError : int
{
    Success = 0,
    Fail = 1,
    InvalidParam = 2,
    OutOfRange = 3,
    GainUpdateError = 4,
    RfUpdateError = 5,
    FsUpdateError = 6,
    HwError = 7,
    AliasingError = 8,
    AlreadyInitialised = 9,
    NotInitialised = 10,
    NotEnabled = 11,
    HwVerError = 12,
    OutOfMemError = 13,
    ServiceNotResponding = 14,
    StartPending = 15,
    StopPending = 16,
    InvalidMode = 17,
    FailedVerification1 = 18,
    FailedVerification2 = 19,
    FailedVerification3 = 20,
    FailedVerification4 = 21,
    FailedVerification5 = 22,
    FailedVerification6 = 23,
    InvalidServiceVersion = 24,
}

internal enum SdrplayTunerSelect : int { Neither = 0, A = 1, B = 2, BothButA = 3, BothButB = 4 }

[Flags]
internal enum SdrplayReasonForUpdate : uint
{
    None = 0,
    Dev_Fs = 0x00000001,
    Dev_Ppm = 0x00000002,
    Dev_SyncUpdate = 0x00000004,
    Dev_ResetFlags = 0x00000008,
    Rsp1a_BiasTControl = 0x00000010,
    Tuner_Gr = 0x00000040,
    Tuner_GrLimits = 0x00000080,
    Tuner_Frf = 0x00000100,
    Tuner_BwType = 0x00000200,
    Tuner_IfType = 0x00000400,
    Tuner_DcOffset = 0x00000800,
    Tuner_LoMode = 0x00001000,
    Ctrl_DCoffsetIQimbalance = 0x00002000,
    Ctrl_Decimation = 0x00004000,
    Ctrl_Agc = 0x00008000,
    Ctrl_AdsbMode = 0x00010000,
    Ctrl_OverloadMsgAck = 0x00020000,
}

internal enum SdrplayReasonForUpdateExt1 : uint { None = 0 }

// ── Structs ───────────────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct SdrplayDeviceT
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string SerNo;

    public byte HwVer;      // 255=RSP1, 1=RSP1A, 2=RSP2, 3=RSPduo, 4=RSPdx, 6=RSP1B

    public SdrplayTunerSelect Tuner;
    public int RspDuoMode;
    public double RspDuoSampleFreq;
    public nint Dev;        // Handle na SelectDevice()
}

[StructLayout(LayoutKind.Sequential)]
internal struct SdrplayCallbackFns
{
    public nint StreamACbFn;   // sdrplay_api_StreamCallback_t
    public nint StreamBCbFn;
    public nint EventCbFn;     // sdrplay_api_EventCallback_t
}
