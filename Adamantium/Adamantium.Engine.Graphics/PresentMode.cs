using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public enum PresentMode
    {
        Immediate = 0,

        Mailbox = 1,

        Fifo = 2,

        FifoRelaxed = 3,

        SharedDemandRefresh = 1000111000,

        SharedContinuousRefresh = 1000111001,
    }
}
