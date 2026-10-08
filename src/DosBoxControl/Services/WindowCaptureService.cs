using System.Drawing;
using System.Drawing.Imaging;
using DosBoxControl.Native;

namespace DosBoxControl.Services;

public sealed class WindowCaptureService
{
    public byte[] CaptureAsPng(nint hWnd)
    {
        if (Win32.IsIconic(hWnd))
        {
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
            Thread.Sleep(150);
        }

        if (!Win32.GetWindowRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("Could not determine window bounds; the window may be closed.");
        }

        using var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            nint hdc = graphics.GetHdc();
            try
            {
                bool ok = Win32.PrintWindow(hWnd, hdc, Win32.PW_RENDERFULLCONTENT);
                if (!ok)
                {
                    Win32.PrintWindow(hWnd, hdc, 0);
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
