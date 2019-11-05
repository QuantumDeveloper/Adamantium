using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Graphics;

namespace Adamantium.Engine.Effects
{
    /// <summary>
    /// Effect class. Representing a wrapper over the shaders to make work with shaders easier
    /// </summary>
    [ContentReader(typeof(EffectContentReader))]
    public class Effect : GraphicsResource
    {
        /// <summary>
        /// Occurs when the effect is being initialized (after a recompilation at runtime for example)
        /// </summary>
        public event EventHandler<EventArgs> Initialized;

        public delegate EffectPass OnApplyDelegate(EffectPass pass);

        private Dictionary<EffectConstantBufferKey, EffectConstantBuffer> effectConstantBuffersCache;

        internal EffectResourceLinker ResourceLinker { get; private set; }

        /// <summary>
        /// Gets a collection of constant buffers that are defined for this effect.
        /// </summary>
        public EffectConstantBufferCollection ConstantBuffers { get; private set; }

        /// <summary>
        /// Gets a collection of parameters that are defined for this effect.
        /// </summary>
        public EffectParameterCollection Parameters { get; private set; }

        /// <summary>
        /// Gets a collection of techniques that are defined for this effect.
        /// </summary>
        public EffectTechniqueCollection Techniques { get; private set; }

        /// <summary>
        /// Gets the data associated to this effect.
        /// </summary> 
        public EffectData.Effect RawEffectData { get; private set; }

        /// <summary>
        /// Set to <c>true</c> to force all constant shaders to be shared between other effects within a common <see cref="EffectPool"/>. Default is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This value can also be set in the TKFX file directly by setting ShareConstantBuffers = <c>true</c>; in a pass.
        /// </remarks>
        protected internal bool ShareConstantBuffers;

        /// <summary>
        /// Initializes a new instance of the <see cref="Effect"/> class with the specified bytecode effect. See remarks.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="bytecode">The bytecode to add to <see cref="D3DGraphicsDevice.DefaultEffectPool"/>. This bytecode must contain only one effect.</param>
        /// <exception cref="ArgumentException">If the bytecode doesn't contain a single effect.</exception>
        /// <remarks>
        /// The effect bytecode must contain only a single effect and will be registered into the <see cref="D3DGraphicsDevice.DefaultEffectPool"/>.
        /// </remarks>
        public Effect(GraphicsDevice device, byte[] bytecode)
           : this(device, EffectData.Load(bytecode))
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Effect" /> class with the specified bytecode effect. See remarks.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="effectData">The bytecode to add to the Effect Pool. This bytecode must contain only one effect.</param>
        /// <param name="effectPool">The effect pool used to register the bytecode. Default is <see cref="D3DGraphicsDevice.DefaultEffectPool"/>.</param>
        /// <exception cref="ArgumentException">If the bytecode doesn't contain a single effect.</exception>
        /// <remarks>The effect bytecode must contain only a single effect and will be registered into the <see cref="D3DGraphicsDevice.DefaultEffectPool"/>.</remarks>
        public Effect(GraphicsDevice device, EffectData effectData, EffectPool effectPool = null)
           : base(device)
        {
            CreateInstanceFrom(device, effectData, effectPool);
        }

        /// <summary>
        /// Gets the pool this effect attached to.
        /// </summary>
        /// <value> The pool. </value>
        public EffectPool Pool { get; private set; }

        /// <summary>
        /// Occurs when the on apply is applied on a pass.
        /// </summary>
        /// <remarks>
        /// This external hook provides a way to pre-configure a pipeline when a pass is applied.
        /// Subclass of this class can override the method <see cref="OnApply"/>.
        /// </remarks>
        public event OnApplyDelegate OnApplyCallback;

        /// <summary>
        /// Gets or sets the current technique. By default, it is set to the first available technique in this effect.
        /// </summary>
        /// <value>The current technique.</value>
        public EffectTechnique CurrentTechnique { get; set; }

        /// <summary>
        /// The effect is supporting dynamic compilation.
        /// </summary>
        public bool IsSupportingDynamicCompilation { get; private set; }

