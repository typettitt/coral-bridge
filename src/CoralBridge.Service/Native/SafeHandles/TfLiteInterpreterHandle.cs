using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace CoralBridge.Native.SafeHandles;

/// <summary>
/// Safe handle for TfLiteInterpreter pointer
/// </summary>
public sealed class TfLiteInterpreterHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TfLiteInterpreterHandle() : base(ownsHandle: true) { }

    public TfLiteInterpreterHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        TfLiteInterop.TfLiteInterpreterDelete(handle);
        return true;
    }

    /// <summary>
    /// Creates an interpreter with the given model and options.
    /// </summary>
    public static TfLiteInterpreterHandle Create(TfLiteModelHandle model, TfLiteOptionsHandle? options = null)
    {
        var handle = TfLiteInterop.TfLiteInterpreterCreate(
            model.DangerousGetHandle(),
            options?.DangerousGetHandle() ?? IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create interpreter");
        }
        return new TfLiteInterpreterHandle(handle);
    }

    /// <summary>
    /// Allocates tensors for the interpreter.
    /// </summary>
    public void AllocateTensors()
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var status = TfLiteInterop.TfLiteInterpreterAllocateTensors(handle);
        if (status != TfLiteInterop.kTfLiteOk)
        {
            throw new InvalidOperationException($"Failed to allocate tensors, status: {status}");
        }
    }

    /// <summary>
    /// Modifies the graph with a delegate.
    /// Must be called before AllocateTensors.
    /// </summary>
    public void ModifyGraphWithDelegate(IntPtr @delegate)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var status = TfLiteInterop.TfLiteInterpreterModifyGraphWithDelegate(handle, @delegate);
        if (status != TfLiteInterop.kTfLiteOk)
        {
            throw new InvalidOperationException($"Failed to modify graph with delegate, status: {status}");
        }
    }

    /// <summary>
    /// Runs inference.
    /// </summary>
    public void Invoke()
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var status = TfLiteInterop.TfLiteInterpreterInvoke(handle);
        if (status != TfLiteInterop.kTfLiteOk)
        {
            throw new InvalidOperationException($"Failed to invoke interpreter, status: {status}");
        }
    }

    /// <summary>
    /// Gets the number of input tensors.
    /// </summary>
    public int GetInputTensorCount()
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));
        return TfLiteInterop.TfLiteInterpreterGetInputTensorCount(handle);
    }

    /// <summary>
    /// Gets the input tensor at the specified index.
    /// </summary>
    public IntPtr GetInputTensor(int index)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));
        return TfLiteInterop.TfLiteInterpreterGetInputTensor(handle, index);
    }

    /// <summary>
    /// Gets the number of output tensors.
    /// </summary>
    public int GetOutputTensorCount()
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));
        return TfLiteInterop.TfLiteInterpreterGetOutputTensorCount(handle);
    }

    /// <summary>
    /// Gets the output tensor at the specified index.
    /// </summary>
    public IntPtr GetOutputTensor(int index)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));
        return TfLiteInterop.TfLiteInterpreterGetOutputTensor(handle, index);
    }

    /// <summary>
    /// Copies data to the input tensor.
    /// </summary>
    public void CopyToInputTensor(int index, ReadOnlySpan<byte> data)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var tensor = GetInputTensor(index);
        if (tensor == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Input tensor {index} not found");
        }

        unsafe
        {
            fixed (byte* ptr = data)
            {
                var status = TfLiteInterop.TfLiteTensorCopyFromBuffer(tensor, (IntPtr)ptr, (nuint)data.Length);
                if (status != TfLiteInterop.kTfLiteOk)
                {
                    throw new InvalidOperationException($"Failed to copy data to input tensor, status: {status}");
                }
            }
        }
    }

    /// <summary>
    /// Copies data from the output tensor.
    /// </summary>
    public void CopyFromOutputTensor(int index, Span<byte> buffer)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var tensor = GetOutputTensor(index);
        if (tensor == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Output tensor {index} not found");
        }

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var status = TfLiteInterop.TfLiteTensorCopyToBuffer(tensor, (IntPtr)ptr, (nuint)buffer.Length);
                if (status != TfLiteInterop.kTfLiteOk)
                {
                    throw new InvalidOperationException($"Failed to copy data from output tensor, status: {status}");
                }
            }
        }
    }

    /// <summary>
    /// Gets the output tensor data directly as a span.
    /// </summary>
    public unsafe ReadOnlySpan<float> GetOutputTensorDataAsFloat(int index)
    {
        if (IsInvalid) throw new ObjectDisposedException(nameof(TfLiteInterpreterHandle));

        var tensor = GetOutputTensor(index);
        if (tensor == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Output tensor {index} not found");
        }

        var dataPtr = TfLiteInterop.TfLiteTensorData(tensor);
        var byteSize = TfLiteInterop.TfLiteTensorByteSize(tensor);
        var floatCount = (int)(byteSize / sizeof(float));

        return new ReadOnlySpan<float>((void*)dataPtr, floatCount);
    }

    /// <summary>
    /// Gets tensor dimensions.
    /// </summary>
    public int[] GetTensorDimensions(IntPtr tensor)
    {
        var numDims = TfLiteInterop.TfLiteTensorNumDims(tensor);
        var dims = new int[numDims];

        for (var i = 0; i < numDims; i++)
        {
            dims[i] = TfLiteInterop.TfLiteTensorDim(tensor, i);
        }

        return dims;
    }
}
