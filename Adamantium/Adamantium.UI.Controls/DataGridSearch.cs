using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls;

/// <summary>What a search over the table found: every cell whose text holds the sought string, in row order, and which
/// of them is the one being looked at.
/// <para>Held by ITEM rather than by row number, because the row numbers move the moment a group is opened or shut and
/// a search is not re-run for that.</para></summary>
internal sealed class DataGridSearch
{
    private readonly List<(object Item, int Column)> _found = new();
    private readonly HashSet<(object Item, int Column)> _byCell = new();

    /// <summary>What was searched for. Empty means nothing was.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>How many cells hold it.</summary>
    public int Count => _found.Count;

    /// <summary>Which of them is current, from 1; 0 when there are none.</summary>
    public int Current { get; private set; }

    public void Clear()
    {
        Text = string.Empty;
        Current = 0;
        _found.Clear();
        _byCell.Clear();
    }

    public void Begin(string text)
    {
        Clear();
        Text = text ?? string.Empty;
    }

    public void Add(object item, int column)
    {
        if (_byCell.Add((item, column))) _found.Add((item, column));
    }

    /// <summary>Whether this cell is one of the matches - what a realized cell asks to paint itself.</summary>
    public bool Holds(object item, int column) => item != null && _byCell.Contains((item, column));

    /// <summary>Whether this cell is the one being looked at.</summary>
    public bool IsCurrent(object item, int column) =>
        Current > 0 && item != null
        && _found[Current - 1].Column == column && ReferenceEquals(_found[Current - 1].Item, item);

    /// <summary>Steps to the next match, or the previous one, wrapping round. False when there are none.</summary>
    public bool Step(int by)
    {
        if (_found.Count == 0)
        {
            Current = 0;
            return false;
        }

        // Wrapping is what makes the button usable at the ends: a Find Next that stops at the last match leaves the
        // reader to scroll back to the top by hand.
        var next = Current + by;
        if (next < 1) next = _found.Count;
        if (next > _found.Count) next = 1;

        Current = next;
        return true;
    }

    /// <summary>The cell the search is looking at, or null when it is looking at none.</summary>
    public (object Item, int Column)? At => Current > 0 ? _found[Current - 1] : null;

    /// <summary>Whether <paramref name="text"/> holds what is being searched for. One place, so the pass that finds the
    /// matches and anything that re-checks a cell can never disagree.
    /// <para>ORDINAL, ignoring case, which is what a search box in an editor does. Measured: the culture-aware
    /// comparison cost 60 ms of a 106 ms pass over ten thousand rows and four columns - more than the reading itself.
    /// What is given up with it is culture-specific equivalence (ß against ss, the Turkish dotless i); case folding for
    /// Latin and Cyrillic is the same either way.</para></summary>
    public static bool Matches(string text, string sought) =>
        !string.IsNullOrEmpty(sought) && text != null
        && text.IndexOf(sought, StringComparison.OrdinalIgnoreCase) >= 0;
}
