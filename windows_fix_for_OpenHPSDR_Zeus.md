# Windows Multi-NIC Fix for OpenHPSDR-Zeus

> Fixes a **blank panadapter / no RX** and **failing radio discovery** on Windows
> hosts that have multiple network adapters (VPNs like NordVPN/OpenVPN, Tailscale,
> Hyper-V/WSL vEthernet, Wi-Fi + multiple Ethernet NICs).
>
> Symptom: Zeus connects to an OpenHPSDR Protocol-2 radio (Brick2, Hermes, ANAN, …),
> high-priority status packets flow (`p2.hi_pri.rx pkts=… pll=True`), but the
> waterfall stays blank and the S-meter sits at the noise floor. The **same Zeus
> build works fine on a single-NIC Linux box** with the same radio.
>
> Root cause: two independent Windows-only networking bugs. Both are validated —
> applying them turned a permanently blank panadapter into a live waterfall.

---

## TL;DR — the two bugs

1. **Socket bound to `IPAddress.Any`.** On a multi-NIC host the OS sends outbound
   command packets out the *default-route* interface (often Wi-Fi or a VPN tunnel),
   not the wired NIC on the radio's subnet. The radio then streams DDC IQ to the
   wrong host address → blank panadapter. Fix: bind the data socket to the local
   IP on the radio's subnet (auto-detected — what Thetis does manually via
   "Via specific NIC").

2. **WSAECONNRESET (10054) on UDP recv.** When a `SendTo` provokes an ICMP
   "port unreachable", Windows makes the *next* `ReceiveFrom` **throw** instead of
   ignoring it (Linux never does this). The uncaught exception tears down the IQ
   receive loop on the first stray ICMP, and also aborts radio discovery. Fix:
   the `SIO_UDP_CONNRESET` ioctl + treat `ConnectionReset` as "continue".

> A third issue we hit (missing native `wdsp.dll`) was specific to our fork's
> `.gitignore`. **Stock OpenHPSDR-Zeus ships the native libs**, so you almost
> certainly don't have that problem — but if you ever see
> `DllNotFoundException: 'wdsp'`, check that `Zeus.Dsp/runtimes/win-x64/native/wdsp.dll`
> exists in your build output.

---

## ⬇️ Copy-paste prompt for Claude Code

Open your OpenHPSDR-Zeus checkout in Claude Code and paste this:

```
I'm running OpenHPSDR-Zeus on Windows. My PC has many network adapters (VPN,
Tailscale, Hyper-V/WSL, Wi-Fi + Ethernet). Zeus connects to my Protocol-2 radio
and high-priority status packets arrive, but the panadapter is blank and the
S-meter sits at the floor. The same build works on my single-NIC Linux box.

There are two Windows-only networking bugs. Apply BOTH fixes, then build.

FIX 1 — bind the P2 data socket to the NIC on the radio's subnet (not IPAddress.Any):
In Zeus.Protocol2/Protocol2Client.cs, in the method that creates the main UDP
data socket (ConnectAsync — it currently does
`sock.Bind(new IPEndPoint(IPAddress.Any, 1025))`), replace IPAddress.Any with the
local IPv4 of the interface whose subnet contains the radio IP. Add a static
helper FindLocalAddressForSubnet(IPAddress radioIp) that enumerates
NetworkInterface.GetAllNetworkInterfaces() (skip Down + Loopback), and for each
UnicastAddress with an IPv4Mask returns the address whose (local & mask) ==
(radioIp & mask). Fall back to IPAddress.Any when nothing matches. Log the chosen
bind address.

FIX 2 — disable Windows WSAECONNRESET (10054) on the UDP sockets:
Add a helper DisableUdpConnReset(Socket s) that, only on Windows, calls
s.IOControl(SIO_UDP_CONNRESET, new byte[4], null) where
SIO_UDP_CONNRESET = -1744830452 (0x9800000C), wrapped in try/catch(SocketException).
Call it right after creating:
  - the P2 data socket in Zeus.Protocol2/Protocol2Client.cs (ConnectAsync)
  - the discovery socket in Zeus.Protocol2/Discovery/RadioDiscoveryService.cs
  - the discovery socket in Zeus.Protocol1/Discovery/RadioDiscoveryService.cs
Also make the receive loops resilient: in each ReceiveFromAsync try/catch, add a
`catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)`
that does `continue;` (P2 data loop and both discovery loops) instead of breaking
or throwing.

Both files already `using System.Net.NetworkInformation;` and
`using System.Net.Sockets;`. Keep everything Windows-guarded with
OperatingSystem.IsWindows() so Linux/macOS behaviour is unchanged. Then run
`dotnet build`.
```

---

## Exact reference code (validated)

If you'd rather apply by hand, here is exactly what was changed.

### File: `Zeus.Protocol2/Protocol2Client.cs`

**(a) In `ConnectAsync`, after creating the socket:**

```csharp
_radioEndpoint = new IPEndPoint(radioEndpoint.Address, 1024);
var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
DisableUdpConnReset(sock);                                   // FIX 2

var localBind = FindLocalAddressForSubnet(radioEndpoint.Address) ?? IPAddress.Any; // FIX 1
sock.Bind(new IPEndPoint(localBind, 1025));                  // was IPAddress.Any
sock.ReceiveBufferSize = 1 << 20;
_sock = sock;
_log.LogInformation(
    "p2.connect radio={Radio} localBind={Local} localPort=1025",
    radioEndpoint.Address,
    localBind.Equals(IPAddress.Any) ? "ANY (no subnet match)" : localBind.ToString());
```

