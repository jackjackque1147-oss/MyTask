using System.Runtime.InteropServices;
using System.Windows.Input;
using MyTask.Models;

namespace MyTask.Services;

public sealed class MacroPlayer
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfWheel = 0x0800;

    public async Task PlayAsync(
        IReadOnlyList<AutomationEvent> events,
        long? loopLimit,
        CancellationToken cancellationToken,
        Func<bool> isPaused,
        Func<CancellationToken, Task> waitUntilResumed)
    {
        if (events.Count == 0)
        {
            throw new InvalidOperationException("Add or record at least one event before playing.");
        }

        var pressedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leftButtonPressed = false;
        var rightButtonPressed = false;
        var inputsReleasedForPause = false;

        async Task WaitWhilePausedAsync()
        {
            if (!isPaused())
            {
                return;
            }

            if (!inputsReleasedForPause)
            {
                ReleaseActiveInputs(pressedKeys, leftButtonPressed, rightButtonPressed);
                inputsReleasedForPause = true;
            }

            await waitUntilResumed(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RestoreActiveInputs(pressedKeys, leftButtonPressed, rightButtonPressed);
            inputsReleasedForPause = false;
        }

        try
        {
            for (long loop = 0; loopLimit is null || loop < loopLimit.Value; loop++)
            {
                foreach (var item in events)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var remainingDelay = (double)item.DelayMs;
                    while (remainingDelay > 0)
                    {
                        await WaitWhilePausedAsync();
                        var delay = Math.Min(remainingDelay, 25);
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken).ConfigureAwait(false);
                        remainingDelay -= timer.Elapsed.TotalMilliseconds;
                    }

                    await WaitWhilePausedAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    PlayEvent(item);
                    switch (item.Type)
                    {
                        case "Key Down":
                            pressedKeys.Add(item.Key);
                            break;
                        case "Key Up":
                            pressedKeys.Remove(item.Key);
                            break;
                        case "Left Down":
                            leftButtonPressed = true;
                            break;
                        case "Left Up":
                            leftButtonPressed = false;
                            break;
                        case "Right Down":
                            rightButtonPressed = true;
                            break;
                        case "Right Up":
                            rightButtonPressed = false;
                            break;
                    }
                }
            }
        }
        finally
        {
            if (!inputsReleasedForPause)
            {
                ReleaseActiveInputs(pressedKeys, leftButtonPressed, rightButtonPressed);
            }
        }
    }

    private static void ReleaseActiveInputs(HashSet<string> pressedKeys, bool leftButtonPressed, bool rightButtonPressed)
    {
        foreach (var key in pressedKeys)
        {
            SendKey(key, true);
        }

        if (leftButtonPressed)
        {
            SendMouse(0, 0, 0, MouseeventfLeftup);
        }

        if (rightButtonPressed)
        {
            SendMouse(0, 0, 0, MouseeventfRightup);
        }
    }

    private static void RestoreActiveInputs(HashSet<string> pressedKeys, bool leftButtonPressed, bool rightButtonPressed)
    {
        foreach (var key in pressedKeys)
        {
            SendKey(key, false);
        }

        if (leftButtonPressed)
        {
            SendMouse(0, 0, 0, MouseeventfLeftdown);
        }

        if (rightButtonPressed)
        {
            SendMouse(0, 0, 0, MouseeventfRightdown);
        }
    }

    private static void PlayEvent(AutomationEvent item)
    {
        switch (item.Type)
        {
            case "Key Down":
            case "Key Up":
                SendKey(item.Key, item.Type == "Key Up");
                break;
            case "Mouse Move":
                MoveCursor(item.X, item.Y);
                break;
            case "Left Down":
                MoveCursor(item.X, item.Y);
                SendMouse(0, 0, 0, MouseeventfLeftdown);
                break;
            case "Left Up":
                MoveCursor(item.X, item.Y);
                SendMouse(0, 0, 0, MouseeventfLeftup);
                break;
            case "Right Down":
                MoveCursor(item.X, item.Y);
                SendMouse(0, 0, 0, MouseeventfRightdown);
                break;
            case "Right Up":
                MoveCursor(item.X, item.Y);
                SendMouse(0, 0, 0, MouseeventfRightup);
                break;
            case "Mouse Wheel":
                MoveCursor(item.X, item.Y);
                SendMouse(0, 0, unchecked((uint)item.Value), MouseeventfWheel);
                break;
            case "Wait":
                break;
            default:
                throw new InvalidOperationException($"Unsupported event type: {item.Type}");
        }
    }

    private static void MoveCursor(int x, int y)
    {
        if (!SetCursorPos(x, y))
        {
            throw new InvalidOperationException($"Windows could not move the cursor to {x}, {y} (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static void SendKey(string keyName, bool keyUp)
    {
        if (!Enum.TryParse<Key>(keyName, out var key) || key == Key.None)
        {
            if (!keyName.StartsWith("VK_", StringComparison.OrdinalIgnoreCase)
                || !ushort.TryParse(keyName.AsSpan(3), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var virtualKey))
            {
                throw new InvalidOperationException($"Unsupported key: {keyName}");
            }

            SendKey(virtualKey, keyUp, keyName);
            return;
        }

        SendKey((ushort)KeyInterop.VirtualKeyFromKey(key), keyUp, keyName);
    }

    private static void SendKey(ushort virtualKey, bool keyUp, string keyName)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? KeyeventfKeyup : 0
                }
            }
        };

        if (SendInput(1, ref input, Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException($"Windows could not send the {keyName} key event (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static void SendMouse(int x, int y, uint data, uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                Mouse = new MouseInput
                {
                    X = x,
                    Y = y,
                    Data = data,
                    Flags = flags
                }
            }
        };

        if (SendInput(1, ref input, Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException($"Windows could not send the mouse event (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, ref Input inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);
}
