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
        if (e.Key == Key.Escape && DataContext is MainViewModel vm)
        {
            vm.CloseInlineSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnInlineSearchVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is Visibility.Visible && sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(() => tb.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
