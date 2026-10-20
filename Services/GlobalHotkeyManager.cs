using System.Runtime.InteropServices;
using System.Windows.Input;
using MyTask.Models;

namespace MyTask.Services;

public enum HotkeyAction
{
    ToggleRecording = 1,
    TogglePlayback = 2,
    Stop = 3,
    TogglePause = 4
}

public sealed class GlobalHotkeyManager : IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Dictionary<int, HotkeyAction> _actions = [];
    private IntPtr _windowHandle;
    private HotkeySettings? _registeredSettings;

    public event Action<HotkeyAction>? HotkeyPressed;

    public static IReadOnlySet<string> GetRecordedKeyNames(string shortcut)
    {
        var (modifiers, virtualKey) = Parse(shortcut);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeKeyName(KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString())
        };

        if ((modifiers & ModControl) != 0) keys.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) keys.Add("Alt");
        if ((modifiers & ModShift) != 0) keys.Add("Shift");
        if ((modifiers & ModWin) != 0) keys.Add("Win");
        return keys;
    }

    public bool ProcessWindowMessage(int message, IntPtr wParam)
    {
        if (message != WmHotkey || !_actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            return false;
        }

        HotkeyPressed?.Invoke(action);
        return true;
    }

    public void Apply(IntPtr windowHandle, HotkeySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("The window handle is not available yet.", nameof(windowHandle));
        }

        var previousHandle = _windowHandle;
        var previousSettings = _registeredSettings?.Copy();
        UnregisterCurrent();
        _windowHandle = windowHandle;

        try
        {
            RegisterAll(settings);
            _registeredSettings = settings.Copy();
        }
        catch (Exception registrationException)
        {
            UnregisterCurrent();
            if (previousSettings is not null && previousHandle != IntPtr.Zero)
            {
                _windowHandle = previousHandle;
                try
                {
                    RegisterAll(previousSettings);
                    _registeredSettings = previousSettings;
                }
                catch (Exception restoreException)
                {
                    _registeredSettings = null;
                    throw new InvalidOperationException(
                        $"{registrationException.Message} The previous hotkeys could not be restored: {restoreException.Message}",
                        registrationException);
                }
            }

            throw;
        }
    }

    public void Dispose() => UnregisterCurrent();

    private void RegisterAll(HotkeySettings settings)
    {
        var shortcuts = new[]
        {
            (HotkeyAction.ToggleRecording, settings.RecordToggle),
            (HotkeyAction.TogglePlayback, settings.PlayToggle),
            (HotkeyAction.Stop, settings.Stop),
            (HotkeyAction.TogglePause, settings.PauseToggle)
        };
        var parsed = shortcuts.Select(item => (item.Item1, item.Item2, Parse(item.Item2))).ToArray();
        var duplicate = parsed.GroupBy(item => (item.Item3.Modifiers, item.Item3.VirtualKey))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"The shortcut {duplicate.First().Item2} is assigned to more than one action.");
        }

        foreach (var (action, shortcut, key) in parsed)
        {
            if (!RegisterHotKey(_windowHandle, (int)action, key.Modifiers | ModNoRepeat, key.VirtualKey))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Could not register {action} shortcut '{shortcut}'. It may already be in use (Win32 error {error}).");
            }

            _actions.Add((int)action, action);
        }
    }

    private void UnregisterCurrent()
    {
        foreach (var id in _actions.Keys.ToArray())
        {
            UnregisterHotKey(_windowHandle, id);
        }

        _actions.Clear();
        _registeredSettings = null;
    }

    private static (uint Modifiers, uint VirtualKey) Parse(string shortcut)
    {
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException("Every global hotkey must include a key.");
        }

        uint modifiers = 0;
        var keyName = string.Empty;
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModWin;
                    break;
                default:
                    if (keyName.Length > 0)
                    {
                        throw new InvalidOperationException($"'{shortcut}' contains more than one non-modifier key.");
                    }

                    keyName = part;
                    break;
            }
        }

        if (!Enum.TryParse<Key>(keyName, true, out var key) || key is Key.None or Key.System
            || key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            throw new InvalidOperationException($"'{shortcut}' is not a valid shortcut. Click a shortcut field and press a key combination.");
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            throw new InvalidOperationException($"'{shortcut}' does not map to a Windows virtual key.");
        }

        return (modifiers, virtualKey);
    }

    private static string NormalizeKeyName(string keyName) => keyName switch
    {
        "LeftCtrl" or "RightCtrl" => "Ctrl",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftShift" or "RightShift" => "Shift",
        "LWin" or "RWin" => "Win",
        _ => keyName
    };

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
