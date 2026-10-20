using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using HornMelodyTimer.Models;
using HornMelodyTimer.Services;
using HornMelodyTimer.Views;

namespace HornMelodyTimer;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _scanTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly NoteRecognizer _recognizer = new();
    private readonly Dictionary<SongEffect, int> _previousCounts = [];
    private readonly Dictionary<string, Dictionary<string, SongSettings>> _scoreSettings = [];
    private RegionIndicatorWindow? _regionIndicator;
    private OverlayWindow? _overlayWindow;
    private Rectangle? _captureRegion;
    private bool _hasMaestro;
    private bool _seedNextScan;
    private bool _showRegionIndicator = true;
    private int _scanInProgress;
    private string _selectedScoreId = "red-yellow";
    private DateTime _lastScan = DateTime.MinValue;

    public ObservableCollection<SongEffect> Songs { get; } = ScoreCatalog.CreateSongs("red-yellow");
    public IReadOnlyList<ScoreOption> ScoreOptions { get; } = ScoreCatalog.All;

    public MainWindow()
    {
        InitializeComponent();
        ShowTimeColumnsCheckBox.IsChecked = true;
        DataContext = this;
        LoadPreferences();
        ScoreSelector.SelectedValue = _selectedScoreId;
        LoadScore(_selectedScoreId, saveCurrent: false);
        _scanTimer.Tick += ScanTimer_Tick;
        _updateTimer.Tick += UpdateTimer_Tick;
        _scanTimer.Start();
        _updateTimer.Start();
        Closed += (_, _) => _regionIndicator?.Close();
        Closing += (_, _) => SavePreferences();
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        var selector = new RegionSelectionWindow();
        selector.RegionSelected += region =>
        {
            _captureRegion = region;
            UpdateRegionIndicator();
            _previousCounts.Clear();
            _seedNextScan = true;
            RegionStatus.Text = $"框選位置：X {region.X}, Y {region.Y}　{region.Width} × {region.Height} px";
            StatusText.Text = "辨識中。請框住提示列音符可能出現的完整範圍，避免包含其他 HUD 區域。";
        };
        selector.ShowDialog();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_overlayWindow is not null)
        {
            _overlayWindow.Activate();
            return;
        }

        SongsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        SongsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        var overlay = new OverlayWindow(
            Songs,
            ShowTimeColumnsCheckBox.IsChecked == true,
            _showRegionIndicator,
            SetRegionIndicatorVisibility);
        _overlayWindow = overlay;
        overlay.Closed += (_, _) =>
        {
            _overlayWindow = null;
            Show();
            Activate();
        };
        overlay.Show();
        Hide();
    }

    private void ResetEffects_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.UtcNow;
        foreach (var song in Songs)
            song.Reset(now);
        StatusText.Text = "已重置目前譜面的所有效果計時。";
    }

    private void ScoreSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ScoreSelector.SelectedItem is not ScoreOption option ||
            option.Id == _selectedScoreId)
            return;

        LoadScore(option.Id, saveCurrent: true);
        _seedNextScan = true;
        StatusText.Text = $"已切換譜面：{option.Name}。效果計時已清空。";
    }

    private void LoadScore(string scoreId, bool saveCurrent)
    {
        if (saveCurrent)
            SaveCurrentScoreSettings();

        _selectedScoreId = scoreId;
        var scoreSongs = ScoreCatalog.CreateSongs(scoreId);

        Songs.Clear();
        foreach (var song in scoreSongs)
        {
            if (_scoreSettings.TryGetValue(scoreId, out var settings))
            {
                settings.TryGetValue(song.NotePattern, out var saved);
                saved ??= song.AcceptedPatterns
                    .Where(pattern => pattern != song.NotePattern)
                    .Select(pattern => settings.TryGetValue(pattern, out var legacy) ? legacy : null)
                    .FirstOrDefault(legacy => legacy is not null);
                if (saved is not null)
                {
                    song.DurationSeconds = saved.DurationSeconds;
                    song.ExtensionSeconds = saved.ExtensionSeconds;
                    song.MaestroDurationSeconds = saved.MaestroDurationSeconds ?? song.MaestroDurationSeconds;
                    song.MaestroExtensionSeconds = saved.MaestroExtensionSeconds ?? song.MaestroExtensionSeconds;
                }
            }
            Songs.Add(song);
        }
        _previousCounts.Clear();
    }

    private void SaveCurrentScoreSettings()
    {
        _scoreSettings[_selectedScoreId] = Songs.ToDictionary(
            song => song.NotePattern,
            song => new SongSettings(
                song.DurationSeconds,
                song.ExtensionSeconds,
                song.MaestroDurationSeconds,
                song.MaestroExtensionSeconds));
    }

    private void Maestro_Changed(object sender, RoutedEventArgs e)
    {
        _hasMaestro = MaestroCheckBox.IsChecked == true;
    }

    private void TimeColumnsVisibility_Changed(object sender, RoutedEventArgs e)
    {
        var visibility = ShowTimeColumnsCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Hidden;
        DurationColumn.Visibility = visibility;
        ExtensionColumn.Visibility = visibility;
        MaestroDurationColumn.Visibility = visibility;
        MaestroExtensionColumn.Visibility = visibility;
        RemainingColumn.Visibility = visibility;
    }

    private void RegionIndicator_Changed(object sender, RoutedEventArgs e)
    {
        SetRegionIndicatorVisibility(ShowRegionIndicatorCheckBox.IsChecked == true);
    }

    private void SetRegionIndicatorVisibility(bool isVisible)
    {
        if (_showRegionIndicator == isVisible)
        {
            if (ShowRegionIndicatorCheckBox.IsChecked != isVisible)
                ShowRegionIndicatorCheckBox.IsChecked = isVisible;
            return;
        }

        _showRegionIndicator = isVisible;
        if (ShowRegionIndicatorCheckBox.IsChecked != isVisible)
            ShowRegionIndicatorCheckBox.IsChecked = isVisible;
        UpdateRegionIndicator();
    }

    private void UpdateRegionIndicator()
    {
        _regionIndicator?.Close();
        _regionIndicator = null;
        if (_showRegionIndicator && _captureRegion is { } region)
        {
            _regionIndicator = new RegionIndicatorWindow(region);
            _regionIndicator.Show();
        }
    }

    private async void ScanTimer_Tick(object? sender, EventArgs e)
    {
        if (_captureRegion is not { Width: > 0, Height: > 0 } region ||
            DateTime.UtcNow - _lastScan < TimeSpan.FromMilliseconds(200) ||
            Interlocked.CompareExchange(ref _scanInProgress, 1, 0) != 0)
            return;

        _lastScan = DateTime.UtcNow;
        try
        {
            var scoreId = _selectedScoreId;
            var acceptedPatterns = Songs.SelectMany(song => song.AcceptedPatterns).ToHashSet(StringComparer.Ordinal);
            var acceptedColors = ScoreCatalog.GetNoteColors(scoreId);
            var result = await Task.Run(() =>
            {
                using var bitmap = new Bitmap(region.Width, region.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(region.Location, System.Drawing.Point.Empty, region.Size);
                var detected = _recognizer.ReadRows(
                    bitmap,
                    acceptedColors,
                    acceptedPatterns,
                    out var scannedSequences);
                return new RecognitionResult(detected, scannedSequences);
            });

            if (_captureRegion != region || _selectedScoreId != scoreId)
                return;

            SetStatus(result.Detected.Count > 0
                ? $"辨識中：{string.Join("、", result.Detected)}"
                : result.ScannedSequences.Count > 0
                    ? $"有讀到音符「{string.Join("、", result.ScannedSequences)}」，但不符合目前譜面；請確認譜面及框選範圍。"
                    : "未讀到譜面音符；請確認框選範圍涵蓋提示列音符可能位置，且未包含其他 HUD 區域。");
            var currentCounts = new Dictionary<SongEffect, int>();
            foreach (var pattern in result.Detected)
            {
                var song = Songs.FirstOrDefault(candidate => candidate.AcceptedPatterns.Contains(pattern));
                if (song is null)
                    continue;
                currentCounts[song] = currentCounts.GetValueOrDefault(song) + 1;
            }

            if (_seedNextScan)
            {
                _previousCounts.Clear();
                foreach (var song in Songs)
                    _previousCounts[song] = currentCounts.GetValueOrDefault(song);
                _seedNextScan = false;
                return;
            }

            foreach (var song in Songs)
            {
                var previousRows = _previousCounts.TryGetValue(song, out var count) ? count : 0;
                var currentRows = currentCounts.GetValueOrDefault(song);
                var previousActivations = previousRows / song.DisplayRowsPerActivation;
                var currentActivations = currentRows / song.DisplayRowsPerActivation;
                for (var i = previousActivations; i < currentActivations; i++)
                {
                    if (song.IsGlobalExtension)
                    {
                        var extension = _hasMaestro
                            ? song.MaestroExtensionSeconds
                            : song.ExtensionSeconds;
                        var extendedCount = Songs
                            .Where(activeSong => !activeSong.IsGlobalExtension)
                            .Count(activeSong => activeSong.ExtendBySeconds(extension, _hasMaestro, DateTime.UtcNow));
                        SetStatus($"全旋律延長：已延長 {extendedCount} 項作用中的效果（+{extension} 秒）。");
                    }
                    else
                    {
                        song.Apply(_hasMaestro);
                    }
                }
            }

            _previousCounts.Clear();
            foreach (var song in Songs)
                _previousCounts[song] = currentCounts.GetValueOrDefault(song);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException)
        {
            SetStatus($"畫面擷取失敗：{ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _scanInProgress, 0);
        }
    }

    private void SetStatus(string message)
    {
        if (!string.Equals(StatusText.Text, message, StringComparison.Ordinal))
            StatusText.Text = message;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var song in Songs)
            song.Update(DateTime.UtcNow);
    }

    private void LoadPreferences()
    {
        var path = GetPreferencesPath();
        if (!File.Exists(path))
            return;

        try
        {
            var preferences = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path));
            if (preferences is null)
                return;
            _selectedScoreId = preferences.SelectedScoreId;
            foreach (var score in preferences.ScoreSongs)
                _scoreSettings[score.Key] = score.Value;
            if (_scoreSettings.TryGetValue("green-blue", out var legacyGreenBlueSettings) &&
                !_scoreSettings.ContainsKey("blue-green"))
                _scoreSettings["blue-green"] = legacyGreenBlueSettings;
            if (_selectedScoreId == "green-blue")
                _selectedScoreId = "blue-green";
            if (preferences.Songs.Count > 0 && !_scoreSettings.ContainsKey("red-yellow"))
                _scoreSettings["red-yellow"] = preferences.Songs;
            MaestroCheckBox.IsChecked = preferences.HasMaestro;
            ShowTimeColumnsCheckBox.IsChecked = preferences.ShowTimeColumns;
            ShowRegionIndicatorCheckBox.IsChecked = preferences.ShowRegionIndicator;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Windows.MessageBox.Show($"讀取設定失敗：{ex.Message}", "MHXX 旋律計時器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SavePreferences()
    {
        SongsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        SongsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        SaveCurrentScoreSettings();
        var preferences = new UserPreferences
        {
            HasMaestro = MaestroCheckBox.IsChecked == true,
            ShowTimeColumns = ShowTimeColumnsCheckBox.IsChecked == true,
            ShowRegionIndicator = ShowRegionIndicatorCheckBox.IsChecked == true,
            SelectedScoreId = _selectedScoreId,
            ScoreSongs = _scoreSettings
        };

        try
        {
            var path = GetPreferencesPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"儲存設定失敗：{ex.Message}", "MHXX 旋律計時器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string GetPreferencesPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HornMelodyTimer", "settings.json");

    public sealed class UserPreferences
    {
        public bool HasMaestro { get; set; }
        public bool ShowTimeColumns { get; set; } = true;
        public bool ShowRegionIndicator { get; set; } = true;
        public string SelectedScoreId { get; set; } = "red-yellow";
        public Dictionary<string, Dictionary<string, SongSettings>> ScoreSongs { get; set; } = [];
        public Dictionary<string, SongSettings> Songs { get; set; } = [];
    }

    public sealed record SongSettings(
        int DurationSeconds,
        int ExtensionSeconds,
        int? MaestroDurationSeconds = null,
        int? MaestroExtensionSeconds = null);

    private sealed record RecognitionResult(
        IReadOnlyList<string> Detected,
        IReadOnlyList<string> ScannedSequences);
}
