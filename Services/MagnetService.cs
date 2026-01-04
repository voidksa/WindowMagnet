using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WindowMagnet;
using WindowMagnet.Models;

namespace WindowMagnet.Services
{
    public class MagnetService : IDisposable
    {
        private NativeMethods.WinEventDelegate _winEventProc;
        private IntPtr _hHook;
        private readonly ConcurrentDictionary<IntPtr, List<Bond>> _bonds = new ConcurrentDictionary<IntPtr, List<Bond>>();

        // Keep track of windows we are currently moving to avoid feedback loops
        private readonly HashSet<IntPtr> _ignoreWindows = new HashSet<IntPtr>();
        private readonly object _lock = new object();

        private struct Bond
        {
            public IntPtr ChildHandle;
            public int OffsetX;
            public int OffsetY;
        }

        public MagnetService()
        {
            _winEventProc = new NativeMethods.WinEventDelegate(WinEventProc);
            // Listen for EVENT_OBJECT_LOCATIONCHANGE (0x800B)
            _hHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }

        public void AddBond(IntPtr parentHandle, IntPtr childHandle)
        {
            // Restore both windows if maximized or minimized
            // Note: We only force restore on Child to resize it. Parent we might want to keep as is?
            // User said: "preserve the last shape I chose for Master". 
            // If Master is maximized, and we want Slave to match Master, Slave would be maximized too.
            // But User said "don't be full screen".
            // So we assume the user has manually sized the Master window and it is NOT maximized, or if it is, they want the Child to match it?
            // "يحافظ على اخر شكل اخترته في الماستر" -> Preserve Master shape.
            // "ويخلي السلف بنفس الماستر واحجامه" -> Make Slave same as Master and its sizes.

            // Let's restore Child so we can move/resize it freely.
            if (NativeMethods.IsZoomed(childHandle) || NativeMethods.IsIconic(childHandle))
            {
                NativeMethods.ShowWindow(childHandle, NativeMethods.SW_RESTORE);
            }

            // Get Master Rect
            if (NativeMethods.GetWindowRect(parentHandle, out var parentRect))
            {
                int width = parentRect.Right - parentRect.Left;
                int height = parentRect.Bottom - parentRect.Top;
                int gap = 10; // Small gap

                // Child to the Right of Parent
                int newChildLeft = parentRect.Right + gap;
                int newChildTop = parentRect.Top;

                // Move/Resize Child
                NativeMethods.SetWindowPos(childHandle, IntPtr.Zero,
                    newChildLeft, newChildTop, width, height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                // Re-read rects for bond
                NativeMethods.GetWindowRect(parentHandle, out var finalParentRect);
                NativeMethods.GetWindowRect(childHandle, out var finalChildRect);

                var bond = new Bond
                {
                    ChildHandle = childHandle,
                    OffsetX = finalChildRect.Left - finalParentRect.Left,
                    OffsetY = finalChildRect.Top - finalParentRect.Top
                };

                _bonds.AddOrUpdate(parentHandle,
                    new List<Bond> { bond },
                    (key, list) => { list.Add(bond); return list; });

                ShakeWindow(childHandle);
            }
        }

        private bool IsMaximized(IntPtr hWnd)
        {
            // Simple check: if rect covers whole screen? 
            // Better: use GetWindowPlacement, but for now assuming if user says "Full Screen" they mean maximized.
            // We force Restore anyway.
            return false; // Force restore handled by ShowWindow call if needed? 
            // Actually ShowWindow(SW_RESTORE) works even if not minimized/maximized, it just restores to normal.
        }

        private void ShakeWindow(IntPtr hWnd)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                if (NativeMethods.GetWindowRect(hWnd, out var rect))
                {
                    int originalX = rect.Left;
                    int originalY = rect.Top;
                    int shakeAmplitude = 5;
                    int shakeCount = 3;
                    int delay = 50;

                    for (int i = 0; i < shakeCount; i++)
                    {
                        NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, originalX + shakeAmplitude, originalY, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                        await System.Threading.Tasks.Task.Delay(delay);
                        NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, originalX - shakeAmplitude, originalY, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                        await System.Threading.Tasks.Task.Delay(delay);
                    }

                    // Restore
                    NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, originalX, originalY, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                }
            });
        }

        public void RemoveBond(IntPtr parentHandle, IntPtr childHandle)
        {
            if (_bonds.TryGetValue(parentHandle, out var list))
            {
                list.RemoveAll(b => b.ChildHandle == childHandle);
                if (list.Count == 0)
                {
                    _bonds.TryRemove(parentHandle, out _);
                }
            }
        }

        public void ClearAll()
        {
            _bonds.Clear();
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE && idObject == 0 && idChild == 0)
            {
                // idObject == 0 means OBJID_WINDOW
                if (_bonds.ContainsKey(hwnd))
                {
                    lock (_lock)
                    {
                        if (_ignoreWindows.Contains(hwnd))
                            return;
                    }

                    MoveChildren(hwnd);
                }
            }
        }

        private void MoveChildren(IntPtr parentHwnd)
        {
            if (_bonds.TryGetValue(parentHwnd, out var bonds) && NativeMethods.GetWindowRect(parentHwnd, out var parentRect))
            {
                // Check if parent is minimized?
                bool isParentMinimized = NativeMethods.IsIconic(parentHwnd);
                // Also check if off-screen (sometimes IsIconic isn't enough during the transition)
                if (parentRect.Left <= -32000) isParentMinimized = true;

                foreach (var bond in bonds)
                {
                    lock (_lock)
                    {
                        _ignoreWindows.Add(bond.ChildHandle);
                    }

                    try
                    {
                        if (isParentMinimized)
                        {
                            // Minimize child too if not already
                            if (!NativeMethods.IsIconic(bond.ChildHandle))
                            {
                                NativeMethods.ShowWindow(bond.ChildHandle, NativeMethods.SW_MINIMIZE);
                            }
                        }
                        else
                        {
                            // Parent is restored/visible.
                            // Check if child is minimized. If so, restore it.
                            if (NativeMethods.IsIconic(bond.ChildHandle))
                            {
                                NativeMethods.ShowWindow(bond.ChildHandle, NativeMethods.SW_RESTORE);
                            }

                            // Check if Master is Maximized (Full Screen)
                            bool isParentMaximized = NativeMethods.IsZoomed(parentHwnd);

                            if (isParentMaximized)
                            {
                                // User request: "If Master is Full Screen, Slave should not go to other screen but be behind Master"
                                // Strategy: Move Slave to same coordinates as Master (behind it) or just keep it there but don't offset.
                                // We'll align it exactly with Master, so it's hidden behind it.
                                int parentWidth = parentRect.Right - parentRect.Left;
                                int parentHeight = parentRect.Bottom - parentRect.Top;

                                NativeMethods.SetWindowPos(bond.ChildHandle, IntPtr.Zero,
                                    parentRect.Left, parentRect.Top, parentWidth, parentHeight,
                                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                            }
                            else
                            {
                                // Move to new position and Resize to match Parent
                                if (NativeMethods.IsWindowVisible(bond.ChildHandle))
                                {
                                    int parentWidth = parentRect.Right - parentRect.Left;
                                    int parentHeight = parentRect.Bottom - parentRect.Top;
                                    int gap = 10;

                                    // Force Child to be on the Right of Parent with same size
                                    int newX = parentRect.Right + gap;
                                    int newY = parentRect.Top;

                                    NativeMethods.SetWindowPos(bond.ChildHandle, IntPtr.Zero,
                                        newX, newY, parentWidth, parentHeight,
                                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _ignoreWindows.Remove(bond.ChildHandle);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_hHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hHook);
                _hHook = IntPtr.Zero;
            }
        }
    }
}
