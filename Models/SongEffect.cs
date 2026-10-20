using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HornMelodyTimer.Models;

public sealed class SongEffect : INotifyPropertyChanged
{
    private DateTime _expiresAt;
    private bool _isActive;
    private int _durationSeconds;
    private int _extensionSeconds;
    private int _maestroDurationSeconds;
    private int _maestroExtensionSeconds;
    private string _remainingText = "--";
    private string _displayColor = "#F2E8D8";

    public string Name { get; init; } = "";
    public string Notes { get; init; } = "";
    public string NotePattern { get; init; } = "";
    public IReadOnlyList<char> NoteCodes => NotePattern.ToCharArray();
    public string[] AcceptedPatterns { get; init; } = [];
    public bool IsGlobalExtension { get; init; }
    public int DisplayRowsPerActivation { get; init; } = 1;

    public int DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            _durationSeconds = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public int ExtensionSeconds
    {
        get => _extensionSeconds;
        set
        {
            _extensionSeconds = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public int MaestroDurationSeconds
    {
        get => _maestroDurationSeconds;
        set
        {
            _maestroDurationSeconds = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public int MaestroExtensionSeconds
    {
        get => _maestroExtensionSeconds;
        set
        {
            _maestroExtensionSeconds = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public string RemainingText
    {
        get => _remainingText;
        private set
        {
            if (_remainingText == value)
                return;
            _remainingText = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (_isActive == value)
                return;
            _isActive = value;
            OnPropertyChanged();
        }
    }

    public string DisplayColor
    {
        get => _displayColor;
        private set
        {
            if (_displayColor == value)
                return;
            _displayColor = value;
            OnPropertyChanged();
        }
    }

    public void Apply(bool hasMaestro)
    {
        Apply(hasMaestro, DateTime.UtcNow);
    }

    public void Apply(bool hasMaestro, DateTime now)
    {
        var active = _expiresAt > now;
        var duration = hasMaestro ? MaestroDurationSeconds : DurationSeconds;
        var extension = hasMaestro ? MaestroExtensionSeconds : ExtensionSeconds;
        if (duration <= 0)
        {
            IsActive = false;
            RemainingText = "--";
            return;
        }

        if (!active)
        {
            _expiresAt = now.AddSeconds(duration);
        }
        else
        {
            var extendedExpiry = _expiresAt.AddSeconds(extension);
            var maximumExpiry = now.AddSeconds(duration);
            _expiresAt = extendedExpiry < maximumExpiry ? extendedExpiry : maximumExpiry;
        }
        Update(now);
    }

    public bool Extend(bool hasMaestro, DateTime now)
    {
        var extension = hasMaestro ? MaestroExtensionSeconds : ExtensionSeconds;
        return ExtendBySeconds(extension, hasMaestro, now);
    }

    public bool ExtendBySeconds(int extensionSeconds, bool hasMaestro, DateTime now)
    {
        if (_expiresAt <= now || extensionSeconds <= 0)
            return false;

        var duration = hasMaestro ? MaestroDurationSeconds : DurationSeconds;
        if (duration <= 0)
            return false;

        var extendedExpiry = _expiresAt.AddSeconds(extensionSeconds);
        var maximumExpiry = now.AddSeconds(duration);
        var newExpiry = extendedExpiry < maximumExpiry ? extendedExpiry : maximumExpiry;
        if (newExpiry <= _expiresAt)
            return false;

        _expiresAt = newExpiry;
        Update(now);
        return true;
    }

    public void Reset(DateTime now)
    {
        _expiresAt = now;
        Update(now);
    }

    public void Update(DateTime now)
    {
        var remaining = Math.Max(0, (int)Math.Ceiling((_expiresAt - now).TotalSeconds));
        IsActive = remaining > 0;
        RemainingText = IsActive ? $"{remaining}s" : "--";
        var shouldFlash = IsActive && remaining <= 20;
        var flashOn = !shouldFlash || (now.Ticks / TimeSpan.FromMilliseconds(500).Ticks) % 2 == 0;
        DisplayColor = !IsActive || (shouldFlash && !flashOn) ? "#F2E8D8" : "#FFE22E";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
