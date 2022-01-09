using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Models.ConversionUtils
{
    public class RawIndicesSemanticData
    {
        public RawIndicesSemanticData()
        {
            SemanticIdMapping = new Dictionary<string, VertexSemantic>();
            RawIndices = new List<int>();
            Offset = new Offset();
            VertexType = new List<int>();
            MeshTopology = PrimitiveType.Undefined;
        }

        public VertexSemantic Semantic { get; set; }

        public String MaterialId { get; set; }

        public List<Int32> RawIndices { get; set; }

        public Dictionary<String, VertexSemantic> SemanticIdMapping { get; set; }

        public Offset Offset { get; set; }

        public List<int> VertexType { get; set; }

        public PrimitiveType MeshTopology { get; set; }
    }
}
