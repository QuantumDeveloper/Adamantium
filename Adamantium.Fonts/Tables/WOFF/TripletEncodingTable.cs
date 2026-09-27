using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class TripletEncodingTable
    {
        private static List<TripletEncodingEntry> entries;
        
        static TripletEncodingTable()
        {
            entries = new List<TripletEncodingEntry>();
            BuildTable();
            ValidateTable();
        }

        public static TripletEncodingEntry GetEntry(int index)
        {
            return entries[index];
        }
        
        private static void BuildTable()
        {
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 0	    2	        0	    8	    N/A	    0	    N/A	    -
            // 1					                        0		        +
            // 2					                        256		        -
            // 3					                        256		        +
            // 4					                        512		        -
            // 5					                        512		        +
            // 6					                        768		        -
            // 7					                        768		        +
            // 8					                        1024	        -
            // 9					                        1024	        +
            BuildEntries(2, 0, 8, null, new ushort[] {0, 256, 512, 768, 1024});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 10	    2	        8	    0	    0	    N/A	    -	    N/A
            // 11				                    0		        +	
            // 12				                    256		        -	
            // 13				                    256		        +	
            // 14				                    512		        -	
            // 15				                    512		        +	
            // 16				                    768		        -	
            // 17				                    768		        +	
            // 18				                    1024		    -	
            // 19				                    1024		    +
            BuildEntries(2, 8, 0, new ushort[] {0, 256, 512, 768, 1024}, null);
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 20	    2	        4	    4	    1	    1	    -	    -
            // 21					                        1	    +	    -
            // 22					                        1	    -	    +
            // 23					                        1	    +	    +
            // 24					                        17	    -	    -
            // 25					                        17	    +	    -
            // 26					                        17	    -	    +
            // 27					                        17	    +	    +
            // 28					                        33	    -	    -
            // 29					                        33	    +	    -
            // 30					                        33	    -	    +
            // 31					                        33	    +	    +
            // 32					                        49	    -	    -
            // 33					                        49	    +	    -
            // 34					                        49	    -	    +
            // 35					                        49	    +	    +
            BuildEntries(2, 4, 4, new ushort[] { 1 }, new ushort[] {1, 17, 33, 49});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 36	    2	        4	    4	    17	    1	    -	    -
            // 37					                        1	    +	    -
            // 38					                        1	    -	    +
            // 39					                        1	    +	    +
            // 40					                        17	    -	    -
            // 41					                        17	    +	    -
            // 42					                        17	    -	    +
            // 43					                        17	    +	    +
            // 44					                        33	    -	    -
            // 45					                        33	    +	    -
            // 46					                        33	    -	    +
            // 47					                        33	    +	    +
            // 48					                        49	    -	    -
            // 49					                        49	    +	    -
            // 50					                        49	    -	    +
            // 51					                        49	    +	    +
            BuildEntries(2, 4, 4, new ushort[] { 17 }, new ushort[] {1, 17, 33, 49});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 52	    2	        4	    4	    33	    1	    -	    -
            // 53	    	        	    	    	    1	    +	    -
            // 54	    	        	    	    	    1	    -	    +
            // 55	    	        	    	    	    1	    +	    +
            // 56	    	        	    	    	    17	    -	    -
            // 57	    	        	    	    	    17	    +	    -
            // 58	    	        	    	    	    17	    -	    +
            // 59	    	        	    	    	    17	    +	    +
            // 60	    	        	    	    	    33	    -	    -
            // 61	    	        	    	    	    33	    +	    -
            // 62	    	        	    	    	    33	    -	    +
            // 63	    	        	    	    	    33	    +	    +
            // 64	    	        	    	    	    49	    -	    -
            // 65	    	        	    	    	    49	    +	    -
            // 66	    	        	    	    	    49	    -	    +
            // 67	    	        	    	    	    49	    +	    +
            BuildEntries(2, 4, 4, new ushort[] { 33 }, new ushort[] {1, 17, 33, 49});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 68	    2	        4	    4	    49	    1	    -	    -
            // 69	    	        	    	    	    1	    +	    -
            // 70	    	        	    	    	    1	    -	    +
            // 71	    	        	    	    	    1	    +	    +
            // 72	    	        	    	    	    17	    -	    -
            // 73	    	        	    	    	    17	    +	    -
            // 74	    	        	    	    	    17	    -	    +
            // 75	    	        	    	    	    17	    +	    +
            // 76	    	        	    	    	    33	    -	    -
            // 77	    	        	    	    	    33	    +	    -
            // 78	    	        	    	    	    33	    -	    +
            // 79	    	        	    	    	    33	    +	    +
            // 80	    	        	    	    	    49	    -	    -
            // 81	    	        	    	    	    49	    +	    -
            // 82	    	        	    	    	    49	    -	    +
            // 83	    	        	    	    	    49	    +	    +
            BuildEntries(2, 4, 4, new ushort[] { 49 }, new ushort[] {1, 17, 33, 49});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            // 84	    3	        8	    8	    1	    1	    -	    -
            // 85	    	        	    	    	    1	    +	    -
            // 86	    	        	    	    	    1	    -	    +
            // 87	    	        	    	    	    1	    +	    +
            // 88	    	        	    	    	    257	    -	    -
            // 89	    	        	    	    	    257	    +	    -
            // 90	    	        	    	    	    257	    -	    +
            // 91	    	        	    	    	    257	    +	    +
            // 92	    	        	    	    	    513	    -	    -
            // 93	    	        	    	    	    513	    +	    -
            // 94	    	        	    	    	    513	    -	    +
            // 95	    	        	    	    	    513	    +	    +
            BuildEntries(3, 8, 8, new ushort[] { 1 }, new ushort[] {1, 257, 513});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            //  96	    3	        8	    8	    257	    1	    -	    -
            //  97	    	        	    	    	    1	    +	    -
            //  98	    	        	    	    	    1	    -	    +
            //  99	    	        	    	    	    1	    +	    +
            //  100	    	        	    	    	    257	    -	    -
            //  101	    	        	    	    	    257	    +	    -
            //  102	    	        	    	    	    257	    -	    +
            //  103	    	        	    	    	    257	    +	    +
            //  104	    	        	    	    	    513	    -	    -
            //  105	    	        	    	    	    513	    +	    -
            //  106	    	        	    	    	    513	    -	    +
            //  107	    	        	    	    	    513	    +	    +
            BuildEntries(3, 8, 8, new ushort[] { 257 }, new ushort[] {1, 257, 513});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            //  108	    3	        8	    8	    513	    1	    -	    -
            //  109	    	        	    	    	    1	    +	    -
            //  110	    	        	    	    	    1	    -	    +
            //  111	    	        	    	    	    1	    +	    +
            //  112	    	        	    	    	    257	    -	    -
            //  113	    	        	    	    	    257	    +	    -
            //  114	    	        	    	    	    257	    -	    +
            //  115	    	        	    	    	    257	    +	    +
            //  116	    	        	    	    	    513	    -	    -
            //  117	    	        	    	    	    513	    +	    -
            //  118	    	        	    	    	    513	    -	    +
            //  119	    	        	    	    	    513	    +	    +
            BuildEntries(3, 8, 8, new ushort[] { 513 }, new ushort[] {1, 257, 513});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            //  120	    4	        12	    12	    0	    0	    -	    -
            //  121	    	        	    	    	    	    +	    -
            //  122	    	        	    	    	    	    -	    +
            //  123	    	        	    	    	    	    +	    +
            BuildEntries(4, 12, 12, new ushort[] { 0 }, new ushort[] {0});
            
            // Index    Byte Count  X bits  Y bits  Delta X Delta Y X sign  Y sign
            //  124	    5	        16	    16	    0	    0	    -	    -
            //  125	    	        	    	    	    	    +	    -
            //  126	    	        	    	    	    	    -	    +
            //  127	    	        	    	    	    	    +	    +
            BuildEntries(5, 16, 16, new ushort[] { 0 }, new ushort[] {0});
        }

        private static void ValidateTable()
        {
            if (entries.Count != 128)
            {
                throw new ArgumentOutOfRangeException(
                    $"entries collection count is not 1280. Current entries count = {entries.Count}");
            }

            for (int i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                if (i < 84)
                {
                    if (entry.ByteCount - 1 != 1)
                    {
                        throw new ArgumentException($"Byte count is not 1");
                    }
                }
                else if (i < 120)
                {
                    if (entry.ByteCount - 1 != 2)
                    {
                        throw new ArgumentException($"Byte count is not 2");
                    }
                }
                else if (i < 124)
                {
                    if (entry.ByteCount - 1 != 3)
                    {
                        throw new ArgumentException($"Byte count is not 3");
                    }
                }
                else if (i < 128)
                {
                    if (entry.ByteCount - 1 != 4)
                    {
                        throw new ArgumentException($"Byte count is not 4");
                    }
                }
            }
        }

        private static void BuildEntries(byte byteCount, byte xBits, byte yBits, ushort[] deltaXs, ushort[] deltaYs)
        {
            if (deltaXs == null)
            {
                for (int i = 0; i < deltaYs.Length; ++i)
                {
                    AddEntry(byteCount, xBits, yBits, 0, deltaYs[i], 0, -1);
                    AddEntry(byteCount, xBits, yBits, 0, deltaYs[i], 0, 1);
                }
                
            }
            else if (deltaYs == null)
            {
                for (int i = 0; i < deltaXs.Length; ++i)
                {
                    AddEntry(byteCount, xBits, yBits, deltaXs[i], 0, -1, 0);
                    AddEntry(byteCount, xBits, yBits, deltaXs[i], 0, 1, 0);
                }
            }
            else
            {
                for (int i = 0; i < deltaXs.Length; ++i)
                {
                    var deltaX = deltaXs[i];
                    for (int y = 0; y < deltaYs.Length; ++y)
                    {
                        var deltaY = deltaYs[y];
                        AddEntry(byteCount, xBits, yBits, deltaX, deltaY, -1, -1);
                        AddEntry(byteCount, xBits, yBits, deltaX, deltaY, 1, -1);
                        AddEntry(byteCount, xBits, yBits, deltaX, deltaY, -1, 1);
                        AddEntry(byteCount, xBits, yBits, deltaX, deltaY, 1, 1);
                    }
                }
            }
        }

        private static void AddEntry(byte byteCount, byte xBits, byte yBits, ushort deltaX, ushort deltaY, sbyte xSign, sbyte ySign)
        {
            var entry = new TripletEncodingEntry(byteCount, xBits, yBits, deltaX, deltaY, xSign, ySign);
            entries.Add(entry);
        }
    }
}