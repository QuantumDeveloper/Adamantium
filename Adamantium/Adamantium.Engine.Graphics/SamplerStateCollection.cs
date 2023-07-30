using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class SamplerStateCollection : StateCollectionBase<SamplerState>
    {
        /// <summary>
        /// Default state is using linear filtering with texture coordinate clamping.
        /// </summary>
        public readonly SamplerState Default;

        /// <summary>
        /// Linear filtering with texture coordinate wrapping.
        /// </summary>
        public readonly SamplerState LinearRepeat;

        /// <summary>
        /// Linear filtering with texture coordinate clamping to border.
        /// </summary>
        public readonly SamplerState LinearClampToBorder;
        
        /// <summary>
        /// Linear filtering with texture coordinate clamping to edge.
        /// </summary>
        public readonly SamplerState LinearClampToEdge;

        /// <summary>
        /// Linear filtering with texture coordinate mirroring.
        /// </summary>
        public readonly SamplerState LinearMirror;

        /// <summary>
        /// Anisotropic filtering with texture coordinate wrapping.
        /// </summary>
        public readonly SamplerState AnisotropicRepeat;

        /// <summary>
        /// Anisotropic filtering with texture coordinate clamping.
        /// </summary>
        public readonly SamplerState AnisotropicClampToBorder;
        
        /// <summary>
        /// Anisotropic filtering with texture coordinate clamping.
        /// </summary>
        public readonly SamplerState AnisotropicClampToEdge;

        /// <summary>
        /// Anisotropic filtering with texture coordinate mirroring.
        /// </summary>
        public readonly SamplerState AnisotropicMirror;

        private GraphicsDevice _device;
        
        internal SamplerStateCollection(GraphicsDevice device)
        {
            _device = device;
            LinearRepeat = Add(GetSamplerSate(device, nameof(LinearRepeat), Filter.Linear, SamplerAddressMode.Repeat, false));
            LinearClampToBorder = Add(GetSamplerSate(device, nameof(LinearClampToBorder), Filter.Linear,
                SamplerAddressMode.ClampToBorder, false));
            LinearClampToEdge = Add(GetSamplerSate(device, nameof(LinearClampToEdge), Filter.Linear,
                SamplerAddressMode.ClampToEdge, false));
            LinearMirror = Add(GetSamplerSate(device, nameof(LinearMirror), Filter.Linear,
                SamplerAddressMode.MirrorClampToEdge, false));
            
            AnisotropicRepeat = Add(GetSamplerSate(device, nameof(AnisotropicRepeat), Filter.Linear, SamplerAddressMode.Repeat));
            AnisotropicClampToBorder = Add(GetSamplerSate(device, nameof(AnisotropicClampToBorder), Filter.Linear,
                SamplerAddressMode.ClampToBorder));
            AnisotropicClampToEdge = Add(GetSamplerSate(device, nameof(AnisotropicClampToEdge), Filter.Linear,
                SamplerAddressMode.ClampToEdge));
            AnisotropicMirror = Add(GetSamplerSate(device, nameof(AnisotropicMirror), Filter.Linear,
                SamplerAddressMode.MirrorClampToEdge));

            Default = AnisotropicClampToBorder;
        }

        private static SamplerState GetSamplerSate(
            GraphicsDevice device,
            string name,
            Filter filter, 
            SamplerAddressMode samplerMode, 
            bool anisotropyEnabled = true, 
            float maxAnisotropy = 16,
            bool unnormalizedCoordinates = false,
            bool compareEnabled = false,
            CompareOp compareOp = CompareOp.Always)
        {
            SamplerCreateInfo samplerInfo = new SamplerCreateInfo();
            samplerInfo.MagFilter = filter;
            samplerInfo.MinFilter = filter;
            samplerInfo.AddressModeU = samplerMode;
            samplerInfo.AddressModeV = samplerMode;
            samplerInfo.AddressModeW = samplerMode;
            samplerInfo.AnisotropyEnable = anisotropyEnabled;
            samplerInfo.MaxAnisotropy = maxAnisotropy;
            samplerInfo.BorderColor = BorderColor.IntOpaqueWhite;
            samplerInfo.UnnormalizedCoordinates = unnormalizedCoordinates;
            samplerInfo.CompareEnable = compareEnabled;
            samplerInfo.CompareOp = compareOp;
            samplerInfo.MipmapMode = SamplerMipmapMode.Linear;

            return SamplerState.New(device, name, samplerInfo);
        }

        public override void Dispose()
        {
            foreach (var state in this)
            {
                _device.LogicalDevice.DestroySampler(state);
            }
            Clear();
        }
    }
}