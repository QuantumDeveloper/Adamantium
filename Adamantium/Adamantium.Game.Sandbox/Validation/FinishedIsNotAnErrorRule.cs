using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Sandbox.Validation;

/// <summary>What no single cell can answer: a record marked done and filed as an error contradicts itself. Neither
/// field is wrong on its own - "done" is a perfectly good value and so is "error" - so the fault belongs to the RECORD,
/// and that is where the table marks it.</summary>
public class FinishedIsNotAnErrorRule : DataGridRowValidationRule
{
    public override string Validate(object item) =>
        item is GridNode { Done: true, Status: "error" } ? "marked done and filed as an error" : null;
}
