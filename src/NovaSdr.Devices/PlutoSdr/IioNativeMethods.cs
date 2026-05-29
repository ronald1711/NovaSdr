using System.Runtime.InteropServices;

namespace NovaSdr.Devices.PlutoSdr;

/// <summary>
/// P/Invoke bindings voor libiio.
/// Linux: libiio.so.0  |  Windows: iio.dll  |  macOS: libiio.dylib
/// Licentie: LGPL v2.1 — compatibel met GPL als dynamisch gelinkt.
/// </summary>
internal static partial class IioNative
{
    private const string Lib = "iio";

    // ── Context ───────────────────────────────────────────────────────────────

    /// <summary>Verbind via Ethernet (standaard PlutoSDR IP: 192.168.2.1)</summary>
    [LibraryImport(Lib, EntryPoint = "iio_create_network_context",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint CreateNetworkContext(string host);

    /// <summary>Verbind via USB</summary>
    [LibraryImport(Lib, EntryPoint = "iio_create_context_from_uri",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint CreateContextFromUri(string uri);

    [LibraryImport(Lib, EntryPoint = "iio_context_destroy")]
    internal static partial void ContextDestroy(nint ctx);

    [LibraryImport(Lib, EntryPoint = "iio_context_get_devices_count")]
    internal static partial uint ContextGetDevicesCount(nint ctx);

    [LibraryImport(Lib, EntryPoint = "iio_context_get_name",
        StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    internal static partial string? ContextGetName(nint ctx);

    // ── Devices ───────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "iio_context_find_device",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint ContextFindDevice(nint ctx, string name);

    // ── Channels ──────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "iio_device_find_channel",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint DeviceFindChannel(nint dev, string name,
        [MarshalAs(UnmanagedType.Bool)] bool output);

    [LibraryImport(Lib, EntryPoint = "iio_channel_enable")]
    internal static partial void ChannelEnable(nint chn);

    [LibraryImport(Lib, EntryPoint = "iio_channel_disable")]
    internal static partial void ChannelDisable(nint chn);

    // ── Attributes ────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "iio_channel_attr_write_longlong",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ChannelAttrWriteLonglong(nint chn, string attr, long val);

    [LibraryImport(Lib, EntryPoint = "iio_channel_attr_read_longlong",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ChannelAttrReadLonglong(nint chn, string attr, out long val);

    [LibraryImport(Lib, EntryPoint = "iio_channel_attr_write",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ChannelAttrWrite(nint chn, string attr, string val);

    [LibraryImport(Lib, EntryPoint = "iio_device_attr_write",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int DeviceAttrWrite(nint dev, string attr, string val);

    [LibraryImport(Lib, EntryPoint = "iio_device_attr_write_longlong",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int DeviceAttrWriteLonglong(nint dev, string attr, long val);

    // ── Buffer ────────────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "iio_device_create_buffer")]
    internal static partial nint DeviceCreateBuffer(nint dev, nuint samplesCount,
        [MarshalAs(UnmanagedType.Bool)] bool cyclic);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_destroy")]
    internal static partial void BufferDestroy(nint buf);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_refill")]
    internal static partial nint BufferRefill(nint buf);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_push")]
    internal static partial nint BufferPush(nint buf);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_first")]
    internal static partial nint BufferFirst(nint buf, nint chn);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_end")]
    internal static partial nint BufferEnd(nint buf);

    [LibraryImport(Lib, EntryPoint = "iio_buffer_step")]
    internal static partial nint BufferStep(nint buf);
}