        internal void CreateInstanceFrom(GraphicsDevice device, EffectData effectData, EffectPool effectPool)
        {
            GraphicsDevice = device;
            ConstantBuffers = new EffectConstantBufferCollection();
            Parameters = new EffectParameterCollection();
            Techniques = new EffectTechniqueCollection();

            //Pool = effectPool ?? device.DefaultEffectPool;

            // Sets the effect name
            Name = effectData.Description.Name;

            // Register the bytecode to the pool
            var effect = Pool.RegisterBytecode(effectData);

            // Initialize from effect
            InitializeFrom(effect, null);

            // If everything was fine, then we can register it into the pool
            Pool.AddEffect(this);
        }

        /// <summary>
        /// Binds the specified effect data to this instance.
        /// </summary>
        /// <param name="effectDataArg">The effect data arg.</param>
        /// <param name="cloneFromEffect">The clone from effect.</param>
        /// <exception cref="System.InvalidOperationException">If no techniques found in this effect.</exception>
        /// <exception cref="System.ArgumentException">If unable to find effect [effectName] from the EffectPool.</exception>
        internal void InitializeFrom(EffectData.Effect effectDataArg, Effect cloneFromEffect)
        {
            RawEffectData = effectDataArg;

            // Clean any previously allocated resources
            DisposeCollector?.DisposeAndClear();
            ConstantBuffers.Clear();
            Parameters.Clear();
            Techniques.Clear();
            ResourceLinker = new EffectResourceLinker();
            effectConstantBuffersCache?.Clear();

            // Copy data
            IsSupportingDynamicCompilation = RawEffectData.Arguments != null;
            ShareConstantBuffers = RawEffectData.ShareConstantBuffers;

            // Create the local effect constant buffers cache
            if (!ShareConstantBuffers)
                effectConstantBuffersCache = new Dictionary<EffectConstantBufferKey, EffectConstantBuffer>();

            var logger = new Logger();
            int techniqueIndex = 0;
            int totalPassCount = 0;
            EffectPass parentPass = null;
            foreach (var techniqueRaw in RawEffectData.Techniques)
            {
                var name = techniqueRaw.Name;
                if (string.IsNullOrEmpty(name))
                    name = $"${techniqueIndex++}";

                var technique = new EffectTechnique(this, name);
                Techniques.Add(technique);

                int passIndex = 0;
                foreach (var passRaw in techniqueRaw.Passes)
                {
                    name = passRaw.Name;
                    if (string.IsNullOrEmpty(name))
                        name = $"${passIndex++}";

                    var pass = new EffectPass(logger, this, technique, passRaw, name);

                    pass.Initialize(logger);

                    // If this is a subpass, add it to the parent pass
                    if (passRaw.IsSubPass)
                    {
                        if (parentPass == null)
                        {
                            logger.Error("Pass [{0}] is declared as a subpass but has no parent.");
                        }
                        else
                        {
                            parentPass.SubPasses.Add(pass);
                        }
                    }
                    else
                    {
                        technique.Passes.Add(pass);
                        parentPass = pass;
                    }
                }

                // Count the number of passes
                totalPassCount += technique.Passes.Count;
            }

            if (totalPassCount == 0)
                throw new InvalidOperationException("No passes found in this effect.");

            // Log all the exception in a single throw
            if (logger.HasErrors)
                throw new InvalidOperationException(Utilities.Join("\n", logger.Messages));

            // Initialize the resource linker when we are done with all pass/parameters
            ResourceLinker.Initialize();

            //// Sort all parameters by their resource types
            //// in order to achieve better local cache coherency in resource linker
            Parameters.Items.Sort((left, right) =>
            {
             // First, order first all value types, then resource type
             var comparison = left.IsValueType != right.IsValueType ? left.IsValueType ? -1 : 1 : 0;

             // If same type
             if (comparison == 0)
             {
                 // Order by resource type
                 comparison = ((int) left.ResourceType).CompareTo((int) right.ResourceType);

                 // If same, order by resource index
                 if (comparison == 0)
                 {
                     comparison = left.Offset.CompareTo(right.Offset);
                 }
             }

             return comparison;
            });

            // Prelink constant buffers
            int resourceIndex = 0;
            foreach (var parameter in Parameters)
            {
                // Recalculate parameter resource index
                if (!parameter.IsValueType)
                {
                    parameter.Offset = resourceIndex;
                    resourceIndex += parameter.ElementCount;
                }

                // Set the default values 
                parameter.SetDefaultValue();

                if (parameter.ResourceType == EffectResourceType.ConstantBuffer)
                    parameter.SetResource(ConstantBuffers[parameter.Name]);
            }

            // Compute slot links
            foreach (var technique in Techniques)
            {
                foreach (var pass in technique.Passes)
                {
                    foreach (var subPass in pass.SubPasses)
                    {
                        subPass.ComputeSlotLinks();
                    }
                    //pass.ComputeSlotLinks();
                    pass.ComputeSlotLinks();
                }
            }

            // Setup the first Current Technique.
            CurrentTechnique = Techniques[0];

            // If this is a clone, we need to 
            if (cloneFromEffect != null)
            {
                // Copy the content of the constant buffers to the new instance.
                for (int i = 0; i < ConstantBuffers.Count; i++)
                {
                    cloneFromEffect.ConstantBuffers[i].CopyTo(ConstantBuffers[i]);
                }

                // Copy back all bound resources except constant buffers
                // that are already initialized with InitializeFrom method.

                foreach (var boundResource in cloneFromEffect.ResourceLinker.BoundResources)
                {
                    if (boundResource.Value is EffectConstantBuffer)
                        continue;

                    ResourceLinker.BoundResources.Add(boundResource.Key, boundResource.Value);
                }

                // If everything was fine, then we can register it into the pool
                Pool.AddEffect(this);
            }

            // Allow subclasses to complete initialization.
            Initialize();

            OnInitialized();
        }