**(b) Two static helpers (anywhere in the class):**

```csharp
// FIX 2: Windows turns an ICMP port-unreachable into a 10054 on the next recv.
private const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
internal static void DisableUdpConnReset(Socket s)
{
    if (!OperatingSystem.IsWindows()) return;
    try { s.IOControl(SIO_UDP_CONNRESET, new byte[4], null); }
    catch (SocketException) { /* best effort */ }
}

// FIX 1: local IPv4 of the NIC whose subnet contains the radio (null = no match).
internal static IPAddress? FindLocalAddressForSubnet(IPAddress radioIp)
{
    if (radioIp.AddressFamily != AddressFamily.InterNetwork) return null;
    var radioBytes = radioIp.GetAddressBytes();
    foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (iface.OperationalStatus != OperationalStatus.Up) continue;
        if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
        foreach (var ua in iface.GetIPProperties().UnicastAddresses)
        {
            if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
            var mask = ua.IPv4Mask;
            if (mask is null || mask.Equals(IPAddress.Any)) continue;
            var local = ua.Address.GetAddressBytes();
            var m = mask.GetAddressBytes();
            bool same = true;
            for (int i = 0; i < 4; i++)
                if ((local[i] & m[i]) != (radioBytes[i] & m[i])) { same = false; break; }
            if (same) return ua.Address;
        }
    }
    return null;
}
```

**(c) In the P2 data-socket receive loop, add a ConnectionReset catch before the
generic SocketException handling:**

```csharp
catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
{
    // Windows 10054: prior SendTo provoked ICMP port-unreachable. Without this
    // the IQ loop dies on the first stray ICMP → frozen panadapter. Keep going.
    continue;
}
```

### File: `Zeus.Protocol2/Discovery/RadioDiscoveryService.cs`
After each `new Socket(...)` (the broadcast `DiscoverAsync` socket **and** any
unicast probe socket):
```csharp
Protocol2Client.DisableUdpConnReset(socket); // same assembly → internal is visible
```

### File: `Zeus.Protocol1/Discovery/RadioDiscoveryService.cs`
Add a private copy of `DisableUdpConnReset` + `SIO_UDP_CONNRESET` (Protocol1 is a
different assembly), call it right after creating the discovery socket, and change
the recv `catch (SocketException) { break; }` to:
```csharp
catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
{
    continue; // stray ICMP — a real radio reply may still arrive
}
catch (SocketException ex)
{
    _log.LogWarning(ex, "discovery.socket.error");
    break;
}
```

---

## Will this work for ALL multi-LAN Windows installs?

**Yes, for the normal case** — and it auto-selects the right NIC, so it's actually
more convenient than Thetis (no manual "Via specific NIC" picking):

- ✅ Radio on a directly-connected subnet (e.g. radio `192.168.10.78`, your Ethernet
  NIC `192.168.10.245/24`). The subnet match finds that NIC and binds to it. Works
  regardless of how many VPN/virtual adapters are present.
- ✅ The `SIO_UDP_CONNRESET` fix helps **every** Windows host, single- or multi-NIC —
  it removes the stray-ICMP crash entirely.

**Edge cases where it falls back to `IPAddress.Any`** (i.e. behaves like before):

- ⚠️ **Radio reached through a router** (not on any local subnet). No local NIC matches
  the radio's subnet, so it falls back to `Any`. Cross-subnet OpenHPSDR is unusual
  (it relies on broadcast discovery), but if you do this you'd need an explicit
  "preferred NIC / local bind IP" setting.
- ⚠️ **Two NICs on the same subnet.** The helper returns the *first* match, which may
  not be the one cabled to the radio. Rare; would need manual selection.
- ⚠️ **A VPN that routes your radio's subnet.** If a VPN advertises a route covering
  the radio's IP, Windows' own routing could still interfere with the *outbound* path.
  The explicit subnet-bind makes this far less likely, but the bullet-proof answer is
  to disconnect the VPN or add an explicit local-bind setting.

**Recommended upstream improvement:** add a user-facing "Network interface / local
bind address" dropdown (like Thetis) that overrides the auto-detection, defaulting
to auto. The auto-detect above covers the 95% case; the dropdown covers the rest.

---

## Verifying the fix

After applying + `dotnet build`, connect to your P2 radio and watch the console:

```
p2.connect radio=192.168.10.78 localBind=192.168.10.245 localPort=1025   ← bound to the right NIC
p2.hi_pri.rx pkts=… pll=True                                             ← status flowing
```
The panadapter should show live signal and the S-meter should lift off the floor.
No more `discovery.socket.error` 10054 in the log.

If `localBind` shows `ANY (no subnet match)`, your radio isn't on a directly-connected
subnet — see the edge cases above.

---

*Derived from fixes validated in PantheonSDR (a Zeus-based project):*
*commits `656bcee` (subnet bind) and `79ccf39` (WSAECONNRESET).*
*https://github.com/ronald1711/PantheonSDR*
