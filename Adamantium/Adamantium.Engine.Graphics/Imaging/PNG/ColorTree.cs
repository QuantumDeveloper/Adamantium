using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class ColorTree
    {
        public ColorTree[] Children { get; set; }

        public int Index { get; set; }

        public void Initialize()
        {
            Index = -1;
            Children = new ColorTree[16];
        }

        public static void Add(ref ColorTree tree, byte r, byte g, byte b, byte a, int index)
        {
            int bit;
            for (bit = 0; bit < 8; ++bit)
            {
                int i = 8 * ((r >> bit) & 1) + 4 * ((g >> bit) & 1) + 2 * ((b >> bit) & 1) + 1 * ((a >> bit) & 1);
                if (tree.Children[i] == null)
                {
                    var colorTree = new ColorTree();
                    tree.Initialize();
                    tree.Children[i] = colorTree;
                }
                tree = tree.Children[i];
            }
            tree.Index = (int)index;
        }

        public static int Get(ref ColorTree tree, byte r, byte g, byte b, byte a)
        {
            int bit = 0;
            for (bit = 0; bit < 8; ++bit)
            {
                int i = 8 * ((r >> bit) & 1) + 4 * ((g >> bit) & 1) + 2 * ((b >> bit) & 1) + 1 * ((a >> bit) & 1);
                if (tree.Children[i] == null) return -1;
                else tree = tree.Children[i];
            }
            return tree != null ? tree.Index : -1;
        }
    }
}
