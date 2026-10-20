using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MyTask.Models;
using MyTask.Services;
using MyTask.ViewModels;

namespace MyTask;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Process _process = Process.GetCurrentProcess();
    private HwndSource? _windowSource;
    private HotkeySettings _registeredHotkeys = new();
    private TimeSpan _previousCpuTime;
    private DateTime _previousCpuCheck;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Notification += ViewModel_Notification;
        _hotkeyManager.HotkeyPressed += HotkeyManager_HotkeyPressed;
        _monitorTimer.Tick += MonitorTimer_Tick;
        ShowPage("Dashboard");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _process.Refresh();
        _previousCpuTime = _process.TotalProcessorTime;
        _previousCpuCheck = DateTime.UtcNow;
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("Could not access the MyTask window message source.");
        _windowSource.AddHook(WindowProc);
        _registeredHotkeys = _viewModel.Hotkeys.Copy();
        try
        {
            _hotkeyManager.Apply(handle, _registeredHotkeys);
        }
        catch (Exception exception)
        {
            _viewModel.Status = $"Global hotkeys could not be registered: {exception.Message}";
            _viewModel.LogError("Register global hotkeys", exception);
        }

        _monitorTimer.Start();
        UpdateMonitor();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _monitorTimer.Stop();
        _windowSource?.RemoveHook(WindowProc);
        _hotkeyManager.Dispose();
        _viewModel.Dispose();
        _process.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsRecording) or nameof(MainViewModel.IsPlaying) or nameof(MainViewModel.Status))
        {
            var active = _viewModel.IsRecording || _viewModel.IsPlaying;
            StatusDot.Fill = active ? new SolidColorBrush(Color.FromRgb(255, 100, 127))
                                    : new SolidColorBrush(Color.FromRgb(87, 214, 160));
            HeaderState.Text = _viewModel.IsRecording ? "RECORDING"
                : _viewModel.IsPaused ? "PAUSED"
                : _viewModel.IsPlaying ? "PLAYING" : "READY";
            PlayButton.Content = _viewModel.IsPlaying ? "■  Stop playback" : "▶  Play macro";
            PauseButton.Visibility = _viewModel.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            PauseButton.Content = _viewModel.IsPaused ? "▶  Resume" : "Ⅱ  Pause";
            HookStatus.Text = _viewModel.IsRecording ? "Active" : "Inactive";
            HookStatus.Foreground = _viewModel.IsRecording
                ? new SolidColorBrush(Color.FromRgb(255, 100, 127))
                : new SolidColorBrush(Color.FromRgb(87, 214, 160));
        }
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e) => UpdateMonitor();

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = _hotkeyManager.ProcessWindowMessage(message, wParam);
        return IntPtr.Zero;
    }

    private void HotkeyManager_HotkeyPressed(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleRecording:
                if (_viewModel.IsRecording)
                {
                    _viewModel.RemoveTrailingHotkeyEvents(_registeredHotkeys.RecordToggle);
                    _viewModel.StopRecording();
                }
                else
                {
                    _ = StartRecordingAfterHotkeyAsync();
                }
                break;
            case HotkeyAction.TogglePlayback:
                if (_viewModel.IsPlaying)
                {
                    _viewModel.StopPlayback();
                }
                else
                {
                    if (_viewModel.IsRecording)
                    {
                        _viewModel.RemoveTrailingHotkeyEvents(_registeredHotkeys.PlayToggle);
                        _viewModel.StopRecording();
                    }

                    _ = PlayMacroAsync();
                }
                break;
            case HotkeyAction.Stop:
                if (_viewModel.IsRecording)
                {
                    _viewModel.RemoveTrailingHotkeyEvents(_registeredHotkeys.Stop);
                }

                _viewModel.StopAll();
                break;
            case HotkeyAction.TogglePause:
                _viewModel.TogglePause();
                break;
        }
    }

    private async Task StartRecordingAfterHotkeyAsync()
    {
        await Task.Delay(200);
        _viewModel.StartRecording();
        ShowPage("Studio");
    }

    private void ViewModel_Notification(string message)
    {
        MessageBox.Show(message, "MyTask", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void UpdateMonitor()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var currentCpuTime = _process.TotalProcessorTime;
        var wallTime = (now - _previousCpuCheck).TotalMilliseconds;
        var cpuTime = (currentCpuTime - _previousCpuTime).TotalMilliseconds;
        var cpuPercent = wallTime <= 0 ? 0 : cpuTime / (wallTime * Environment.ProcessorCount) * 100;
        CpuUsage.Text = $"{Math.Clamp(cpuPercent, 0, 100):0.0}%";
        MemoryUsage.Text = $"{_process.WorkingSet64 / 1024d / 1024d:0} MB";
        _previousCpuTime = currentCpuTime;
        _previousCpuCheck = now;
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowPage("Dashboard");
    private void StudioNav_Click(object sender, RoutedEventArgs e) => ShowPage("Studio");
    private void ScriptsNav_Click(object sender, RoutedEventArgs e) => ShowPage("Scripts");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage("Settings");

    private void ShowPage(string page)
    {
        _viewModel.SelectedPage = page;
        DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        StudioPage.Visibility = page == "Studio" ? Visibility.Visible : Visibility.Collapsed;
        ScriptsPage.Visibility = page == "Scripts" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch
        {
            "Studio" => "Recorder studio",
            "Scripts" => "Script library",
            "Settings" => "Settings",
            _ => "Execution hub"
        };
        PageEyebrow.Text = page switch
        {
            "Studio" => "CREATE & EDIT",
            "Scripts" => "YOUR COLLECTION",
            "Settings" => "PREFERENCES",
            _ => "OVERVIEW"
        };
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsPlaying)
        {
            _viewModel.StopPlayback();
            return;
        }

        if (_viewModel.Events.Count == 0)
        {
            ShowPage("Studio");
            _viewModel.Status = "Record a macro or add events in the studio before playing.";
            return;
        }

        await PlayMacroAsync();
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();

    private async Task PlayMacroAsync()
    {
        if (_viewModel.Events.Count == 0)
        {
            _viewModel.Status = "Record a macro or add events in the studio before playing.";
            return;
        }

        try
        {
            await _viewModel.PlayAsync();
        }
        catch (Exception exception)
        {
            _viewModel.LogError("Playback", exception);
            MessageBox.Show(exception.Message, "Playback failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HotkeyField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBox textBox)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            _viewModel.Status = "Press a non-modifier key with your chosen modifiers.";
            return;
        }

        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        var shortcut = string.Join("+", parts);

        switch (textBox.Tag as string)
        {
            case "RecordToggle":
                _viewModel.Hotkeys.RecordToggle = shortcut;
                break;
            case "PlayToggle":
                _viewModel.Hotkeys.PlayToggle = shortcut;
                break;
            case "Stop":
                _viewModel.Hotkeys.Stop = shortcut;
                break;
            case "PauseToggle":
                _viewModel.Hotkeys.PauseToggle = shortcut;
                break;
        }
    }

    private async void SaveHotkeys_Click(object sender, RoutedEventArgs e)
    {
        var requested = _viewModel.Hotkeys.Copy();
        try
        {
            _hotkeyManager.Apply(new WindowInteropHelper(this).Handle, requested);
            await _viewModel.SaveHotkeysAsync();
            _registeredHotkeys = requested.Copy();
        }
        catch (Exception exception)
        {
            try
            {
                _hotkeyManager.Apply(new WindowInteropHelper(this).Handle, _registeredHotkeys);
            }
            catch (Exception restoreException)
            {
                _viewModel.LogError("Restore global hotkeys", restoreException);
                _viewModel.Status = $"Hotkey update failed and the previous shortcuts could not be restored: {restoreException.Message}";
            }

            _viewModel.ReplaceHotkeys(_registeredHotkeys);
            _viewModel.LogError("Save global hotkeys", exception);
            MessageBox.Show(exception.Message, "Hotkeys could not be applied",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ReplaceHotkeys(new HotkeySettings());
        _viewModel.Status = "Default shortcuts restored in the form. Click Apply hotkeys to register them.";
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StartRecording();
        ShowPage("Studio");
    }

    private async void SaveRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.SaveRecordingAsync();
        }
        catch (Exception exception)
        {
            _viewModel.Status = $"Could not save recording: {exception.Message}";
            _viewModel.LogError("Save recording", exception);
            MessageBox.Show(exception.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveScript_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        try
        {
            await _viewModel.SaveScriptAsync();
        }
        catch (Exception exception)
        {
            _viewModel.Status = $"Could not save script: {exception.Message}";
            _viewModel.LogError("Save script", exception);
            MessageBox.Show(exception.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshScripts_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshScripts();

    private async void ScriptGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ScriptGrid.SelectedItem is not ScriptFile script)
        {
            return;
        }

        try
        {
            await _viewModel.LoadScriptAsync(script);
            ShowPage("Studio");
        }
        catch (Exception exception)
        {
            _viewModel.Status = $"Could not load script: {exception.Message}";
            _viewModel.LogError("Load script", exception);
            MessageBox.Show(exception.Message, "Script could not be loaded",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_viewModel.DataFolder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _viewModel.LogError("Open data folder", exception);
            MessageBox.Show(exception.Message, "Could not open data folder",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
