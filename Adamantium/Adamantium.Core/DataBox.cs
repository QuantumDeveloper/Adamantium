using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Core
{
    public struct DataBox
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataBox"/> struct.
        /// </summary>
        /// <param name="datapointer">The datapointer.</param>
        /// <param name="rowPitch">The row pitch.</param>
        /// <param name="slicePitch">The slice pitch.</param>
        public DataBox(IntPtr datapointer, int rowPitch, int slicePitch)
        {
            DataPointer = datapointer;
            RowPitch = rowPitch;
            SlicePitch = slicePitch;
        }

        public DataBox(IntPtr dataPointer)
        {
            DataPointer = dataPointer;
            RowPitch = 0;
            SlicePitch = 0;
        }

        public IntPtr DataPointer;
        public int RowPitch;
        public int SlicePitch;

        public bool IsEmpty
        {
            get
            {
                return DataPointer == IntPtr.Zero && RowPitch == 0 && SlicePitch == 0;
            }
        }
    }
}
