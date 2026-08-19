namespace Adamantium.UI.Rendering;

/// <summary>
/// One LAYER of the recorded frame: the set of draws whose mutual order does not matter, while everything else is
/// strictly earlier or strictly later. It is not a level of the tree - depth does not decide paint order (a deep child of
/// an early sibling paints BEFORE a shallow late one) - and two siblings that overlap cannot share one, because then
/// their order is exactly what decides the picture.
/// <para>The engine already computes these: a batch is flushed the moment the next draw OVERLAPS what it has pending, so
/// one flush cycle IS such a set. What was missing is that it had no name - the cycle was implicit in a flat op stream,
/// its place in the order was remembered as a single rank rather than the INTERVAL it really covers, and a newcomer
/// landing inside that interval could only be dealt with by cutting the stream open (see §5a in
/// docs/RENDER_CACHE_REDESIGN.md).</para>
/// <para>The layer owns a slice of the recorded stream, addressed by rank INTERVAL. A newcomer whose rank falls inside
/// the interval and which overlaps nothing the layer draws simply joins it; one that does overlap opens a layer of its
/// own directly after - which is the ordinary operation, not a repair.</para>
/// </summary>
internal sealed class RenderLayer
{
    /// <summary>Paint rank of the first draw in this layer, and of the last. Together they are the layer's PLACE in the
    /// frame - an interval, not a point, because the layer glues everything that fell between two flushes.</summary>
    public long RankFirst = long.MaxValue;

    public long RankLast = long.MinValue;

    /// <summary>Where this layer's ops begin in the recorded stream, and how many there are. Kept as a range rather than
    /// a list of its own so the stream stays one contiguous array to replay - the layer is what the range MEANS.</summary>
    public int OpFirst;

    public int OpCount;

    /// <summary>The batch runs this layer draws: which collector, and which segment of it. One layer holds at most one
    /// run per material (a flush cycle empties each collector once), and their mutual order is the fixed material order,
    /// not the paint ranks - which is precisely why a layer can be treated as one place in the frame.</summary>
    public readonly System.Collections.Generic.List<(byte Batch, int SegId)> Runs = new();

    public void Cover(long rank)
    {
        if (rank < RankFirst) RankFirst = rank;
        if (rank > RankLast) RankLast = rank;
    }

    /// <summary>Whether this rank belongs inside the layer's span. A newcomer here has to be asked the overlap question;
    /// one outside simply goes before or after the whole layer.</summary>
    public bool Covers(long rank) => rank >= RankFirst && rank <= RankLast;

    public override string ToString() => $"layer [{RankFirst}..{RankLast}] ops {OpFirst}+{OpCount}";
}
