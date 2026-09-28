namespace Adamantium.Imaging.Png
{
    /// <summary>
    /// /*BPM: Boundary Package Merge, see "A Fast and Space-Economical Algorithm for Length-Limited Coding",
    /// Jyrki Katajainen, Alistair Moffat, Andrew Turpin, 1995.*/
    /// </summary>
    internal class BPMNode
    {
        /*the sum of all weights in this chain*/
        public int Weight { get; set; }

        /*index of this leaf node (called "count" in the paper)*/
        public int Index { get; set; }

        /*the next nodes in this chain (null if last)*/
        public BPMNode Tail { get; set; }

        public bool InUse { get; set; }

        /// <summary>
        /// creates a new chain node with the given parameters, from the memory in the lists
        /// </summary>
        /// <param name="lists"></param>
        /// <param name="weight"></param>
        /// <param name="index"></param>
        /// <param name="tail"></param>
        /// <returns></returns>
        public static BPMNode Create(BpmLists lists, int weight, int index, BPMNode tail)
        {
            /*memory full, so garbage collect*/
            if (lists.NextFree >= lists.Numfree)
            {
                /*mark only those that are in use*/
                for (int i = 0; i != lists.MemSize; ++i)
                {
                    if (lists.Memory[i] == null)
                    {
                        lists.Memory[i] = new BPMNode();
                    }
                    lists.Memory[i].InUse = false;
                }

                for (int i = 0; i != lists.ListSize; ++i)
                {
                    BPMNode node;
                    for (node = lists.Chains0[i]; node != null; node = node.Tail) node.InUse = true;
                    for (node = lists.Chains1[i]; node != null; node = node.Tail) node.InUse = true;
                }
                /*collect those that are free*/
                lists.Numfree = 0;
                for (int i = 0; i != lists.MemSize; ++i)
                {
                    if (!lists.Memory[i].InUse)
                    {
                        lists.FreeList[lists.Numfree++] = lists.Memory[i];
                    }
                }
                lists.NextFree = 0;
            }

            BPMNode result;
            result = lists.FreeList[lists.NextFree++];
            result.Weight = weight;
            result.Index = index;
            result.Tail = tail;

            return result;
        }

        public static void Sort(ref BPMNode[] leaves, int num)
        {
            BPMNode[] mem = new BPMNode[num];
            int width, counter = 0;

            for (width = 1; width < num; width *= 2)
            {
                BPMNode[] a = (counter & 1) == 1 ? mem : leaves;
                BPMNode[] b = (counter & 1) == 1 ? leaves : mem;
                int p;
                for (p = 0; p < num; p+= 2 * width)
                {
                    int q = (p + width > num) ? num : (p + width);
                    int r = (p + 2 * width > num) ? num : (p + 2 * width);
                    int i = p, j = q, k;
                    for (k = p; k< r; k++)
                    {
                        if (i < q && (j >= r || a[i].Weight <= a[j].Weight))
                        {
                            b[k] = a[i++];
                        }
                        else
                        {
                            b[k] = a[j++];
                        }
                    }
                }

                counter++;
            }

            if ((counter & 1) == 1)
            {
                leaves = mem;
            }
        }

        /*Boundary Package Merge step, numpresent is the amount of leaves, and chain is the current chain.*/
        public static void BoundaryPackageMerge(BpmLists lists, BPMNode[] leaves, int numpresent, int chain, int num)
        {
            int lastIndex = lists.Chains1[chain].Index;

            if (chain == 0)
            {
                if (lastIndex >= numpresent) return;

                lists.Chains0[chain] = lists.Chains1[chain];
                lists.Chains1[chain] = BPMNode.Create(lists, leaves[lastIndex].Weight, lastIndex + 1, null);
            }
            else
            {
                /*sum of the weights of the head nodes of the previous lookahead chains.*/
                int sum = lists.Chains0[chain - 1].Weight + lists.Chains1[chain - 1].Weight;
                lists.Chains0[chain] = lists.Chains1[chain];
                if (lastIndex < numpresent && sum > leaves[lastIndex].Weight)
                {
                    lists.Chains1[chain] = BPMNode.Create(lists, leaves[lastIndex].Weight, lastIndex + 1, lists.Chains1[chain].Tail);
                    return;
                }
                lists.Chains1[chain] = BPMNode.Create(lists, sum, lastIndex, lists.Chains1[chain - 1]);
                /*in the end we are only interested in the chain of the last list, so no
                need to recurse if we're at the last one (this gives measurable speedup)*/
                if (num + 1 < (2 * numpresent - 2))
                {
                    BoundaryPackageMerge(lists, leaves, numpresent, chain - 1, num);
                    BoundaryPackageMerge(lists, leaves, numpresent, chain - 1, num);
                }

            }
        }
    }
}
