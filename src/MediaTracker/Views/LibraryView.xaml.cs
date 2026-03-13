using System.Windows;
using System.Windows.Controls;
using MediaTracker.ViewModels;

namespace MediaTracker.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm && !vm.IsLoading && vm.Items.Count == 0)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
