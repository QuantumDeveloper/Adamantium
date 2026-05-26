using System;
using System.Runtime.InteropServices;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics.Core
{
    /// <summary>
    /// Values that indicate how the pipeline interprets vertex data that is bound to the input-assembler stage. These primitive topology values determine how the vertex data is rendered on screen.
    /// PrimitiveType is equivalent to <see cref="AdamantiumVulkan.Core.PrimitiveTopology"/>.
    /// </summary>
    /// <remarks>
    /// This structure is implicitly castable to and from <see cref="AdamantiumVulkan.Core.PrimitiveTopology"/>, you can use it in place where <see cref="AdamantiumVulkan.Core.PrimitiveTopology"/> is required
    /// and vice-versa.
    /// </remarks>
    /// <msdn-id>ff728726</msdn-id>	
    /// <unmanaged>D3D_PRIMITIVE_TOPOLOGY</unmanaged>	
    /// <unmanaged-short>D3D_PRIMITIVE_TOPOLOGY</unmanaged-short>
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public readonly struct PrimitiveType : IEquatable<PrimitiveType>
    {
        private readonly PrimitiveTopology _topology;

        private PrimitiveType(PrimitiveTopology type)
        {
            _topology = type;
        }

        public static readonly PrimitiveType Undefined = new PrimitiveType((PrimitiveTopology)(-1)); 
        
        public static readonly PrimitiveType PointList = new PrimitiveType(PrimitiveTopology.PointList);
        public static readonly PrimitiveType LineList = new PrimitiveType(PrimitiveTopology.LineList);
        public static readonly PrimitiveType LineStrip = new PrimitiveType(PrimitiveTopology.LineStrip);
        public static readonly PrimitiveType TriangleList = new PrimitiveType(PrimitiveTopology.TriangleList);
        public static readonly PrimitiveType TriangleFan = new PrimitiveType(PrimitiveTopology.TriangleFan);
        public static readonly PrimitiveType TriangleStrip = new PrimitiveType(PrimitiveTopology.TriangleStrip);
        public static readonly PrimitiveType LineListWithAdjacency = new PrimitiveType(PrimitiveTopology.LineListWithAdjacency);
        public static readonly PrimitiveType LineStripWithAdjacency = new PrimitiveType(PrimitiveTopology.LineStripWithAdjacency);
        public static readonly PrimitiveType TriangleListWithAdjacency = new PrimitiveType(PrimitiveTopology.TriangleListWithAdjacency);
        public static readonly PrimitiveType TriangleStripWithAdjacency = new PrimitiveType(PrimitiveTopology.TriangleStripWithAdjacency);
        public static readonly PrimitiveType PatchList = new PrimitiveType(PrimitiveTopology.PatchList);

        public static implicit operator PrimitiveTopology(PrimitiveType from) => from._topology;
        public static implicit operator PrimitiveType(PrimitiveTopology from) => new PrimitiveType(from);

        public bool Equals(PrimitiveType other) => _topology == other._topology;

        public override bool Equals(object obj) => obj is PrimitiveType other && Equals(other);

        public override int GetHashCode() => (int)_topology;

        public static bool operator ==(PrimitiveType left, PrimitiveType right) => left.Equals(right);
        public static bool operator !=(PrimitiveType left, PrimitiveType right) => !left.Equals(right);

        public override string ToString() => _topology.ToString();
    }
}
