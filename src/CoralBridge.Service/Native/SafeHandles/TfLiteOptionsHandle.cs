using Microsoft.Win32.SafeHandles;

namespace CoralBridge.Native.SafeHandles;

/// <summary>
/// Safe handle for TfLiteInterpreterOptions pointer
/// </summary>
public sealed class TfLiteOptionsHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TfLiteOptionsHandle() : base(ownsHandle: true) { }

    public TfLiteOptionsHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        TfLiteInterop.TfLiteInterpreterOptionsDelete(handle);
        return true;
    }

    /// <summary>
    /// Creates a new interpreter options instance.
    /// </summary>
    public static TfLiteOptionsHandle Create()
    {
        var handle = TfLiteInterop.TfLiteInterpreterOptionsCreate();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create interpreter options");
        }
        return new TfLiteOptionsHandle(handle);
    }

    /// <summary>
    /// Sets the number of threads for inference.
    /// </summary>
    public void SetNumThreads(int numThreads)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteOptionsHandle));
        TfLiteInterop.TfLiteInterpreterOptionsSetNumThreads(handle, numThreads);
    }

    /// <summary>
    /// Adds a delegate to the options.
    /// </summary>
    public void AddDelegate(IntPtr @delegate)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteOptionsHandle));
        TfLiteInterop.TfLiteInterpreterOptionsAddDelegate(handle, @delegate);
    }
}
