using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OtpBridge.Services;

public static class ClipboardService
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public static async Task<bool> SetTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            return await Task.Run(() => TrySetText(text)).WaitAsync(DefaultTimeout);
        }
        catch (TimeoutException exception)
        {
            StartupLog.Error("Clipboard copy timed out.", exception);
            return false;
        }
        catch (Exception exception)
        {
            StartupLog.Error("Clipboard copy failed.", exception);
            return false;
        }
    }

    private static bool TrySetText(string text)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                if (TrySetTextOnce(text))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            Thread.Sleep(60);
        }

        if (lastException is not null)
        {
            StartupLog.Error("Clipboard copy retries failed.", lastException);
        }

        return false;
    }

    private static bool TrySetTextOnce(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        var memoryHandle = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to empty clipboard.");
            }

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            memoryHandle = GlobalAlloc(GmemMoveable | GmemZeroInit, (UIntPtr)bytes.Length);
            if (memoryHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to allocate clipboard memory.");
            }

            var lockedMemory = GlobalLock(memoryHandle);
            if (lockedMemory == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to lock clipboard memory.");
            }

            try
            {
                Marshal.Copy(bytes, 0, lockedMemory, bytes.Length);
            }
            finally
            {
                _ = GlobalUnlock(memoryHandle);
            }

            if (SetClipboardData(CfUnicodeText, memoryHandle) == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set clipboard data.");
            }

            memoryHandle = IntPtr.Zero;
            return true;
        }
        finally
        {
            _ = CloseClipboard();
            if (memoryHandle != IntPtr.Zero)
            {
                _ = GlobalFree(memoryHandle);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
