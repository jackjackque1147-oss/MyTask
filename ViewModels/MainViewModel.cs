using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyTask.Models;
using MyTask.Services;

namespace MyTask.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppPaths _paths = new();
    private readonly GlobalInputRecorder _recorder = new();
    private readonly MacroPlayer _player = new();
    private readonly PlaybackPauseGate _pauseGate = new();
    private CancellationTokenSource? _playCancellation;
    private string _status = "Ready";
    private string _selectedPage = "Dashboard";
    private string _activeScriptName = "Untitled macro";
    private bool _isPlaying;
    private AutomationEvent? _selectedEvent;
    private string _loopSelection = "1";
    private long _completedLoops;
    private HotkeySettings _hotkeys = new();

    public MainViewModel()
    {
        _paths.EnsureDirectories();
        LoadHotkeys();
        _recorder.EventRecorded += OnEventRecorded;
        RefreshScripts();
        StartRecordingCommand = new RelayCommand(StartRecording, () => !IsPlaying && !_recorder.IsRecording);
        StopRecordingCommand = new RelayCommand(StopRecording, () => _recorder.IsRecording);
        AddWaitCommand = new RelayCommand(() => Events.Add(new AutomationEvent { Type = "Wait", DelayMs = 500 }));
        AddKeyCommand = new RelayCommand(AddKey);
        AddClickCommand = new RelayCommand(AddClick);
        DuplicateEventCommand = new RelayCommand(DuplicateSelected, () => SelectedEvent is not null);
        DeleteEventCommand = new RelayCommand(DeleteSelected, () => SelectedEvent is not null);
    }

    public ObservableCollection<AutomationEvent> Events { get; } = [];
    public ObservableCollection<ScriptFile> Scripts { get; } = [];
    public string[] LoopOptions { get; } = ["1", "2", "3", "5", "10", "Unlimited"];
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand AddWaitCommand { get; }
    public ICommand AddKeyCommand { get; }
    public ICommand AddClickCommand { get; }
    public ICommand DuplicateEventCommand { get; }
    public ICommand DeleteEventCommand { get; }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string SelectedPage
    {
        get => _selectedPage;
        set => SetField(ref _selectedPage, value);
    }

    public string ActiveScriptName
    {
        get => _activeScriptName;
        set => SetField(ref _activeScriptName, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetField(ref _isPlaying, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsRecording => _recorder.IsRecording;
    public bool IsPaused => _pauseGate.IsPaused;

    public HotkeySettings Hotkeys
    {
        get => _hotkeys;
        private set => SetField(ref _hotkeys, value);
    }

    public AutomationEvent? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (SetField(ref _selectedEvent, value))
            {
                RefreshCommands();
            }
        }
    }

    public string LoopSelection
    {
        get => _loopSelection;
        set => SetField(ref _loopSelection, value);
    }

    public long CompletedLoops
    {
        get => _completedLoops;
        private set => SetField(ref _completedLoops, value);
    }

    public string DataFolder => _paths.Root;

    public event Action<string>? Notification;

    public void StartRecording()
    {
        if (IsPlaying)
        {
            Status = "Stop playback before recording.";
            return;
        }

        if (_recorder.IsRecording)
        {
            Status = "Recording is already active.";
            return;
        }

        try
        {
            Events.Clear();
            _recorder.Start();
            OnPropertyChanged(nameof(IsRecording));
            RefreshCommands();
            Status = "Recording keyboard and mouse input. Stop when finished.";
        }
        catch (Exception exception)
        {
            Status = $"Could not start recording: {exception.Message}";
            RefreshCommands();
            LogError("Start recording", exception);
            Notification?.Invoke(exception.Message);
        }
    }

    public void StopRecording()
    {
        _recorder.Stop();
        OnPropertyChanged(nameof(IsRecording));
        RefreshCommands();
        Status = $"Recording stopped · {Events.Count} events captured";
    }

    public async Task PlayAsync()
    {
        if (Events.Count == 0)
        {
            throw new InvalidOperationException("Add or record at least one event before playing.");
        }

        if (_recorder.IsRecording)
        {
            StopRecording();
        }

        var snapshot = Events.Select(item => item.Copy()).ToArray();
        var loopLimit = LoopSelection == "Unlimited" ? (long?)null : long.Parse(LoopSelection);
        _pauseGate.Resume();
        var cancellation = new CancellationTokenSource();
        _playCancellation = cancellation;
        IsPlaying = true;
        CompletedLoops = 0;
        Status = "Starting in 3 seconds · switch to the target application";
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellation.Token);
            while (loopLimit is null || CompletedLoops < loopLimit.Value)
            {
                Status = IsPaused
                    ? $"Paused · {CompletedLoops} loops completed"
                    : loopLimit is null
                        ? $"Playing · {CompletedLoops} loops · Unlimited"
                        : $"Playing · {CompletedLoops} / {loopLimit.Value} loops";
                await Task.Run(
                    () => _player.PlayAsync(snapshot, 1, cancellation.Token,
                        () => _pauseGate.IsPaused, _pauseGate.WaitUntilResumedAsync),
                    cancellation.Token);
                CompletedLoops++;
                Status = loopLimit is null
                    ? $"Playing · {CompletedLoops} loops · Unlimited"
                    : $"Playing · {CompletedLoops} / {loopLimit.Value} loops";
            }

            Status = $"Playback complete · {CompletedLoops} loops";
        }
        catch (OperationCanceledException)
        {
            Status = "Playback stopped";
        }
        catch (Exception exception)
        {
            Status = $"Playback failed: {exception.Message}";
            LogError("Playback", exception);
            throw;
        }
        finally
        {
            IsPlaying = false;
            cancellation.Dispose();
            _playCancellation = null;
        }
    }

    public void StopPlayback()
    {
        _pauseGate.Resume();
        _playCancellation?.Cancel();
        OnPropertyChanged(nameof(IsPaused));
    }

    public void TogglePause()
    {
        if (!IsPlaying)
        {
            Status = "Start playback before pausing.";
            return;
        }

        if (IsPaused)
        {
            _pauseGate.Resume();
            Status = $"Playback resumed · {CompletedLoops} loops completed";
        }
        else
        {
            _pauseGate.Pause();
            Status = $"Playback paused · {CompletedLoops} loops completed";
        }

        OnPropertyChanged(nameof(IsPaused));
    }

    public void ToggleRecording()
    {
        if (_recorder.IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    public void StopAll()
    {
        if (_recorder.IsRecording)
        {
            StopRecording();
        }

        StopPlayback();
    }

    public void RemoveTrailingHotkeyEvents(string shortcut)
    {
        IReadOnlySet<string> hotkeyKeys;
        try
        {
            hotkeyKeys = GlobalHotkeyManager.GetRecordedKeyNames(shortcut);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var startIndex = Events.Count;
        while (startIndex > 0)
        {
            var item = Events[startIndex - 1];
            if (item.Type is not ("Key Down" or "Key Up")
                || !hotkeyKeys.Contains(NormalizeRecordedKey(item.Key)))
            {
                break;
            }

            startIndex--;
        }

        var downKeys = Events.Skip(startIndex)
            .Where(item => item.Type == "Key Down")
            .Select(item => NormalizeRecordedKey(item.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!hotkeyKeys.All(downKeys.Contains))
        {
            return;
        }

        for (var index = Events.Count - 1; index >= startIndex; index--)
        {
            Events.RemoveAt(index);
        }
    }

    public void ReplaceHotkeys(HotkeySettings settings)
    {
        Hotkeys = settings.Copy();
    }

    public async Task SaveHotkeysAsync()
    {
        var path = Path.Combine(_paths.Config, "hotkeys.json");
        var json = JsonSerializer.Serialize(Hotkeys, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        Status = "Global hotkeys saved.";
    }

    private void LoadHotkeys()
    {
        var path = Path.Combine(_paths.Config, "hotkeys.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            Hotkeys = JsonSerializer.Deserialize<HotkeySettings>(File.ReadAllText(path))
                ?? throw new InvalidDataException("The hotkey settings file is empty.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            Status = $"Could not load hotkey settings: {exception.Message}";
            LogError("Load hotkey settings", exception);
        }
    }

    public void LogError(string operation, Exception exception)
    {
        try
        {
            var entry = $"{DateTimeOffset.Now:O} [ERROR] {operation}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(_paths.Logs, "mytask.log"), entry);
        }
        catch (Exception logException)
        {
            Status = $"{Status} · Could not write error log: {logException.Message}";
        }
    }

    public async Task SaveRecordingAsync()
    {
        var name = $"Recording-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
        var path = Path.Combine(_paths.Recorded, name);
        await SaveEventsAsync(path);
        Status = $"Recording saved · {path}";
    }

    public async Task SaveScriptAsync()
    {
        var safeName = MakeSafeFileName(ActiveScriptName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"Macro-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        var path = Path.Combine(_paths.Scripts, safeName + ".mytask");
        var suffix = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(_paths.Scripts, $"{safeName}-{suffix++}.mytask");
        }

        await SaveEventsAsync(path);
        RefreshScripts();
        ActiveScriptName = Path.GetFileNameWithoutExtension(path);
        Status = $"Script saved · {path}";
    }

    public async Task LoadScriptAsync(ScriptFile? script)
    {
        if (script is null)
        {
            return;
        }

        var json = await File.ReadAllTextAsync(script.FullPath);
        var events = JsonSerializer.Deserialize<List<AutomationEvent>>(json)
            ?? throw new InvalidDataException("The selected script does not contain a valid event list.");
        Events.Clear();
        foreach (var item in events)
        {
            Events.Add(item);
        }

        ActiveScriptName = Path.GetFileNameWithoutExtension(script.Name);
        SelectedPage = "Studio";
        Status = $"Loaded {ActiveScriptName} · {Events.Count} events";
    }

    public void RefreshScripts()
    {
        Scripts.Clear();
        foreach (var path in Directory.EnumerateFiles(_paths.Scripts, "*.mytask").OrderBy(Path.GetFileName))
        {
            Scripts.Add(new ScriptFile(Path.GetFileName(path), path));
        }

        OnPropertyChanged(nameof(Scripts));
    }

    private async Task SaveEventsAsync(string path)
    {
        if (Events.Count == 0)
        {
            throw new InvalidOperationException("There are no events to save.");
        }

        var snapshot = Events.Select(item => item.Copy()).ToArray();
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private void AddKey()
    {
        Events.Add(new AutomationEvent { Type = "Key Down", Key = "A" });
        Events.Add(new AutomationEvent { Type = "Key Up", Key = "A" });
        Status = "Added an A key press. Edit the event rows to customize it.";
    }

    private void AddClick()
    {
        var point = GetCursorPosition();
        Events.Add(new AutomationEvent { Type = "Left Down", X = point.X, Y = point.Y });
        Events.Add(new AutomationEvent { Type = "Left Up", X = point.X, Y = point.Y });
        Status = $"Added a left click at {point.X}, {point.Y}.";
    }

    private void DuplicateSelected()
    {
        if (SelectedEvent is null)
        {
            return;
        }

        var index = Events.IndexOf(SelectedEvent);
        Events.Insert(index + 1, SelectedEvent.Copy());
    }

    private void DeleteSelected()
    {
        if (SelectedEvent is not null)
        {
            Events.Remove(SelectedEvent);
        }
    }

    private void OnEventRecorded(AutomationEvent item)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(() => OnEventRecorded(item));
            return;
        }

        if (Events.Count >= 20000)
        {
            StopRecording();
            Status = "Recording stopped at the 20,000-event safety limit. Save or clear the event list to continue.";
            return;
        }

        Events.Add(item);
    }

    private void RefreshCommands()
    {
        (StartRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StopRecordingCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DuplicateEventCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeleteEventCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Trim().Where(character => !invalid.Contains(character)).ToArray());
    }

    private static string NormalizeRecordedKey(string keyName) => keyName switch
    {
        "LeftCtrl" or "RightCtrl" => "Ctrl",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftShift" or "RightShift" => "Shift",
        "LWin" or "RWin" => "Win",
        _ => keyName
    };

    private static (int X, int Y) GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new InvalidOperationException($"Windows could not read the cursor position (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return (point.X, point.Y);
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        SetProperty(ref field, value, propertyName);

    public void Dispose()
    {
        _recorder.Dispose();
        _playCancellation?.Cancel();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
