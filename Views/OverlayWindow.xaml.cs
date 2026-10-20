using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HornMelodyTimer.Models;

namespace HornMelodyTimer.Views;

public partial class OverlayWindow : Window
{
    private readonly Action<bool> _regionIndicatorVisibilityChanged;

    public OverlayWindow(
        ObservableCollection<SongEffect> songs,
        bool showTimeColumns,
        bool showRegionIndicator,
        Action<bool> regionIndicatorVisibilityChanged)
    {
        InitializeComponent();
        _regionIndicatorVisibilityChanged = regionIndicatorVisibilityChanged;
        RegionIndicatorCheckBox.IsChecked = showRegionIndicator;
        DataContext = songs;
        var rows = Math.Max(1, songs.Count);
        SongsGrid.Height = 38 + rows * SongsGrid.RowHeight;
        SongsGrid.Width = showTimeColumns ? 980 : 620;
        if (!showTimeColumns)
        {
            DurationColumn.Visibility = Visibility.Collapsed;
            ExtensionColumn.Visibility = Visibility.Collapsed;
            MaestroDurationColumn.Visibility = Visibility.Collapsed;
            MaestroExtensionColumn.Visibility = Visibility.Collapsed;
            RemainingColumn.Visibility = Visibility.Collapsed;
        }
    }

    private void Return_Click(object sender, RoutedEventArgs e) => Close();

    private void RegionIndicator_Changed(object sender, RoutedEventArgs e) =>
        _regionIndicatorVisibilityChanged(RegionIndicatorCheckBox.IsChecked == true);

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
