using System.Runtime.InteropServices;

namespace PantheonSDR.Devices.SdrPlay;

/// <summary>
/// P/Invoke bindings for the SDRplay API 3.x.
/// Install the API from https://www.sdrplay.com/api/
/// Windows: sdrplay_api.dll  |  Linux: libsdrplay_api.so  |  macOS: libsdrplay_api.dylib
///
/// NOTE: Uses classic [DllImport] throughout — the [LibraryImport] source
/// generator (SYSLIB1051) cannot marshal structs with ByValTStr or nested
/// function-pointer fields. DllImport handles all marshalling correctly.
/// </summary>
internal static class SdrplayNative
{
    private const string Lib = "sdrplay_api";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [DllImport(Lib, EntryPoint = "sdrplay_api_Open", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError Open();

    [DllImport(Lib, EntryPoint = "sdrplay_api_Close", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError Close();

    [DllImport(Lib, EntryPoint = "sdrplay_api_ApiVersion", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError ApiVersion(out float version);

    // ── Device enumeration ───────────────────────────────────────────────────

    [DllImport(Lib, EntryPoint = "sdrplay_api_GetDevices", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError GetDevices(
        [Out] SdrplayDeviceT[] devices,
        out uint numDevs,
        uint maxNumDevs);

    [DllImport(Lib, EntryPoint = "sdrplay_api_SelectDevice", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError SelectDevice(ref SdrplayDeviceT device);

    [DllImport(Lib, EntryPoint = "sdrplay_api_ReleaseDevice", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError ReleaseDevice(ref SdrplayDeviceT device);

    // ── Parameters ───────────────────────────────────────────────────────────

    [DllImport(Lib, EntryPoint = "sdrplay_api_GetDeviceParams", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError GetDeviceParams(nint dev, out nint deviceParams);

    // ── Streaming ────────────────────────────────────────────────────────────

    [DllImport(Lib, EntryPoint = "sdrplay_api_Init", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError Init(
        nint dev,
        ref SdrplayCallbackFns callbackFns,
        nint cbContext);

    [DllImport(Lib, EntryPoint = "sdrplay_api_Uninit", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError Uninit(nint dev);

    [DllImport(Lib, EntryPoint = "sdrplay_api_Update", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError Update(
        nint dev,
        SdrplayTunerSelect tuner,
        SdrplayReasonForUpdate reasonForUpdate,
        SdrplayReasonForUpdateExt1 reasonForUpdateExt1);

    // ── Debug ────────────────────────────────────────────────────────────────

    [DllImport(Lib, EntryPoint = "sdrplay_api_DebugEnable", CallingConvention = CallingConvention.Cdecl)]
    internal static extern SdrplayError DebugEnable(nint dev, uint enable);
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
