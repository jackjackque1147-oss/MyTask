using CommunityToolkit.Mvvm.ComponentModel;

namespace MyTask.Models;

public sealed class HotkeySettings : ObservableObject
{
    private string _recordToggle = "Ctrl+Alt+F9";
    private string _playToggle = "Ctrl+Alt+F10";
    private string _stop = "Ctrl+Alt+F11";
    private string _pauseToggle = "Ctrl+Alt+F12";

    public string RecordToggle
    {
        get => _recordToggle;
        set => SetProperty(ref _recordToggle, value);
    }

    public string PlayToggle
    {
        get => _playToggle;
        set => SetProperty(ref _playToggle, value);
    }

    public string Stop
    {
        get => _stop;
        set => SetProperty(ref _stop, value);
    }

    public string PauseToggle
    {
        get => _pauseToggle;
        set => SetProperty(ref _pauseToggle, value);
    }

    public HotkeySettings Copy() => new()
    {
        RecordToggle = RecordToggle,
        PlayToggle = PlayToggle,
        Stop = Stop,
        PauseToggle = PauseToggle
    };
}
