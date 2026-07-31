namespace Adamantium.UI.Controls.Navigation;

/// <summary>A view model that can put ITSELF back together when a saved layout is restored. The layout remembers only
/// the pane's id and its view model's type; everything else the instance knew - which page it was showing, which file
/// it had open - it takes from that id.
/// <para>Without it a restored pane comes back as a blank instance of the right type, which is worse than not coming
/// back at all: the arrangement looks right and the content is wrong.</para></summary>
public interface IRestorablePane
{
    /// <summary>Called on a freshly made view model, before it is put into the region, with the id its pane had.</summary>
    void RestoreFrom(string paneId);
}
