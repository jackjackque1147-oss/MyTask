using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using MyTask.Models;

namespace MyTask.Services;

public sealed class GlobalInputRecorder : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMouseWheel = 0x020A;

    private readonly LowLevelKeyboardProc _keyboardProc;
    private readonly LowLevelMouseProc _mouseProc;
    private readonly Stopwatch _clock = new();
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private long _lastEventAt;
    private long _lastMoveAt;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private bool _isRecording;

    public GlobalInputRecorder()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    public event Action<AutomationEvent>? EventRecorded;
    public bool IsRecording => _isRecording;

    public void Start()
    {
        if (_isRecording)
        {
            return;
        }

        var module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, module, 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Could not install the keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            throw new InvalidOperationException($"Could not install the mouse hook (Win32 error {error}).");
        }

        _lastEventAt = 0;
        _lastMoveAt = 0;
        _lastX = int.MinValue;
        _lastY = int.MinValue;
        _clock.Restart();
        _isRecording = true;
    }

    public void Stop()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _clock.Stop();
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();

    private IntPtr KeyboardCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _isRecording)
        {
            var message = wParam.ToInt32();
            var type = message switch
            {
                WmKeyDown or WmSysKeyDown => "Key Down",
                WmKeyUp or WmSysKeyUp => "Key Up",
                _ => null
            };

            if (type is not null)
            {
                var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var key = KeyInterop.KeyFromVirtualKey((int)data.VirtualKey);
                var keyName = key is Key.None or Key.System ? $"VK_{data.VirtualKey:X2}" : key.ToString();
                Publish(type, keyName, 0, 0, 0);
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _isRecording)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
            var type = message switch
            {
                WmMouseMove => "Mouse Move",
                WmLButtonDown => "Left Down",
                WmLButtonUp => "Left Up",
                WmRButtonDown => "Right Down",
                WmRButtonUp => "Right Up",
                WmMouseWheel => "Mouse Wheel",
                _ => null
            };

            if (type == "Mouse Move")
            {
                var now = _clock.ElapsedMilliseconds;
                if (now - _lastMoveAt < 20 || (data.Point.X == _lastX && data.Point.Y == _lastY))
                {
                    return CallNextHookEx(_mouseHook, code, wParam, lParam);
                }

                _lastMoveAt = now;
                _lastX = data.Point.X;
                _lastY = data.Point.Y;
            }

            if (type is not null)
            {
                var wheelDelta = type == "Mouse Wheel" ? (short)(data.MouseData >> 16) : 0;
                Publish(type, string.Empty, data.Point.X, data.Point.Y, wheelDelta);
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void Publish(string type, string key, int x, int y, int value)
    {
        var now = _clock.ElapsedMilliseconds;
        var delay = (int)Math.Clamp(now - _lastEventAt, 0, int.MaxValue);
        _lastEventAt = now;
        EventRecorded?.Invoke(new AutomationEvent
        {
            Type = type,
            Key = key,
            X = x,
            Y = y,
            Value = value,
            DelayMs = delay
        });
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
