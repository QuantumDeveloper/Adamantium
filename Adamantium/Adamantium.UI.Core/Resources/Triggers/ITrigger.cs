namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Add(ISetter setter);
   ITriggerActivator Apply(ITriggerExecutionContext context);
}