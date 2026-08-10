using Adamantium.Core.Collections;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Resources;

public class TriggerCollection : AdamantiumCollection<ITrigger>
{
    // Stamp every setter with where it stands in the MARKUP, so two triggers writing one property resolve by what the
    // author wrote rather than by which happened to fire last (see ISetter.DeclarationOrder). Done on insert, because
    // that is the one moment a trigger's position in the collection is known - and re-stamped for the whole collection,
    // since inserting in the middle moves everything after it.
    protected override void InsertItem(int index, ITrigger item)
    {
        base.InsertItem(index, item);
        Restamp();
    }

    protected override ITrigger RemoveItem(int index)
    {
        var removed = base.RemoveItem(index);
        Restamp();
        return removed;
    }

    private void Restamp()
    {
        for (var i = 0; i < Count; i++)
        {
            var setters = this[i]?.Setters;
            if (setters == null) continue;

            for (var s = 0; s < setters.Count; s++)
            {
                // Room for the setters of one trigger, so a later trigger always outranks an earlier one whatever it
                // holds.
                setters[s].DeclarationOrder = (i + 1) * 1000 + s;
            }
        }
    }
}
