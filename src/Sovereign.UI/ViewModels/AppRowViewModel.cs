using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Sovereign.Contracts.Ipc;
using Windows.Storage.Streams;
using Windows.UI;

namespace Sovereign.UI.ViewModels;

/// <summary>
/// A bindable view of a single installed app shown on the Apps &amp; debloat page.
/// </summary>
public sealed class AppRowViewModel : INotifyPropertyChanged
{
    private readonly string? _iconBase64;
    private bool _isSelected;
    private bool _isRemoved;
    private ImageSource? _iconSource;

    /// <summary>Creates a view model from the service's app info.</summary>
    /// <param name="info">The app info from the service.</param>
    public AppRowViewModel(AppInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        this.PackageFullName = info.PackageFullName;
        this.DisplayName = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Name : info.DisplayName;
        this.Name = info.Name;
        this.Description = info.Description;
        this.Publisher = info.Publisher;
        this.Category = info.Category;
        this.Recommended = info.Recommended;
        this.Removable = info.Removable;
        this.IsSystem = info.IsSystem;
        this.HasStartEntry = info.HasStartEntry;
        this.Kind = info.Kind;
        this.Reversible = info.Reversible;
        this._iconBase64 = info.IconBase64;
        this._isSelected = info.Recommended && info.Removable;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The unique package full name.</summary>
    public string PackageFullName { get; }

    /// <summary>The friendly display name.</summary>
    public string DisplayName { get; }

    /// <summary>The underlying package name.</summary>
    public string Name { get; }

    /// <summary>A short, human description of the app.</summary>
    public string Description { get; }

    /// <summary>A short publisher label.</summary>
    public string Publisher { get; }

    /// <summary>The grouping category.</summary>
    public string Category { get; }

    /// <summary>Whether Sovereign recommends removing this as bloat.</summary>
    public bool Recommended { get; }

    /// <summary>Whether this app may be removed (false for protected system apps).</summary>
    public bool Removable { get; }

    /// <summary>Whether this is a system / background package.</summary>
    public bool IsSystem { get; }

    /// <summary>Whether the app appears in the Start menu (user-facing).</summary>
    public bool HasStartEntry { get; }

    /// <summary>The entry kind: <c>appx</c> for a Store/MSIX package, <c>win32</c> for a classic program.</summary>
    public string Kind { get; }

    /// <summary>Whether removal can be undone (Store apps can be reinstalled; classic uninstalls cannot).</summary>
    public bool Reversible { get; }

    /// <summary>Whether this entry is a classic Win32 program.</summary>
    public bool IsProgram => string.Equals(this.Kind, "win32", StringComparison.OrdinalIgnoreCase);

    /// <summary>A short source label shown next to the publisher.</summary>
    public string SourceLabel => this.IsProgram ? "Installed program" : "Store app";

    /// <summary>The publisher line shown in the list, including the source.</summary>
    public string PublisherLine => string.IsNullOrWhiteSpace(this.Publisher)
        ? this.SourceLabel
        : $"{this.Publisher}  ·  {this.SourceLabel}";

    /// <summary>Whether the checkbox can be toggled.</summary>
    public bool CanSelect => this.Removable && !this.IsRemoved;

    /// <summary>The badge text.</summary>
    public string BadgeText => !this.Removable ? "Protected" : this.Recommended ? "Recommended" : this.IsSystem ? "System" : "Optional";

    /// <summary>A color hint for the badge.</summary>
    public Brush BadgeBrush => !this.Removable ? Bad : this.Recommended ? Warn : this.IsSystem ? SystemBadge : Neutral;

    /// <summary>The resolved app icon, if any.</summary>
    public ImageSource? IconSource
    {
        get => this._iconSource;
        private set
        {
            if (this.Set(ref this._iconSource, value))
            {
                this.OnChanged(nameof(this.IconVisibility));
                this.OnChanged(nameof(this.GlyphVisibility));
            }
        }
    }

    /// <summary>Whether a resolved icon is shown.</summary>
    public Visibility IconVisibility => this._iconSource is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Whether the fallback glyph is shown.</summary>
    public Visibility GlyphVisibility => this._iconSource is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Whether the user has selected this app for removal.</summary>
    public bool IsSelected
    {
        get => this._isSelected;
        set => this.Set(ref this._isSelected, value);
    }

    /// <summary>Whether this app has already been removed in this session.</summary>
    public bool IsRemoved
    {
        get => this._isRemoved;
        set
        {
            if (this.Set(ref this._isRemoved, value))
            {
                this.OnChanged(nameof(this.CanSelect));
                this.OnChanged(nameof(this.StatusText));
                this.OnChanged(nameof(this.RowOpacity));
            }
        }
    }

    /// <summary>A short status label shown after removal.</summary>
    public string StatusText => this.IsRemoved ? "Removed" : string.Empty;

    /// <summary>Dims the row once removed.</summary>
    public double RowOpacity => this.IsRemoved ? 0.45 : 1.0;

    /// <summary>Decodes the icon bytes into an image source. Must be called on the UI thread.</summary>
    public async Task LoadIconAsync()
    {
        if (string.IsNullOrEmpty(this._iconBase64))
        {
            return;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(this._iconBase64);
            var bitmap = new BitmapImage { DecodePixelWidth = 48 };
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
            this.IconSource = bitmap;
        }
        catch (Exception)
        {
            // A bad icon is non-fatal; the fallback glyph stays.
        }
    }

    private static SolidColorBrush Warn => new(Color.FromArgb(255, 201, 138, 19));
    private static SolidColorBrush Bad => new(Color.FromArgb(255, 197, 57, 41));
    private static SolidColorBrush SystemBadge => new(Color.FromArgb(255, 90, 110, 150));
    private static SolidColorBrush Neutral => new(Color.FromArgb(255, 120, 120, 120));

    private void OnChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        this.OnChanged(name ?? string.Empty);
        return true;
    }
}
