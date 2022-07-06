using System;
using Adamantium.Core;
using Adamantium.Engine.Effects;

namespace Adamantium.Engine.Graphics.Effects
{
    /// <summary>
    /// A constant buffer exposed by an effect.
    /// </summary>
    /// <remarks>
    /// Constant buffers are created and shared inside a same <see cref="EffectPool"/>. The creation of the underlying GPU buffer
    /// can be overridden using <see cref="EffectPool.ConstantBufferAllocator"/>.
    /// </remarks>
    public sealed class EffectConstantBuffer : DisposableObject, IEquatable<EffectConstantBuffer>
    {
        /// <summary>
        /// Reference to <see cref="Adamantium.Engine.Graphics.Buffer"/>
        /// </summary>
        public readonly Buffer NativeBuffer;

        /// <summary>
        /// <see cref="DataBuffer"/> for buffering variables
        /// </summary>
        public readonly DataBuffer BackingBuffer;

        public EffectData.ConstantBuffer Description;

        private GraphicsDevice device;
        private readonly int hashCode;
        private DataPointer pointer;

        public EffectConstantBuffer(GraphicsDevice device, EffectData.ConstantBuffer description)
        {
            BackingBuffer = new DataBuffer(description.Size);
            this.device = device;
            Description = description;
            Name = description.Name;
            Parameters = new EffectParameterCollection(description.Parameters.Count);
            hashCode = description.GetHashCode();

            // Add all parameters to this constant buffer.
            for (int i = 0; i < description.Parameters.Count; i++)
            {
                var parameterRaw = description.Parameters[i];
                var parameter = new EffectParameter(parameterRaw, this) { Index = i };
                Parameters.Add(parameter);
            }

            // By default, all constant buffers are cleared with 0
            BackingBuffer.Clear();

            NativeBuffer = ToDispose(Buffer.Uniform.New(device, BackingBuffer.Size));

            // The buffer is considered dirty for the first usage.
            IsDirty = true;
        }

        /// <summary>
        /// Set this flag to true to notify that the buffer was changed
        /// </summary>
        /// <remarks>
        /// When using Set(value) methods on this buffer, this property must be set to true to ensure that the buffer will
        /// be uploaded.
        /// </remarks>
        public bool IsDirty;

        /// <summary>
        /// Gets the parameters registered for this constant buffer.
        /// </summary>
        public readonly EffectParameterCollection Parameters;

        /// <summary>
        /// Updates the specified constant buffer from all parameters value.
        /// </summary>
        public void Update()
        {
            Update(device);
        }

        /// <summary>
        /// Copies the CPU content of this buffer to another constant buffer. 
        /// Destination buffer will be flagged as dirty.
        /// </summary>
        /// <param name="toBuffer">To buffer to receive the content.</param>
        public void CopyTo(EffectConstantBuffer toBuffer)
        {
            if (toBuffer == null)
                throw new ArgumentNullException(nameof(toBuffer));

            if (BackingBuffer.Size != toBuffer.BackingBuffer.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(toBuffer), "Size of the source and destination buffer are not the same");
            }

            Utilities.CopyMemory(toBuffer.BackingBuffer.DataPointer, BackingBuffer.DataPointer, BackingBuffer.Size);
            toBuffer.IsDirty = true;
        }

        /// <summary>
        /// Updates the specified constant buffer from all parameters value.
        /// </summary>
        /// <param name="graphicsDevice">The device.</param>
        public void Update(GraphicsDevice graphicsDevice)
        {
            if (IsDirty)
            {
                pointer.Pointer = BackingBuffer.DataPointer;
                pointer.Size = BackingBuffer.Size;
                NativeBuffer.SetData(graphicsDevice, pointer);
                IsDirty = false;
            }
        }

        public void CopyTo(Buffer buffer)
        {
            if (IsDirty)
            {
                pointer.Pointer = BackingBuffer.DataPointer;
                pointer.Size = BackingBuffer.Size;
                buffer.SetData(device, pointer);
                IsDirty = false;
            }
        }

        public bool Equals(EffectConstantBuffer other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            // Fast comparison using hashCode.
            return hashCode == other.hashCode && Description.Equals(other.Description);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EffectConstantBuffer)obj);
        }

        public override int GetHashCode()
        {
            // Return precalculated hashcode
            return hashCode;
        }

        public static bool operator ==(EffectConstantBuffer left, EffectConstantBuffer right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EffectConstantBuffer left, EffectConstantBuffer right)
        {
            return !Equals(left, right);
        }

        public static implicit operator AdamantiumVulkan.Core.Buffer(EffectConstantBuffer from)
        {
            return from.NativeBuffer;
        }

        public static implicit operator Buffer(EffectConstantBuffer from)
        {
            return from.NativeBuffer;
        }
    }
}
