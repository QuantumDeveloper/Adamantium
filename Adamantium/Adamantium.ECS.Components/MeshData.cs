using System;
using Adamantium.ECS.ComponentsBasics;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.ECS.Components
{
    public sealed class MeshData : ActivatableComponent
    {
        private Mesh mesh;
        private MeshMetadata metadata;

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

        public event EventHandler MeshDataChanged;

        public event EventHandler MetadataChanged;

        public override IComponent Clone()
        {
            var meshComponent = new MeshData(Metadata);
            meshComponent.Mesh = Mesh.Clone();
            return meshComponent;
        }
    }
}
