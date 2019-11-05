﻿using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Graphics;
using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.Engine.Effects
{
    /// <summary>
    /// This class manages a pool of <see cref="Effect"/>.
    /// </summary>
    /// <remarks>
    /// This class is responsible to store all EffectData, create shareable constant buffers between effects and reuse shader EffectData instances.
    /// </remarks>
    public sealed class EffectPool : DisposableObject
    {
        #region Delegates

        public delegate Buffer ConstantBufferAllocatorDelegate(GraphicsDevice device, EffectPool pool, EffectConstantBuffer constantBuffer);

        #endregion


        internal readonly List<EffectData.Shader> RegisteredShaders;
        private readonly List<ShaderModule>[] compiledShadersGroup;
        private readonly GraphicsDevice graphicsDevice;
        private readonly List<Effect> effects;

        // GraphicsDevice => (ConstantBufferName => (EffectConstantBufferKey => EffectConstantBuffer))
        private readonly Dictionary<GraphicsDevice, Dictionary<string, Dictionary<EffectConstantBufferKey, EffectConstantBuffer>>> mapNameToConstantBuffer;
        private readonly Dictionary<EffectData, EffectData.Effect> registered;
        private readonly object syncObj = new object();
        private ConstantBufferAllocatorDelegate constantBufferAllocator;


        private EffectPool(GraphicsDevice device, string name = null)
           : base(name)
        {
            RegisteredShaders = new List<EffectData.Shader>();
            mapNameToConstantBuffer = new Dictionary<GraphicsDevice, Dictionary<string, Dictionary<EffectConstantBufferKey, EffectConstantBuffer>>>();
            compiledShadersGroup = new List<ShaderModule>[(int)EffectShaderType.Compute + 1];
            for (int i = 0; i < compiledShadersGroup.Length; i++)
            {
                compiledShadersGroup[i] = new List<ShaderModule>(256);
            }

            registered = new Dictionary<EffectData, EffectData.Effect>(new IdentityEqualityComparer<EffectData>());
            effects = new List<Effect>();
            graphicsDevice = device;
            constantBufferAllocator = DefaultConstantBufferAllocator;
            graphicsDevice.EffectPools.Add(this);
        }

        /// <summary>
        ///   Gets or sets the constant buffer allocator used to allocate a GPU constant buffer declared in an Effect.
        /// </summary>
        /// <remarks>
        ///   This delegate must be overridden when you want to control the creation of the GPU Constant buffer.
        ///   By default, the allocator is just allocating the buffer using "Buffer.Constant.New(size)" but
        ///   It is sometimes needed to create a constant buffer with different usage scenarios (using for example
        ///   a RawBuffer with multiple usages).
        ///   Setting this property to null will revert the default allocator.
        /// </remarks>
        public ConstantBufferAllocatorDelegate ConstantBufferAllocator
        {
            get
            {
                lock (mapNameToConstantBuffer)
                {
                    return constantBufferAllocator;
                }
            }
            set
            {
                lock (mapNameToConstantBuffer)
                {
                    constantBufferAllocator = value ?? DefaultConstantBufferAllocator;
                }
            }
        }

        /// <summary>
        /// Registers a EffectData to this pool.
        /// </summary>
        /// <param name="data">The effect data to register.</param>
        /// <returns>The effect description.</returns>
        public EffectData.Effect RegisterBytecode(EffectData data)
        {
            // Lock the whole EffectPool in case multiple threads would add EffectData at the same time.
            lock (syncObj)
            {
                EffectData.Effect effect;

                if (!registered.TryGetValue(data, out effect))
                {
                    // Pre-cache all input signatures
                    CacheInputSignature(data);

                    effect = RegisterInternal(data);
                    registered.Add(data, effect);

                    // Just allocate the compiled shaders array according to the current size of shader data
                    foreach (var compiledShaders in compiledShadersGroup)
                    {
                        for (int i = compiledShaders.Count; i < RegisteredShaders.Count; i++)
                        {
                            compiledShaders.Add(null);
                        }
                    }
                }

                return effect;
            }
        }

        private void CacheInputSignature(EffectData effectData)
        {
            // Iterate on all vertex shaders and make unique the bytecode
            // for faster comparison when creating input layout.
            foreach (var shader in effectData.Shaders)
            {
                if (shader.Type == EffectShaderType.Vertex && shader.InputSignature.Bytecode != null)
                {
                    var inputSignature = graphicsDevice.GetOrCreateInputSignatureManager(shader.InputSignature.Bytecode, shader.InputSignature.Hashcode);
                    shader.InputSignature.Bytecode = inputSignature.Bytecode;
                }
            }
        }

        /// <summary>
        /// Readonly collection of registered effects
        /// </summary>
        public ReadOnlyCollection<Effect> RegisteredEffects
        {
            get
            {
                lock (syncObj)
                {
                    return effects.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Called when Effect added
        /// </summary>
        public event EventHandler<EffectPoolEventArgs> EffectAdded;

        /// <summary>
        /// Called when Effect removed
        /// </summary>
        public event EventHandler<EffectPoolEventArgs> EffectRemoved;

        internal void AddEffect(Effect effect)
        {
            lock (effects)
            {
                effects.Add(effect);
            }
            OnEffectAdded(new EffectPoolEventArgs(effect));
        }

        internal void RemoveEffect(Effect effect)
        {
            lock (effects)
            {
                effects.Remove(effect);
            }
            OnEffectRemoved(new EffectPoolEventArgs(effect));
        }

        internal ShaderModule GetOrCompileShader(EffectShaderType shaderType, int index, int soRasterizedStream, StreamOutputElement[] soElements, out string profileError)
        {
            ShaderModule shader = null;
            profileError = null;
            lock (syncObj)
            {
                shader = compiledShadersGroup[(int)shaderType][index];
                if (shader == null)
                {
                    if (RegisteredShaders[index].Level > graphicsDevice.Features.Level)
                    {
                        profileError = $"{RegisteredShaders[index].Level}";
                        return null;
                    }

                    var bytecodeRaw = RegisteredShaders[index].Bytecode;
                    switch (shaderType)
                    {
                        case EffectShaderType.Vertex:
                            shader = new VertexShader(graphicsDevice, bytecodeRaw);
                            break;
                        case EffectShaderType.Domain:
                            shader = new DomainShader(graphicsDevice, bytecodeRaw);
                            break;
                        case EffectShaderType.Hull:
                            shader = new HullShader(graphicsDevice, bytecodeRaw);
                            break;
                        case EffectShaderType.Geometry:
                            if (soElements != null)
                            {
                                // Calculate the strides
                                var soStrides = new List<int>();
                                foreach (var streamOutputElement in soElements)
                                {
                                    for (int i = soStrides.Count; i < (streamOutputElement.Stream + 1); i++)
                                    {
                                        soStrides.Add(0);
                                    }

                                    soStrides[streamOutputElement.Stream] += streamOutputElement.ComponentCount * sizeof(float);
                                }
                                shader = new GeometryShader(graphicsDevice, bytecodeRaw, soElements, soStrides.ToArray(), soRasterizedStream);
                            }
                            else
                            {
                                shader = new GeometryShader(graphicsDevice, bytecodeRaw);
                            }
                            break;
                        case EffectShaderType.Fragment:
                            shader = new PixelShader(graphicsDevice, bytecodeRaw);
                            break;
                        case EffectShaderType.Compute:
                            shader = new ComputeShader(graphicsDevice, bytecodeRaw);
                            break;
                    }
                    compiledShadersGroup[(int)shaderType][index] = ToDispose(shader);
                }
            }
            return shader;
        }

        internal EffectConstantBuffer GetOrCreateConstantBuffer(GraphicsDevice context, EffectData.ConstantBuffer bufferRaw)
        {
            // Only lock the constant buffer object
            lock (mapNameToConstantBuffer)
            {
                Dictionary<string, Dictionary<EffectConstantBufferKey, EffectConstantBuffer>> nameToConstantBufferList;

                // ----------------------------------------------------------------------------
                // 1) Get the cache of constant buffers for a particular GraphicsDevice
                // ----------------------------------------------------------------------------
                // TODO cache is not clear if a GraphicsDevice context is disposed
                // To simplify, we assume that a GraphicsDevice is alive during the whole life of the application.
                if (!mapNameToConstantBuffer.TryGetValue(context, out nameToConstantBufferList))
                {
                    nameToConstantBufferList = new Dictionary<string, Dictionary<EffectConstantBufferKey, EffectConstantBuffer>>();
                    mapNameToConstantBuffer[context] = nameToConstantBufferList;
                }

                // ----------------------------------------------------------------------------
                // 2) Get a set of constant buffers for a particular constant buffer name
                // ----------------------------------------------------------------------------
                Dictionary<EffectConstantBufferKey, EffectConstantBuffer> bufferSet;
                if (!nameToConstantBufferList.TryGetValue(bufferRaw.Name, out bufferSet))
                {
                    bufferSet = new Dictionary<EffectConstantBufferKey, EffectConstantBuffer>();
                    nameToConstantBufferList[bufferRaw.Name] = bufferSet;
                }

                // ----------------------------------------------------------------------------
                // 3) Get an existing constant buffer having the same name/size/layout/parameters
                // ----------------------------------------------------------------------------
                var bufferKey = new EffectConstantBufferKey(bufferRaw);
                EffectConstantBuffer buffer;
                if (!bufferSet.TryGetValue(bufferKey, out buffer))
                {
                    // 4) If this buffer doesn't exist, create a new one and register it.
                    buffer = new EffectConstantBuffer(graphicsDevice, bufferRaw);
                    bufferSet[bufferKey] = ToDispose(buffer);
                }

                return buffer;
            }
        }

        /// <summary>
        /// Creates a new effect pool from a specified list of <see cref="EffectData" />.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <returns>An instance of <see cref="EffectPool" />.</returns>
        public static EffectPool New(GraphicsDevice device)
        {
            return new EffectPool(device);
        }

        /// <summary>
        /// Creates a new named effect pool from a specified list of <see cref="EffectData" />.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="name">The name of this effect pool.</param>
        /// <returns>An instance of <see cref="EffectPool" />.</returns>
        public static EffectPool New(GraphicsDevice device, string name)
        {
            return new EffectPool(device);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString() => $"EffectPool [{Name}]";

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);

            if (disposeManagedResources)
                graphicsDevice.EffectPools.Remove(this);
        }

        /// <summary>
        /// Merges an existing <see cref="EffectData" /> into this instance.
        /// </summary>
        /// <param name="source">The EffectData to merge.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        /// <remarks>This method is useful to build an archive of several effects.</remarks>
        private EffectData.Effect RegisterInternal(EffectData source)
        {
            var effect = source.Description;

            var effectRuntime = new EffectData.Effect()
            {
                Name = effect.Name,
                Arguments = effect.Arguments,
                ShareConstantBuffers = effect.ShareConstantBuffers,
                Techniques = new List<EffectData.Technique>(effect.Techniques.Count)
            };

            Logger logger = null;

            foreach (var techniqueOriginal in effect.Techniques)
            {
                var technique = techniqueOriginal.Clone();
                effectRuntime.Techniques.Add(technique);

                foreach (var pass in technique.Passes)
                {
                    foreach (var shaderLink in pass.Pipeline)
                    {
                        // No shader set for this stage
                        if (shaderLink == null) continue;

                        // If the shader is an import, we try first to resolve it directly
                        if (shaderLink.IsImport)
                        {
                            var index = FindShaderByName(shaderLink.ImportName);
                            if (index >= 0)
                            {
                                shaderLink.ImportName = null;
                                shaderLink.Index = index;
                            }
                            else
                            {
                                if (logger == null)
                                {
                                    logger = new Logger();
                                }

                                logger.Error("Cannot find shader import by name [{0}]", shaderLink.ImportName);
                            }
                        }
                        else if (!shaderLink.IsNullShader)
                        {
                            var shader = source.Shaders[shaderLink.Index];

                            // Find a similar shader
                            var shaderIndex = FindSimilarShader(shader);

                            if (shaderIndex >= 0)
                            {
                                var previousShader = RegisteredShaders[shaderIndex];

                                // If the previous shader is 
                                if (shader.Name != null)
                                {
                                    // if shader from this instance is local and shader from source is global => transform current shader to global
                                    if (previousShader.Name == null)
                                    {
                                        previousShader.Name = shader.Name;
                                    }
                                    else if (shader.Name != previousShader.Name)
                                    {
                                        if (logger == null)
                                        {
                                            logger = new Logger();
                                        }
                                        // If shader from this instance is global and shader from source is global => check names. If exported names are different, this is an error
                                        logger.Error("Cannot merge shader [{0}] into this instance, as there is already a global shader with a different name [{1}]", shader.Name, previousShader.Name);
                                    }
                                }

                                shaderLink.Index = shaderIndex;
                            }
                            else
                            {
                                shaderLink.Index = RegisteredShaders.Count;
                                RegisteredShaders.Add(shader);
                            }
                        }
                    }
                }
            }

            if (logger != null && logger.HasErrors)
                throw new InvalidOperationException(Utilities.Join("\r\n", logger.Messages));

            return effectRuntime;
        }

        private int FindSimilarShader(EffectData.Shader shader)
        {
            for (int i = 0; i < RegisteredShaders.Count; i++)
            {
                if (RegisteredShaders[i].IsSimilar(shader))
                    return i;
            }
            return -1;
        }

        private int FindShaderByName(string name)
        {
            for (int i = 0; i < RegisteredShaders.Count; i++)
            {
                if (RegisteredShaders[i].Name == name)
                    return i;
            }
            return -1;

        }

        private static Buffer DefaultConstantBufferAllocator(GraphicsDevice device, EffectPool pool, EffectConstantBuffer constantBuffer)
        {
            return Buffer.Uniform.New(device, constantBuffer.BackingBuffer.Size);
        }

        private void OnEffectAdded(EffectPoolEventArgs e)
        {
            EventHandler<EffectPoolEventArgs> handler = EffectAdded;
            handler?.Invoke(this, e);
        }

        private void OnEffectRemoved(EffectPoolEventArgs e)
        {
            EventHandler<EffectPoolEventArgs> handler = EffectRemoved;
            handler?.Invoke(this, e);
        }
    }
}