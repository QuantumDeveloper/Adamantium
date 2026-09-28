using System;
using Adamantium.Core;
using Adamantium.EffectsCompiler;

namespace Adamantium.Graphics.Core.EffectsFramework;

/// <summary>
/// A constant buffer exposed by an effect.
/// </summary>
/// <remarks>
/// Constant buffers are created and shared inside a same <see cref="EffectPool"/>. The creation of the underlying GPU buffer
/// </remarks>
public sealed class EffectConstantBuffer : DisposableObject, IEquatable<EffectConstantBuffer>
{
    /// <summary>
    /// <see cref="DataBuffer"/> for buffering variables
    /// </summary>
    public readonly DataBuffer BackingBuffer;
    public Guid Id { get; } = Guid.NewGuid();

    public readonly EffectData.ConstantBuffer Description;
    
    private readonly int hashCode;
    
    private ulong _contentHash;

    public EffectConstantBuffer(EffectData.ConstantBuffer description)
    {
        var alignedSize = Utilities.AlignSize((uint)description.Size, 16);
        BackingBuffer = new DataBuffer(alignedSize);
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
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets the parameters registered for this constant buffer.
    /// </summary>
    public readonly EffectParameterCollection Parameters;
    
    public void CheckForChanges()
    {
        var newHash = CalculateHash();
        if (newHash != _contentHash)
        {
            _contentHash = newHash;
            IsDirty = true;
        }
        else
        {
            IsDirty = false;
        }
    }

    private unsafe ulong CalculateHash()
    {
        var dataSpan = new Span<byte>(BackingBuffer.DataPointer.ToPointer(), (int)BackingBuffer.Size);
        // Use a quick non-cryptographic hash function
        return System.IO.Hashing.Crc64.HashToUInt64(dataSpan);
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

        Utilities.CopyMemory(toBuffer.BackingBuffer.DataPointer, BackingBuffer.DataPointer, (long)BackingBuffer.Size);
        toBuffer.IsDirty = true;
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
}