        protected virtual void Initialize()
        {
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>A new instance of this Effect.</returns>
        public virtual Effect Clone()
        {
            var effect = (Effect)MemberwiseClone();
            effect.DisposeCollector = new DisposeCollector();
            effect.ConstantBuffers = new EffectConstantBufferCollection();
            effect.Parameters = new EffectParameterCollection();
            effect.Techniques = new EffectTechniqueCollection();
            effect.effectConstantBuffersCache = null;

            // Initialize from effect
            effect.InitializeFrom(effect.RawEffectData, this);

            return effect;
        }

        internal new DisposeCollector DisposeCollector
        {
            get { return base.DisposeCollector; }
            private set { base.DisposeCollector = value; }
        }

        protected internal virtual EffectPass OnApply(EffectPass pass)
        {
            var handler = OnApplyCallback;
            if (handler != null) pass = handler(pass);

            return pass;
        }

        /// <summary>
        /// Disposes of object resources.
        /// </summary>
        /// <param name="disposeManagedResources">If true, managed resources should be
        /// disposed of in addition to unmanaged resources.</param>
        protected override void Dispose(bool disposeManagedResources)
        {
            // Remove this instance from the pool
            Pool.RemoveEffect(this);

            base.Dispose(disposeManagedResources);
        }

        internal EffectConstantBuffer GetOrCreateConstantBuffer(GraphicsDevice context, EffectData.ConstantBuffer bufferRaw)
        {
            EffectConstantBuffer constantBuffer;
            // Is the effect is using shared constant buffers via the EffectPool?
            if (ShareConstantBuffers)
            {
                // Use the pool to share constant buffers
                constantBuffer = Pool.GetOrCreateConstantBuffer(context, bufferRaw);
            }
            else
            {
                // ----------------------------------------------------------------------------
                // Get an existing constant buffer having the same name/size/layout/parameters
                // ----------------------------------------------------------------------------
                var bufferKey = new EffectConstantBufferKey(bufferRaw);
                if (!effectConstantBuffersCache.TryGetValue(bufferKey, out constantBuffer))
                {
                    // 4) If this buffer doesn't exist, create a new one and register it.
                    constantBuffer = new EffectConstantBuffer(context, bufferRaw);
                    effectConstantBuffersCache.Add(bufferKey, constantBuffer);
                    ToDispose(constantBuffer);
                }
            }

            return constantBuffer;
        }

        /// <summary>
        /// Initialized event handler
        /// </summary>
        protected virtual void OnInitialized()
        {
            EventHandler<EventArgs> handler = Initialized;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public static Effect Load(string path, GraphicsDevice device)
        {
            var data = EffectData.Load(path);
            return new Effect(device, data);
        }

        public static Effect Load(Stream stream, GraphicsDevice device)
        {
            var data = EffectData.Load(stream);
            return new Effect(device, data);
        }

        public static Effect Load(byte[] data, GraphicsDevice device)
        {
            var effectData = EffectData.Load(data);
            return new Effect(device, effectData);
        }
    }
}
