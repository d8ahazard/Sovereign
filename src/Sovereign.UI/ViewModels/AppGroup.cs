using System.Collections.Generic;

namespace Sovereign.UI.ViewModels;

/// <summary>
/// A category group of apps used to render section headers on the Apps &amp; debloat page.
/// </summary>
public sealed class AppGroup : List<AppRowViewModel>
{
    /// <summary>Creates a group.</summary>
    /// <param name="key">The category header.</param>
    /// <param name="items">The apps in this category.</param>
    public AppGroup(string key, IEnumerable<AppRowViewModel> items)
        : base(items)
    {
        this.Key = key;
    }

    /// <summary>The category header text.</summary>
    public string Key { get; }

    /// <summary>The number of apps in this category, for the header count.</summary>
    public int CountLabel => this.Count;
}
