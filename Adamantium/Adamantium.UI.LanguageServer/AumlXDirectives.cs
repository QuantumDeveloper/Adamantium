using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// The <c>x:</c> directives surfaced by completion and hover. The set is NOT defined here - it is derived from the
/// single source of truth <see cref="AumlDirectives.All"/> in Adamantium.UI.Markup, so adding a directive there shows
/// up in tooling automatically without editing the language server.
/// </summary>
internal static class AumlXDirectives
{
    public const string Xmlns = "http://adamantium/ui/xaml/extensions";

    public static readonly IReadOnlyList<(string Name, string Detail)> All =
        AumlDirectives.All.Select(d => (d.Name, Detail: d.Description)).ToList();
}
