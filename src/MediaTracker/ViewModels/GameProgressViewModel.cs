using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class GameProgressViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly int _mediaItemId;

    [ObservableProperty]
    private double? _hoursPlayed;

    [ObservableProperty]
    private string? _currentStage;

    [ObservableProperty]
    private string? _platform;

    [ObservableProperty]
    private CompletionState _completionState = CompletionState.NotStarted;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    public Array CompletionStates => Enum.GetValues<CompletionState>();

    public GameProgressViewModel(MediaService mediaService, int mediaItemId, GameProgress? existing)
    {
        _mediaService = mediaService;
        _mediaItemId = mediaItemId;

        if (existing is not null)
        {
            HoursPlayed = existing.HoursPlayed;
            CurrentStage = existing.CurrentStage;
            Platform = existing.Platform;
            CompletionState = existing.CompletionState;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        ErrorMessage = MediaInputValidator.ValidateGameProgress(HoursPlayed);

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            IsSaving = false;
            return;
        }

        var progress = new GameProgress
        {
            MediaItemId = _mediaItemId,
            HoursPlayed = HoursPlayed,
            CurrentStage = CurrentStage?.Trim(),
            Platform = Platform?.Trim(),
            CompletionState = CompletionState
        };

        try
        {
            await _mediaService.UpdateGameProgressAsync(progress);
            StatusMessage = "Progress saved.";
        }
        catch (Exception)
        {
            ErrorMessage = "Could not save game progress right now.";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
