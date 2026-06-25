using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using Sovereign.Contracts;
using Sovereign.Contracts.Ipc;
using Windows.UI;

namespace Sovereign.UI.ViewModels;

/// <summary>
/// A bindable view of a single managed policy used by the Cleanup page. Tracks the user's selection
/// and the latest detected state so tiles can show live badges.
/// </summary>
public sealed class PolicyViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _stateText = "Checking\u2026";
    private Brush _stateBrush = Neutral;
    private bool _isBusy;

    /// <summary>Creates a view model from the service's policy metadata.</summary>
    /// <param name="info">The policy info from the service.</param>
    public PolicyViewModel(PolicyInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        this.Id = info.Id;
        this.Title = info.Title;
        this.Description = info.Description;
        this.Category = info.Category;
        this.Level = info.Level;
        this.RiskText = info.RiskLevel.ToString();
        this.RiskBrush = RiskToBrush(info.RiskLevel);
        this.LevelText = info.Level.ToString();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The policy id.</summary>
    public string Id { get; }

    /// <summary>The short title.</summary>
    public string Title { get; }

    /// <summary>The longer description.</summary>
    public string Description { get; }

    /// <summary>The grouping category.</summary>
    public string Category { get; }

    /// <summary>The hardening preset this policy belongs to.</summary>
    public PolicyLevel Level { get; }

    /// <summary>The preset label, for the tile badge.</summary>
    public string LevelText { get; }

    /// <summary>The risk label.</summary>
    public string RiskText { get; }

    /// <summary>A color hint for the risk badge.</summary>
    public Brush RiskBrush { get; }

    /// <summary>Whether the user has selected this policy to apply.</summary>
    public bool IsSelected
    {
        get => this._isSelected;
        set => this.Set(ref this._isSelected, value);
    }

    /// <summary>A short label describing the latest detected state.</summary>
    public string StateText
    {
        get => this._stateText;
        set => this.Set(ref this._stateText, value);
    }

    /// <summary>A color hint for the state badge.</summary>
    public Brush StateBrush
    {
        get => this._stateBrush;
        set => this.Set(ref this._stateBrush, value);
    }

    /// <summary>Whether an operation is in flight for this policy.</summary>
    public bool IsBusy
    {
        get => this._isBusy;
        set => this.Set(ref this._isBusy, value);
    }

    /// <summary>Updates the state badge from a detected policy state.</summary>
    /// <param name="state">The detected state.</param>
    public void ApplyState(PolicyResultState state)
    {
        switch (state)
        {
            case PolicyResultState.Compliant:
                this.StateText = "Applied";
                this.StateBrush = Good;
                break;
            case PolicyResultState.NonCompliant:
                this.StateText = "Not applied";
                this.StateBrush = Neutral;
                break;
            case PolicyResultState.Applied:
                this.StateText = "Applied";
                this.StateBrush = Good;
                break;
            default:
                this.StateText = state.ToString();
                this.StateBrush = Warn;
                break;
        }
    }

    private static SolidColorBrush RiskToBrush(PolicyRiskLevel risk) => risk switch
    {
        PolicyRiskLevel.Low => Good,
        PolicyRiskLevel.Medium => Warn,
        PolicyRiskLevel.High => Bad,
        _ => Neutral,
    };

    private static SolidColorBrush Good => new(Color.FromArgb(255, 56, 142, 60));
    private static SolidColorBrush Warn => new(Color.FromArgb(255, 201, 138, 19));
    private static SolidColorBrush Bad => new(Color.FromArgb(255, 197, 57, 41));
    private static SolidColorBrush Neutral => new(Color.FromArgb(255, 120, 120, 120));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
