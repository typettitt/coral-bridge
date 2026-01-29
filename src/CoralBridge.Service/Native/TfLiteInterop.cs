using System.Runtime.InteropServices;

namespace CoralBridge.Native;

/// <summary>
/// P/Invoke bindings for TensorFlow Lite C API (tensorflowlite_c.dll)
/// Based on tensorflow/lite/c/c_api.h
/// </summary>
public static partial class TfLiteInterop
{
    private const string LibraryName = "tensorflowlite_c";

    // TfLiteStatus enum values
    public const int kTfLiteOk = 0;
    public const int kTfLiteError = 1;
    public const int kTfLiteDelegateError = 2;
    public const int kTfLiteApplicationError = 3;
    public const int kTfLiteDelegateDataNotFound = 4;
    public const int kTfLiteDelegateDataWriteError = 5;
    public const int kTfLiteDelegateDataReadError = 6;
    public const int kTfLiteUnresolvedOps = 7;
    public const int kTfLiteCancelled = 8;

    // TfLiteType enum values
    public const int kTfLiteNoType = 0;
    public const int kTfLiteFloat32 = 1;
    public const int kTfLiteInt32 = 2;
    public const int kTfLiteUInt8 = 3;
    public const int kTfLiteInt64 = 4;
    public const int kTfLiteString = 5;
    public const int kTfLiteBool = 6;
    public const int kTfLiteInt16 = 7;
    public const int kTfLiteComplex64 = 8;
    public const int kTfLiteInt8 = 9;
    public const int kTfLiteFloat16 = 10;
    public const int kTfLiteFloat64 = 11;
    public const int kTfLiteComplex128 = 12;
    public const int kTfLiteUInt64 = 13;
    public const int kTfLiteResource = 14;
    public const int kTfLiteVariant = 15;
    public const int kTfLiteUInt32 = 16;
    public const int kTfLiteUInt16 = 17;
    public const int kTfLiteInt4 = 18;

    #region Model Functions

    /// <summary>
    /// Creates a TfLiteModel from a file path.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteModelCreateFromFile", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr TfLiteModelCreateFromFile(string model_path);

    /// <summary>
    /// Creates a TfLiteModel from a memory buffer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteModelCreate")]
    public static partial IntPtr TfLiteModelCreate(IntPtr model_data, nuint model_size);

    /// <summary>
    /// Destroys a TfLiteModel instance.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteModelDelete")]
    public static partial void TfLiteModelDelete(IntPtr model);

    #endregion

    #region Interpreter Options Functions

    /// <summary>
    /// Creates a TfLiteInterpreterOptions instance.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterOptionsCreate")]
    public static partial IntPtr TfLiteInterpreterOptionsCreate();

    /// <summary>
    /// Destroys a TfLiteInterpreterOptions instance.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterOptionsDelete")]
    public static partial void TfLiteInterpreterOptionsDelete(IntPtr options);

    /// <summary>
    /// Sets the number of threads to use for inference.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterOptionsSetNumThreads")]
    public static partial void TfLiteInterpreterOptionsSetNumThreads(IntPtr options, int num_threads);

    /// <summary>
    /// Adds a delegate to the interpreter options.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterOptionsAddDelegate")]
    public static partial void TfLiteInterpreterOptionsAddDelegate(IntPtr options, IntPtr @delegate);

    #endregion

    #region Interpreter Functions

    /// <summary>
    /// Creates a TfLiteInterpreter instance with the given model and options.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterCreate")]
    public static partial IntPtr TfLiteInterpreterCreate(IntPtr model, IntPtr options);

    /// <summary>
    /// Destroys a TfLiteInterpreter instance.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterDelete")]
    public static partial void TfLiteInterpreterDelete(IntPtr interpreter);

    /// <summary>
    /// Modifies the graph with the given delegate.
    /// Must be called after TfLiteInterpreterCreate and before TfLiteInterpreterAllocateTensors.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterModifyGraphWithDelegate")]
    public static partial int TfLiteInterpreterModifyGraphWithDelegate(IntPtr interpreter, IntPtr @delegate);

    /// <summary>
    /// Returns the number of input tensors.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterGetInputTensorCount")]
    public static partial int TfLiteInterpreterGetInputTensorCount(IntPtr interpreter);

    /// <summary>
    /// Returns the input tensor at the given index.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterGetInputTensor")]
    public static partial IntPtr TfLiteInterpreterGetInputTensor(IntPtr interpreter, int input_index);

    /// <summary>
    /// Resizes the input tensor at the given index.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterResizeInputTensor")]
    public static partial int TfLiteInterpreterResizeInputTensor(IntPtr interpreter, int input_index, IntPtr input_dims, int input_dims_size);

    /// <summary>
    /// Allocates tensors for the interpreter.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterAllocateTensors")]
    public static partial int TfLiteInterpreterAllocateTensors(IntPtr interpreter);

    /// <summary>
    /// Runs the inference.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterInvoke")]
    public static partial int TfLiteInterpreterInvoke(IntPtr interpreter);

    /// <summary>
    /// Returns the number of output tensors.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterGetOutputTensorCount")]
    public static partial int TfLiteInterpreterGetOutputTensorCount(IntPtr interpreter);

    /// <summary>
    /// Returns the output tensor at the given index.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteInterpreterGetOutputTensor")]
    public static partial IntPtr TfLiteInterpreterGetOutputTensor(IntPtr interpreter, int output_index);

    #endregion

    #region Tensor Functions

    /// <summary>
    /// Returns the type of the tensor.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorType")]
    public static partial int TfLiteTensorType(IntPtr tensor);

    /// <summary>
    /// Returns the number of dimensions of the tensor.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorNumDims")]
    public static partial int TfLiteTensorNumDims(IntPtr tensor);

    /// <summary>
    /// Returns the dimension at the given index.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorDim")]
    public static partial int TfLiteTensorDim(IntPtr tensor, int dim_index);

    /// <summary>
    /// Returns the size of the tensor data in bytes.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorByteSize")]
    public static partial nuint TfLiteTensorByteSize(IntPtr tensor);

    /// <summary>
    /// Returns a pointer to the tensor data.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorData")]
    public static partial IntPtr TfLiteTensorData(IntPtr tensor);

    /// <summary>
    /// Returns the name of the tensor.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorName")]
    public static partial IntPtr TfLiteTensorName(IntPtr tensor);

    /// <summary>
    /// Copies data from a buffer to the tensor.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorCopyFromBuffer")]
    public static partial int TfLiteTensorCopyFromBuffer(IntPtr tensor, IntPtr input_data, nuint input_data_size);

    /// <summary>
    /// Copies data from the tensor to a buffer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteTensorCopyToBuffer")]
    public static partial int TfLiteTensorCopyToBuffer(IntPtr tensor, IntPtr output_data, nuint output_data_size);

    #endregion

    #region Version

    /// <summary>
    /// Returns the version string for TensorFlow Lite.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "TfLiteVersion")]
    public static partial IntPtr TfLiteVersion();

    #endregion
}
