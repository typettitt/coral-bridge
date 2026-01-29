using System.Runtime.InteropServices;

namespace CoralBridge.Native;

/// <summary>
/// P/Invoke bindings for Edge TPU C API (edgetpu.dll)
/// Based on edgetpu_c.h
/// </summary>
public static partial class EdgeTpuInterop
{
    private const string LibraryName = "edgetpu";

    /// <summary>
    /// Edge TPU device types
    /// </summary>
    public enum EdgeTpuDeviceType
    {
        /// <summary>PCIe/M.2 Coral Accelerator</summary>
        ApexPci = 0,
        /// <summary>USB Coral Accelerator</summary>
        ApexUsb = 1
    }

    /// <summary>
    /// Represents an Edge TPU device
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EdgeTpuDevice
    {
        public EdgeTpuDeviceType Type;
        public IntPtr Path;

        public string? GetPath() => Marshal.PtrToStringAnsi(Path);
    }

    /// <summary>
    /// Options for creating an Edge TPU delegate
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EdgeTpuOption
    {
        public IntPtr Name;
        public IntPtr Value;
    }

    /// <summary>
    /// Returns array of connected Edge TPU devices.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_list_devices")]
    public static partial IntPtr edgetpu_list_devices(out nuint num_devices);

    /// <summary>
    /// Frees array returned by edgetpu_list_devices.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_free_devices")]
    public static partial void edgetpu_free_devices(IntPtr dev);

    /// <summary>
    /// Creates a delegate which handles all Edge TPU custom ops.
    /// Options must be available only during the call of this function.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_create_delegate", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr edgetpu_create_delegate(
        EdgeTpuDeviceType type,
        string? name,
        IntPtr options,
        nuint num_options);

    /// <summary>
    /// Frees delegate returned by edgetpu_create_delegate.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_free_delegate")]
    public static partial void edgetpu_free_delegate(IntPtr @delegate);

    /// <summary>
    /// Sets verbosity of operating logs related to Edge TPU.
    /// Verbosity level can be set to [0-10], in which 10 is the most verbose.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_verbosity")]
    public static partial void edgetpu_verbosity(int verbosity);

    /// <summary>
    /// Returns the version of Edge TPU runtime stack.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "edgetpu_version")]
    public static partial IntPtr edgetpu_version();

    /// <summary>
    /// Gets the Edge TPU runtime version as a string.
    /// </summary>
    public static string? GetVersion()
    {
        var ptr = edgetpu_version();
        return Marshal.PtrToStringAnsi(ptr);
    }

    /// <summary>
    /// Enumerates all connected Edge TPU devices.
    /// </summary>
    public static EdgeTpuDevice[] ListDevices()
    {
        var devicesPtr = edgetpu_list_devices(out var numDevices);
        if (devicesPtr == IntPtr.Zero || numDevices == 0)
        {
            return [];
        }

        try
        {
            var devices = new EdgeTpuDevice[(int)numDevices];
            var deviceSize = Marshal.SizeOf<EdgeTpuDevice>();

            for (var i = 0; i < (int)numDevices; i++)
            {
                devices[i] = Marshal.PtrToStructure<EdgeTpuDevice>(devicesPtr + i * deviceSize);
            }

            return devices;
        }
        finally
        {
            edgetpu_free_devices(devicesPtr);
        }
    }
}
