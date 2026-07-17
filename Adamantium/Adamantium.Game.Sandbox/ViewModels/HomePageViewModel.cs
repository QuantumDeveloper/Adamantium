namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Home page shown in the Navigation region. A plain content view model - the region resolves it to HomePageView
/// by naming convention, so a page needs no base class or attribute to participate.</summary>
public class HomePageViewModel
{
    public string Message => "Home - the region's starting page. Navigate by view-model type with the buttons above.";
}
