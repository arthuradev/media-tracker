using System.Windows;
using System.Windows.Controls;
using MediaTracker.ViewModels;

namespace MediaTracker.Views;

public partial class EpisodesView : UserControl
{
    public EpisodesView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EpisodesViewModel vm && !vm.IsLoading && vm.Seasons.Count == 0)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
