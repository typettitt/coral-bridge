using Microsoft.Win32.SafeHandles;

namespace CoralBridge.Native.SafeHandles;

/// <summary>
/// Safe handle for Edge TPU delegate pointer
/// </summary>
public sealed class EdgeTpuDelegateHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public EdgeTpuDelegateHandle() : base(ownsHandle: true) { }

    public EdgeTpuDelegateHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        EdgeTpuInterop.edgetpu_free_delegate(handle);
        return true;
    }

    /// <summary>
    /// Creates an Edge TPU delegate for the specified device.
    /// </summary>
    /// <param name="deviceType">Type of Edge TPU device (PCI or USB)</param>
    /// <param name="devicePath">Optional device path, null for default device</param>
    /// <returns>Handle to the delegate, or null if creation failed</returns>
    public static EdgeTpuDelegateHandle? Create(
        EdgeTpuInterop.EdgeTpuDeviceType deviceType = EdgeTpuInterop.EdgeTpuDeviceType.ApexPci,
        string? devicePath = null)
    {
        var handle = EdgeTpuInterop.edgetpu_create_delegate(
            deviceType,
            devicePath,
            IntPtr.Zero,
            0);

        if (handle == IntPtr.Zero)
        {
            return null;
        }

        return new EdgeTpuDelegateHandle(handle);
    }

    /// <summary>
    /// Creates an Edge TPU delegate for the first available PCI device.
    /// </summary>
    public static EdgeTpuDelegateHandle? CreateForPci()
    {
        return Create(EdgeTpuInterop.EdgeTpuDeviceType.ApexPci);
    }

    /// <summary>
    /// Creates an Edge TPU delegate for the first available USB device.
    /// </summary>
    public static EdgeTpuDelegateHandle? CreateForUsb()
    {
        return Create(EdgeTpuInterop.EdgeTpuDeviceType.ApexUsb);
    }

    /// <summary>
    /// Tries to create a delegate for any available Edge TPU device.
    /// Prefers PCI over USB.
    /// </summary>
    public static (EdgeTpuDelegateHandle? Delegate, EdgeTpuInterop.EdgeTpuDeviceType? DeviceType) CreateForAnyDevice()
    {
        var devices = EdgeTpuInterop.ListDevices();

        // Prefer PCI devices
        var pciDevice = devices.FirstOrDefault(d => d.Type == EdgeTpuInterop.EdgeTpuDeviceType.ApexPci);
        if (pciDevice.Path != IntPtr.Zero)
        {
            var handle = Create(EdgeTpuInterop.EdgeTpuDeviceType.ApexPci, pciDevice.GetPath());
            if (handle != null)
            {
                return (handle, EdgeTpuInterop.EdgeTpuDeviceType.ApexPci);
            }
        }

        // Fall back to USB
        var usbDevice = devices.FirstOrDefault(d => d.Type == EdgeTpuInterop.EdgeTpuDeviceType.ApexUsb);
        if (usbDevice.Path != IntPtr.Zero)
        {
            var handle = Create(EdgeTpuInterop.EdgeTpuDeviceType.ApexUsb, usbDevice.GetPath());
            if (handle != null)
            {
                return (handle, EdgeTpuInterop.EdgeTpuDeviceType.ApexUsb);
            }
        }

        // Try default PCI without specific path
        var defaultPci = Create(EdgeTpuInterop.EdgeTpuDeviceType.ApexPci);
        if (defaultPci != null)
        {
            return (defaultPci, EdgeTpuInterop.EdgeTpuDeviceType.ApexPci);
        }

        return (null, null);
    }
}
