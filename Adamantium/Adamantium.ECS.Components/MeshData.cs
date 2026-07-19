using System;
using Adamantium.ECS.ComponentsBasics;
using Adamantium.Graphics.Core.Models;
using Adamantium.Vulkan.Core;

namespace Adamantium.ECS.Components
{
    public sealed class MeshData : ActivatableComponent
    {
        private Mesh mesh;
        private MeshMetadata metadata;
        private MeshRenderMode renderMode;
        private bool isWireFrame;
        private CullModeFlagBits cullMode = CullModeFlagBits.None;
        private bool depthTestEnabled = true;
        private bool depthWriteEnabled = true;
        private PrimitiveTopology? topologyOverride;

        public MeshData()
        {
            Metadata = MeshMetadata.Default();
        }

        public MeshData(MeshMetadata metadata)
        {
            Metadata = new MeshMetadata(metadata);
        }

        [DoNotClone]
        public Mesh Mesh
        {
            get => mesh;
            set
            {
                if (SetProperty(ref mesh, value))
                {
                    MeshDataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public MeshMetadata Metadata
        {
            get => metadata;
            set
            {
                if (SetProperty(ref metadata, value))
                {
                    MetadataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // --- Render state (the EXPLICIT how-to-draw config; was the now-removed renderer component chain) -------------

        // Which render path to use. Explicit and overridable - NOT derived from the mesh's semantic, so the same mesh
        // can be drawn Static or Skinned.
        public MeshRenderMode RenderMode
        {
            get => renderMode;
            set => SetProperty(ref renderMode, value);
        }

        public bool IsWireFrame
        {
            get => isWireFrame;
            set => SetProperty(ref isWireFrame, value);
        }

        public CullModeFlagBits CullMode
        {
            get => cullMode;
            set => SetProperty(ref cullMode, value);
        }

        public bool DepthTestEnabled
        {
            get => depthTestEnabled;
            set => SetProperty(ref depthTestEnabled, value);
        }

        public bool DepthWriteEnabled
        {
            get => depthWriteEnabled;
            set => SetProperty(ref depthWriteEnabled, value);
        }

        // Overrides the mesh's own topology when set (null = use the mesh topology).
        public PrimitiveTopology? TopologyOverride
        {
            get => topologyOverride;
            set => SetProperty(ref topologyOverride, value);
        }

        public event EventHandler MeshDataChanged;

        public event EventHandler MetadataChanged;

        public override IComponent Clone()
        {
            var meshComponent = new MeshData(Metadata)
            {
                Mesh = Mesh.Clone(),
                RenderMode = RenderMode,
                IsWireFrame = IsWireFrame,
                CullMode = CullMode,
                DepthTestEnabled = DepthTestEnabled,
                DepthWriteEnabled = DepthWriteEnabled,
                TopologyOverride = TopologyOverride
            };
            return meshComponent;
        }
    }
}
