namespace Adamantium.UI.Core.Behaviors;

public interface IAttachedObject
{
    void AttachTo(IAdamantiumComponent adamantiumComponent);
    
    void DetachFrom(IAdamantiumComponent adamantiumComponent);
}

// Base = FundamentalUIComponent (the LOGICAL layer: DataContext + bindings + styles, no visual/render - those live in
// UIComponent). So a behavior can carry {Binding}s, and on attach it JOINS the associated element's logical tree
// (AddLogicalChild), inheriting DataContext/resources the same way a real child does - the Avalonia model (behavior is a
// StyledElement joined via SetParent), not WPF's InheritanceContext hack.
public abstract class Behavior : FundamentalUIComponent, IAttachedObject
{
    public void AttachTo(IAdamantiumComponent adamantiumComponent)
    {
        if (adamantiumComponent is IFundamentalUIComponent element)
            element.AddLogicalChild(this);   // logical-tree join -> DataContext/bindings/resources flow to this behavior
        OnAttached(adamantiumComponent);
    }

    public void DetachFrom(IAdamantiumComponent adamantiumComponent)
    {
        OnDetached(adamantiumComponent);
        if (adamantiumComponent is IFundamentalUIComponent element)
            element.RemoveLogicalChild(this);
    }

    protected virtual void OnAttached(IAdamantiumComponent adamantiumComponent)
    {
        
    }

    protected virtual void OnDetached(IAdamantiumComponent adamantiumComponent)
    {
        
    }
}