using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyTask.Models;

public sealed class AutomationEvent : INotifyPropertyChanged
{
    private string _type = "Wait";
    private string _key = string.Empty;
    private int _x;
    private int _y;
    private int _delayMs;
    private int _value;

    public string Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    public string Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public int DelayMs
    {
        get => _delayMs;
        set => SetField(ref _delayMs, Math.Max(0, value));
    }

    public int Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AutomationEvent Copy() => new()
    {
        Type = Type,
        Key = Key,
        X = X,
        Y = Y,
        DelayMs = DelayMs,
        Value = Value
    };

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
