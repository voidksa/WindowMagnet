using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WindowMagnet;
using WindowMagnet.Models;

namespace WindowMagnet.Services
{
    public class WindowManager
    {
        public List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (NativeMethods.IsWindowVisible(hWnd))
                {
                    int length = NativeMethods.GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        var sb = new StringBuilder(length + 1);
                        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();

                        // Filter out Program Manager and typical shell windows if needed, 
                        // but for now just check if title is not empty.
                        if (!string.IsNullOrWhiteSpace(title) && title != "Program Manager")
                        {
                            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                            string processName = "Unknown";

                            // Try to get process name, but don't fail if we can't
                            try
                            {
                                using (var process = Process.GetProcessById((int)processId))
                                {
                                    processName = process.ProcessName;
                                }
                            }
                            catch
                            {
                                // Ignore permission errors or if process has exited
                            }

                            windows.Add(new WindowInfo
                            {
                                Handle = hWnd,
                                Title = title,
                                ProcessName = processName
                            });
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }
    }
}
