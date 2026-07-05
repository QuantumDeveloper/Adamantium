using System.ComponentModel.DataAnnotations;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Demo enum for the DropDown: the [Display] names are what the dropdown shows, while the bound value stays the
/// enum member - the friendly-name binding WPF never did for free.</summary>
public enum Priority
{
    [Display(Name = "Low priority")] Low,
    [Display(Name = "Normal priority")] Normal,
    [Display(Name = "High priority")] High,
    [Display(Name = "Critical - drop everything")] Critical,
}
