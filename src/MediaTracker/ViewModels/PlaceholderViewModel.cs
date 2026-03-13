using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MediaTracker.ViewModels;

public partial class PlaceholderViewModel : ObservableObject
{
    private readonly Action _onPrimaryAction;
    private readonly Action? _onSecondaryAction;

    public string Eyebrow { get; }
    public string Title { get; }
    public string Description { get; }
    public string PrimaryActionText { get; }
    public string? SecondaryActionText { get; }
    public bool HasSecondaryAction => !string.IsNullOrWhiteSpace(SecondaryActionText);

    public PlaceholderViewModel(
        string eyebrow,
        string title,
        string description,
        string primaryActionText,
        string? secondaryActionText,
        Action onPrimaryAction,
        Action? onSecondaryAction = null)
    {
        Eyebrow = eyebrow;
        Title = title;
        Description = description;
        PrimaryActionText = primaryActionText;
        SecondaryActionText = secondaryActionText;
        _onPrimaryAction = onPrimaryAction;
        _onSecondaryAction = onSecondaryAction;
    }

    [RelayCommand]
    private void PrimaryAction() => _onPrimaryAction();

    [RelayCommand]
    private void SecondaryAction()
    {
        _onSecondaryAction?.Invoke();
    }
}
