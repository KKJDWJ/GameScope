using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Automation.Core.Capture;
using Automation.Core.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace Automation.Windows.Capture
{
    public sealed class WindowsGraphicsCaptureService
    {
        // IID for Windows.Graphics.Capture.IGraphicsCaptureItem.
        // Do not use typeof(GraphicsCaptureItem).GUID here: GraphicsCaptureItem is
        // a WinRT runtime class and its projected type GUID is not this interface IID.
        private static readonly Guid GraphicsCaptureItemId =
            new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        private static readonly Guid DxgiDeviceId = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
        private readonly IWindowService _windowService;

        public WindowsGraphicsCaptureService(IWindowService windowService)
        {
            _windowService = windowService;
        }

        public async Task<CaptureResult> CaptureWindowAsync(
            IntPtr handle,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var window = _windowService.GetWindow(handle);
            if (window == null)
            {
                throw new InvalidOperationException("Window not found.");
            }

            if (window.IsMinimized)
            {
                throw new InvalidOperationException(
                    "Windows Graphics Capture cannot capture this window while it is minimized. " +
                    "Restore it first; it may remain behind other windows.");
            }

            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new NotSupportedException("Windows Graphics Capture is not supported on this PC.");
            }

            using var device = CreateDirect3DDevice();
            var itemInterop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
            var itemPointer = IntPtr.Zero;
            GraphicsCaptureItem item;
            try
            {
                var itemId = GraphicsCaptureItemId;
                var createResult = itemInterop.CreateForWindow(handle, in itemId, out itemPointer);
                Marshal.ThrowExceptionForHR(createResult);
                item = MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPointer);
            }
            finally
            {
                if (itemPointer != IntPtr.Zero)
                {
                    Marshal.Release(itemPointer);
                }
            }

            var size = item.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                throw new InvalidOperationException("The selected window has no capturable content.");
            }

            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                size);
            using var session = framePool.CreateCaptureSession(item);
            var frameCompletion = new TaskCompletionSource<Direct3D11CaptureFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void FrameArrived(Direct3D11CaptureFramePool sender, object args)
            {
                var frame = sender.TryGetNextFrame();
                if (frame != null && !frameCompletion.TrySetResult(frame))
                {
                    frame.Dispose();
                }
            }

            framePool.FrameArrived += FrameArrived;
            try
            {
                session.IsCursorCaptureEnabled = false;
                session.StartCapture();

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delay = Task.Delay(timeout, timeoutSource.Token);
                var completed = await Task.WhenAny(frameCompletion.Task, delay).ConfigureAwait(false);
                if (completed != frameCompletion.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("No frame arrived from Windows Graphics Capture.");
                }

                timeoutSource.Cancel();
                using var frame = await frameCompletion.Task.ConfigureAwait(false);
                using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();

                var bytes = new byte[stream.Size];
                stream.Seek(0);
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(bytes);
                }

                return new CaptureResult(bytes, window.Bounds, DateTimeOffset.Now);
            }
            finally
            {
                framePool.FrameArrived -= FrameArrived;
            }
        }

        private static IDirect3DDevice CreateDirect3DDevice()
        {
            const uint bgraSupport = 0x20;
            const uint sdkVersion = 7;
            IntPtr d3dDevice = IntPtr.Zero;
            IntPtr immediateContext = IntPtr.Zero;
            IntPtr dxgiDevice = IntPtr.Zero;
            IntPtr inspectableDevice = IntPtr.Zero;

            try
            {
                var result = D3D11CreateDevice(
                    IntPtr.Zero,
                    1,
                    IntPtr.Zero,
                    bgraSupport,
                    IntPtr.Zero,
                    0,
                    sdkVersion,
                    out d3dDevice,
                    out _,
                    out immediateContext);
                Marshal.ThrowExceptionForHR(result);

                var dxgiDeviceId = DxgiDeviceId;
                result = Marshal.QueryInterface(d3dDevice, in dxgiDeviceId, out dxgiDevice);
                Marshal.ThrowExceptionForHR(result);

                result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice);
                Marshal.ThrowExceptionForHR(result);
                return MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
            }
            finally
            {
                if (inspectableDevice != IntPtr.Zero)
                {
                    Marshal.Release(inspectableDevice);
                }

                if (dxgiDevice != IntPtr.Zero)
                {
                    Marshal.Release(dxgiDevice);
                }

                if (immediateContext != IntPtr.Zero)
                {
                    Marshal.Release(immediateContext);
                }

                if (d3dDevice != IntPtr.Zero)
                {
                    Marshal.Release(d3dDevice);
                }
            }
        }

        [DllImport("d3d11.dll", ExactSpelling = true)]
        private static extern int D3D11CreateDevice(
            IntPtr adapter,
            uint driverType,
            IntPtr software,
            uint flags,
            IntPtr featureLevels,
            uint featureLevelsCount,
            uint sdkVersion,
            out IntPtr device,
            out uint featureLevel,
            out IntPtr immediateContext);

        [DllImport("d3d11.dll", ExactSpelling = true)]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            [PreserveSig]
            int CreateForWindow(IntPtr window, in Guid iid, out IntPtr item);
        }
    }
}
