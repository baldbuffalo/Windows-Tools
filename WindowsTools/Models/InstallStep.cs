using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace WindowsTools.Models;

public enum StepState { Pending, Active, Done }

public class InstallStep : INotifyPropertyChanged
{
    public string Title { get; init; } = "";

    private StepState _state;
    public StepState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Indicator));
            OnPropertyChanged(nameof(IndicatorBrush));
            OnPropertyChanged(nameof(TitleBrush));
            OnPropertyChanged(nameof(TitleWeight));
        }
    }

    public string Indicator => State switch
    {
        StepState.Done => "✓",
        StepState.Active => "●",
        _ => "○"
    };

    public Brush IndicatorBrush => State == StepState.Pending
        ? new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0))
        : new SolidColorBrush(Color.FromRgb(0x3D, 0xA6, 0x3D));

    public Brush TitleBrush => State == StepState.Pending
        ? new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90))
        : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

    public FontWeight TitleWeight => State == StepState.Active ? FontWeights.SemiBold : FontWeights.Normal;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
