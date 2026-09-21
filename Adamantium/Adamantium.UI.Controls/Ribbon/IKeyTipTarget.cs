namespace Adamantium.UI.Controls;

/// <summary>What a key tip does when its keys are typed, for anything that is not simply a button. A ribbon tab header
/// selects its tab; a command runs. Without this the session would have to know the control types it drives, which is
/// the one thing a service must not do.</summary>
public interface IKeyTipTarget
{
    void PressKeyTip();
}

/// <summary>A key-tip LEVEL whose next set of badges does not live under it. A ribbon tab header is the case: pressing
/// it selects the tab, but the tab's commands are shown by the band, in a different subtree entirely.</summary>
public interface IKeyTipScope
{
    Core.IUIComponent KeyTipContent { get; }
}
