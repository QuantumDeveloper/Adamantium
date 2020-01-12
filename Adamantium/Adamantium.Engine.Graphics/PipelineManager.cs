using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public class PipelineManager
    {
        private Dictionary<Type, GraphicsPipeline> graphicsPipelines;
        private Dictionary<int, ComputePipeline> computePipelines;
    }
}
