using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage.Streams;

namespace MediaCaptureWpfCustomControl
{
    /// <summary>
    /// Interaction logic for CaptureElement.xaml
    /// </summary>
    public partial class CaptureElement : UserControl
    {
        WriteableBitmap writeableBitmap;
        SoftwareBitmap backBuffer;

        bool taskRunning;

        public CaptureElement()
        {
            InitializeComponent();
        }

        public async Task SetCaptureAsync(MediaCapture capture)
        {
            await capture.InitializeAsync(new() 
            {
                MemoryPreference = MediaCaptureMemoryPreference.Cpu

            });

            // TODO: Likely should use APIs which can guarantee us the proper media sources and formats
            // https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/process-media-frames-with-mediaframereader#select-frame-sources-and-frame-source-groups
            var video = capture.FrameSources.First().Value;
            var reader = await capture.CreateFrameReaderAsync(video);

            writeableBitmap = new(
                (int) video.CurrentFormat.VideoFormat.Width, 
                (int) video.CurrentFormat.VideoFormat.Height, 
                96,
                96,
                PixelFormats.Bgra32, 
                null);

            PreviewImage.Source = writeableBitmap;

            reader.FrameArrived += Reader_FrameArrived;
            var thing = await reader.StartAsync();
        }

        private void Reader_FrameArrived(Windows.Media.Capture.Frames.MediaFrameReader sender, Windows.Media.Capture.Frames.MediaFrameArrivedEventArgs args)
        {
            using var mediaFrameReference = sender.TryAcquireLatestFrame();
            var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
            var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

            if (softwareBitmap != null)
            {
                if (softwareBitmap is { BitmapPixelFormat: not BitmapPixelFormat.Bgra8 } or { BitmapAlphaMode: not BitmapAlphaMode.Premultiplied })
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                softwareBitmap = Interlocked.Exchange(ref backBuffer, softwareBitmap);
                softwareBitmap?.Dispose();

                writeableBitmap.Dispatcher.Invoke(() =>
                {
                    if (taskRunning)
                    {
                        return;
                    }

                    taskRunning = true;

                    SoftwareBitmap latestBitmap;
                    while ((latestBitmap = Interlocked.Exchange(ref backBuffer, null)) != null)
                    {
                        try
                        {
                            writeableBitmap.Lock();

                            using var m = latestBitmap.LockBuffer(BitmapBufferAccessMode.Read);
                            using var reference = m.CreateReference();

                            var t = m.GetPlaneDescription(0);

                            unsafe
                            {
                                ((IMemoryBufferByteAccess) reference).GetBuffer(out var ptr, out var capacity);

                                writeableBitmap.WritePixels(
                                    new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight), 
                                    (IntPtr)ptr, 
                                    (int)capacity, 
                                    t.Stride);
                            }

                            latestBitmap.Dispose();

                            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));

                        }
                        finally
                        {
                            writeableBitmap.Unlock();
                        }

                    }

                    taskRunning = false;
                });
            }
        }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
