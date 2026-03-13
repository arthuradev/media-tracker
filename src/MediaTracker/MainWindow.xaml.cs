using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MediaTracker.ViewModels;

namespace MediaTracker;

public partial class MainWindow : Window
{
    private bool _restoreLibrarySearchFocus;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnCurrentViewChanged(object sender, DataTransferEventArgs e)
    {
        if (sender is not ContentControl contentHost)
            return;

        contentHost.Opacity = 0;
        contentHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void OnInlineSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Escape:
                _restoreLibrarySearchFocus = vm.CanSearch;
                vm.CloseInlineSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                vm.MoveInlineSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                vm.MoveInlineSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (vm.TryImportSelectedInlineResult())
                    e.Handled = true;
                break;
        }
    }

    private void OnInlineSearchVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool isVisible && isVisible && sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(() => tb.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnLibrarySearchVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_restoreLibrarySearchFocus)
            return;

        if (e.NewValue is bool isVisible && isVisible && sender is TextBox tb)
        {
            _restoreLibrarySearchFocus = false;
            tb.Dispatcher.BeginInvoke(() => tb.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnInlineResultMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl itemsControl || DataContext is not MainViewModel vm)
            return;

        var container = ItemsControl.ContainerFromElement(itemsControl, e.OriginalSource as DependencyObject) as FrameworkElement;
        if (container?.DataContext is not Services.Providers.SearchResult result)
            return;

        vm.SelectedInlineResult = result;
        vm.InlineImportCommand.Execute(result);
        e.Handled = true;
    }
}
