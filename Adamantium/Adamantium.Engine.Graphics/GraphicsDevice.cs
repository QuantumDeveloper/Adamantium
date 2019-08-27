using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsDevice : DisposableObject
    {
        private VulkanInstance instance;
        private PhysicalDevice physicalDevice;
        private Device logicalDevice;
        private Queue graphicsQueue;

        

        private GraphicsDevice(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            this.instance = instance;
            this.physicalDevice = physicalDevice;
            CreateLogicalDevice();
        }

        //private Device2 Device;
        //private DeviceContext2 Context;
        //private DeviceDebug debug;

        //private DepthStencilBuffer currentDepthStencil;
        //private RenderTarget2D currentRenderTarget;
        //private RenderTarget2D[] currentRenderTargets;


        //private RasterizerState _rasterizerState;
        //private DepthStencilState _depthStencilState;
        //private BlendState _blendState;
        //private readonly Dictionary<InputSignatureKey, InputSignatureManager> inputSignatureCache;
        //private List<RenderTargetView> rendertargets = new List<RenderTargetView>();

        //internal GraphicsAdapter Adapter { get; private set; }
        //internal EffectPass CurrentPass { get; set; }
        //internal CommonShaderStage[] ShaderStages;
        //internal VertexShaderStage VertexShaderStage;
        //internal HullShaderStage HullShaderStage;
        //internal DomainShaderStage DomainShaderStage;
        //internal GeometryShaderStage GeometryShaderStage;
        //internal PixelShaderStage PixelShaderStage;
        //internal ComputeShaderStage ComputeShaderStage;
        //private VertexInputLayout _vertexInputLayout;
        //private Color4F _blendFactor;
        //private int _blendSampleMask;

        //public ObservableCollection<EffectPool> EffectPools { get; set; }

        //public EffectPool DefaultEffectPool { get; internal set; }

        //public FullScreenQuad Quad { get; internal set; }

        public static VulkanInstance Instance { get; private set; }

        //public Boolean IsD2dSupportEnabled { get; private set; }

        //public Boolean IsVideoSupportEnabled { get; private set; }

        //public Boolean IsDeferred { get; private set; }

        //public RasterizerStateCollection RasterizerStates { get; private set; }

        //public DepthStencilStatesCollection DepthStencilStates { get; private set; }

        //public BlendStatesCollection BlendStates { get; private set; }

        //public SamplerStateCollection SamplersStates { get; private set; }

        //public GraphicsDevice MainDevice { get; private set; }

        //public GraphicsDeviceFeatures Features { get; private set; }

        public static GraphicsDevice Create(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            return new GraphicsDevice(instance, physicalDevice);
        }

        private void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(physicalDevice);

            var queueInfos = new List<DeviceQueueCreateInfo>();
            HashSet<uint> uniqueQueueFamilies = new HashSet<uint>() { indices.graphicsFamily.Value, indices.presentFamily.Value };
            float queuePriority = 1.0f;
            foreach (var queueFamily in uniqueQueueFamilies)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo();
                queueCreateInfo.QueueFamilyIndex = queueFamily;
                queueCreateInfo.QueueCount = 1;
                queueCreateInfo.PQueuePriorities = queuePriority;
                queueInfos.Add(queueCreateInfo);
            }

            var deviceFeatures = physicalDevice.GetPhysicalDeviceFeatures();
            deviceFeatures.SamplerAnisotropy = true;

            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.PEnabledFeatures = deviceFeatures;
            createInfo.EnabledExtensionCount = (uint)VulkanInstance.DeviceExtensions.Count;
            createInfo.PpEnabledExtensionNames = VulkanInstance.DeviceExtensions.ToArray();

            if (instance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PpEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
            }

            logicalDevice = physicalDevice.CreateDevice(createInfo);

            createInfo.Dispose();

            graphicsQueue = logicalDevice.GetDeviceQueue(indices.graphicsFamily.Value, 0);
            
        }

        public void DeviceWaitIdle()
        {
            logicalDevice.DeviceWaitIdle();
        }

        //public BlendState BlendState
        //{
        //    get => _blendState;
        //    set
        //    {
        //        _blendState = value;
        //        Context.OutputMerger.BlendState = value;
        //        Context.OutputMerger.BlendFactor = value.BlendFactor;
        //        Context.OutputMerger.BlendSampleMask = value.MultiSampleMask;
        //    }
        //}

        //public Color4F BlendFactor
        //{
        //    get => _blendFactor;
        //    set
        //    {
        //        _blendFactor = value;
        //        Context.OutputMerger.BlendFactor = value;
        //    }
        //}

        //public int BlendSampleMask
        //{
        //    get => _blendSampleMask;
        //    set
        //    {
        //        _blendSampleMask = value;
        //        Context.OutputMerger.BlendSampleMask = value;
        //    }
        //}

        //public RasterizerState RasterizerState
        //{
        //    get => _rasterizerState;
        //    set
        //    {
        //        _rasterizerState = value;
        //        Context.Rasterizer.State = value;
        //    }
        //}

        //public bool IsFeatureLevelSupported(FeatureLevel level, GraphicsAdapter adapter = null)
        //{
        //    if (APIVersion == GAPIVersion.DirectX11)
        //    {
        //        if (adapter == null)
        //        {
        //            adapter = Adapter;
        //        }
        //        return SharpDX.Direct3D11.Device.IsSupportedFeatureLevel(adapter, level);
        //    }

        //    return false;
        //}

        //public VertexInputLayout VertexInputLayout
        //{
        //    get { return _vertexInputLayout; }
        //    set { _vertexInputLayout = value; }
        //}

        //public void GenerateMips(Texture texture)
        //{
        //    Context.GenerateMips(texture);
        //}

        //public void SetScissorRectangle(int left, int top, int right, int bottom)
        //{
        //    Context.Rasterizer.SetScissorRectangle(left, top, right, bottom);
        //}

        //public void SetScissorRectangles(params RawRectangle[] rectangles)
        //{
        //    Context.Rasterizer.SetScissorRectangles(rectangles);
        //}

        //public DepthStencilState DepthStencilState
        //{
        //    get => _depthStencilState;
        //    set
        //    {
        //        _depthStencilState = value;
        //        Context.OutputMerger.DepthStencilState = _depthStencilState;
        //    }
        //}

        ///// <summary>	
        ///// <p>Execute a command list from a thread group.</p>	
        ///// </summary>
        //public void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        //{
        //    Context.Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        //}

        ///// <summary>	
        ///// <p>Execute a command list over one or more thread groups.</p>	
        ///// </summary>
        //public void Dispatch(Buffer argumentsBuf, int alignByteOffsetForArgs)
        //{
        //    Context.DispatchIndirect(argumentsBuf, alignByteOffsetForArgs);
        //}

        ////Constructor for Main Device with Immediate context
        //internal GraphicsDevice(GraphicsAdapter adapter, DeviceCreationFlags deviceCreationFlags, params FeatureLevel[] featureLevels)
        //{
        //    Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

        //    IsD2dSupportEnabled = (deviceCreationFlags & DeviceCreationFlags.BgraSupport) != 0;
        //    IsInDebugMode = (deviceCreationFlags & DeviceCreationFlags.Debug) != 0;
        //    IsVideoSupportEnabled = (deviceCreationFlags & DeviceCreationFlags.VideoSupport) != 0;
        //    inputSignatureCache = new Dictionary<InputSignatureKey, InputSignatureManager>();

        //    CreateMainDevice(adapter, deviceCreationFlags, featureLevels);
        //}

        //public GraphicsDeviceStatus DeviceStatus
        //{
        //    get
        //    {
        //        var result = Device.DeviceRemovedReason;

        //        if (result == SharpDX.DXGI.ResultCode.DeviceRemoved)
        //        {
        //            return GraphicsDeviceStatus.Removed;
        //        }

        //        if (result == SharpDX.DXGI.ResultCode.DeviceHung)
        //        {
        //            return GraphicsDeviceStatus.Hung;
        //        }

        //        if (result == SharpDX.DXGI.ResultCode.DeviceReset)
        //        {
        //            return GraphicsDeviceStatus.Reset;
        //        }

        //        if (result == SharpDX.DXGI.ResultCode.DriverInternalError)
        //        {
        //            return GraphicsDeviceStatus.InternalError;
        //        }

        //        if (result == SharpDX.DXGI.ResultCode.InvalidCall)
        //        {
        //            return GraphicsDeviceStatus.InvalidCall;
        //        }

        //        if (result.Code < 0)
        //        {
        //            return GraphicsDeviceStatus.Reset;
        //        }

        //        return GraphicsDeviceStatus.Normal;
        //    }
        //}

        //// Create Device and Context
        //private void CreateMainDevice(GraphicsAdapter adapter, DeviceCreationFlags deviceCreationFlags, params FeatureLevel[] featureLevel)
        //{
        //    var device = new Device(adapter, deviceCreationFlags, featureLevel);
        //    Device = ToDispose(device.QueryInterface<Device2>());
        //    device.Dispose();
        //    Context = Device.ImmediateContext2;
        //    RasterizerStates = ToDispose(new RasterizerStateCollection(this));
        //    BlendStates = ToDispose(new BlendStatesCollection(this));
        //    DepthStencilStates = ToDispose(new DepthStencilStatesCollection(this));
        //    SamplersStates = ToDispose(new SamplerStateCollection(this));
        //    Features = new GraphicsDeviceFeatures(Device);

        //    if (deviceCreationFlags.HasFlag(DeviceCreationFlags.Debug))
        //    {
        //        debug = ToDispose(Device.QueryInterface<DeviceDebug>());
        //    }

        //    MainDevice = this;
        //    var multithreaded = ToDispose(Device.QueryInterface<DeviceMultithread>());
        //    multithreaded.SetMultithreadProtected(true);

        //    EffectPools = new ObservableCollection<EffectPool>();
        //    DefaultEffectPool = EffectPool.New(this);

        //    RasterizerState = RasterizerStates.CullBackClipEnabled;
        //    DepthStencilState = DepthStencilStates.DepthEnableGreaterEqual;
        //    BlendState = BlendStates.Default;

        //    // Precompute shader stages
        //    ShaderStages = new CommonShaderStage[]
        //                          {
        //                           Context.VertexShader,
        //                           Context.HullShader,
        //                           Context.DomainShader,
        //                           Context.GeometryShader,
        //                           Context.PixelShader,
        //                           Context.ComputeShader
        //                          };

        //    HullShaderStage = Context.HullShader;
        //    DomainShaderStage = Context.DomainShader;
        //    GeometryShaderStage = Context.GeometryShader;
        //    PixelShaderStage = Context.PixelShader;
        //    VertexShaderStage = Context.VertexShader;
        //    ComputeShaderStage = Context.ComputeShader;

        //    Quad = ToDispose(new FullScreenQuad(this));
        //}

        ////Deferred device constructor
        //internal GraphicsDevice(GraphicsDevice mainDevice)
        //{
        //    Device = mainDevice;
        //    MainDevice = mainDevice.MainDevice;
        //    var context = new DeviceContext(Device);
        //    Context = ToDispose(context.QueryInterface<DeviceContext2>());
        //    context.Dispose();
        //    RasterizerStates = mainDevice.RasterizerStates;
        //    BlendStates = mainDevice.BlendStates;
        //    DepthStencilStates = mainDevice.DepthStencilStates;
        //    SamplersStates = mainDevice.SamplersStates;
        //    Features = new GraphicsDeviceFeatures(Device);

        //    IsDeferred = true;
        //    IsD2dSupportEnabled = mainDevice.IsD2dSupportEnabled;
        //    IsInDebugMode = mainDevice.IsInDebugMode;
        //    IsVideoSupportEnabled = mainDevice.IsVideoSupportEnabled;

        //    inputSignatureCache = mainDevice.inputSignatureCache;
        //    ShaderStages = new CommonShaderStage[]
        //                          {
        //                           Context.VertexShader,
        //                           Context.HullShader,
        //                           Context.DomainShader,
        //                           Context.GeometryShader,
        //                           Context.PixelShader,
        //                           Context.ComputeShader
        //                          };
        //    HullShaderStage = Context.HullShader;
        //    DomainShaderStage = Context.DomainShader;
        //    GeometryShaderStage = Context.GeometryShader;
        //    PixelShaderStage = Context.PixelShader;
        //    VertexShaderStage = Context.VertexShader;
        //    ComputeShaderStage = Context.ComputeShader;

        //    EffectPools = mainDevice.EffectPools;
        //    DefaultEffectPool = mainDevice.DefaultEffectPool;

        //    RasterizerState = RasterizerStates.CullBackClipEnabled;
        //    DepthStencilState = DepthStencilStates.DepthEnableGreaterEqual;
        //    BlendState = BlendStates.Default;
        //    Quad = ToDispose(new FullScreenQuad(this));
        //    inputSignatureCache = mainDevice.inputSignatureCache;
        //}

        ///// <summary>
        ///// Gets or create an input signature manager for a particular signature.
        ///// </summary>
        ///// <param name="signatureBytecode">The signature bytecode.</param>
        ///// <param name="signatureHashcode">The signature hashcode.</param>
        ///// <returns></returns>
        //internal InputSignatureManager GetOrCreateInputSignatureManager(byte[] signatureBytecode, int signatureHashcode)
        //{
        //    var key = new InputSignatureKey(signatureBytecode, signatureHashcode);

        //    InputSignatureManager signatureManager;

        //    // Lock all input signatures, as they are shared between all shaders/graphics device instances.
        //    lock (inputSignatureCache)
        //    {
        //        if (!inputSignatureCache.TryGetValue(key, out signatureManager))
        //        {
        //            signatureManager = ToDispose(new InputSignatureManager(this, signatureBytecode));
        //            inputSignatureCache.Add(key, signatureManager);
        //        }
        //    }

        //    return signatureManager;
        //}

        //public static GraphicsDevice Create(GraphicsDeviceParameters parameters)
        //{
        //    if (parameters == null)
        //    {
        //        throw new ArgumentNullException(nameof(parameters));
        //    }
        //    return new GraphicsDevice(parameters.Adapter, parameters, parameters.Profile);
        //}


        //public static GraphicsDevice Create(GraphicsAdapter adapter, DeviceCreationFlags deviceCreationFlags,
        //   params FeatureLevel[] featureLevel)
        //{
        //    if (adapter == null)
        //    {
        //        adapter = GraphicsAdapter.Adapters[0];
        //    }
        //    return new GraphicsDevice(adapter, deviceCreationFlags, featureLevel);
        //}

        //public GraphicsDevice CreateDeferred()
        //{
        //    return new GraphicsDevice(this);
        //}


        //public static implicit operator Device(GraphicsDevice graphicsDevice)
        //{
        //    return graphicsDevice.Device;
        //}

        //public static implicit operator Device2(GraphicsDevice graphicsDevice)
        //{
        //    return graphicsDevice.Device;
        //}

        //public static implicit operator SharpDX.DXGI.Device(GraphicsDevice graphicsDevice)
        //{
        //    return graphicsDevice.ToDispose(graphicsDevice.Device.QueryInterface<SharpDX.DXGI.Device>());
        //}

        //public static implicit operator ComObject(GraphicsDevice graphicsDevice)
        //{
        //    return graphicsDevice.Device;
        //}

        //public static implicit operator DeviceContext(GraphicsDevice graphicsDevice)
        //{
        //    return graphicsDevice.Context;
        //}

        //public void SetViewport(Int32 width, Int32 height, Single minDepth = 0.0f, Single maxDepth = 1.0f)
        //{
        //    Context.Rasterizer.SetViewport(new ViewportF(0, 0, width,
        //       height, minDepth, maxDepth));
        //}

        //public void SetViewport(Int32 x, Int32 y, Int32 width, Int32 height, Single minDepth = 0.0f, Single maxDepth = 1.0f)
        //{
        //    Context.Rasterizer.SetViewport(new ViewportF(x, y, width,
        //       height, minDepth, maxDepth));
        //}

        //public void SetViewport(ViewportF viewPort)
        //{
        //    Context.Rasterizer.SetViewport(viewPort);
        //}

        //public void SetViewports(RawViewportF[] viewports, int count = 0)
        //{
        //    Context.Rasterizer.SetViewports(viewports, count);
        //}

        //public void SetRenderTargets(DepthStencilBuffer depthStencilView, RenderTarget2D renderTargetView)
        //{
        //    lock (Context)
        //    {
        //        currentDepthStencil = depthStencilView;
        //        currentRenderTarget = renderTargetView;
        //        Context.OutputMerger.SetRenderTargets(depthStencilView, renderTargetView);
        //    }
        //}

        //public void SetTargets(RenderTarget2D renderTarget)
        //{
        //    lock (Context)
        //    {
        //        currentRenderTarget = renderTarget;
        //        Context.OutputMerger.SetTargets((DepthStencilView)null, renderTarget);
        //    }
        //}

        //public void SetTargets(RenderTarget2D renderTarget, DepthStencilView depthStencil)
        //{
        //    lock (Context)
        //    {
        //        currentRenderTarget = renderTarget;
        //        Context.OutputMerger.SetTargets(depthStencil, renderTarget);
        //    }
        //}

        //public void SetRenderTargets(DepthStencilBuffer depthStencilView, params RenderTarget2D[] renderTargetViews)
        //{
        //    lock (Context)
        //    {
        //        var rtv = new RenderTargetView[renderTargetViews.Length];
        //        rendertargets.Clear();
        //        currentDepthStencil = depthStencilView;
        //        currentRenderTarget = renderTargetViews[0];
        //        for (int i = 0; i < renderTargetViews.Length; i++)
        //        {
        //            rendertargets.Add(renderTargetViews[i]);
        //            rtv[i] = renderTargetViews[i];
        //        }
        //        Context.OutputMerger.SetRenderTargets(depthStencilView, rtv);
        //    }
        //}

        //public void SetRenderTargets(DepthStencilBuffer depthStencilView, params RenderTargetView[] renderTargetViews)
        //{
        //    currentDepthStencil = depthStencilView;
        //    Context.OutputMerger.SetRenderTargets(depthStencilView, renderTargetViews);
        //}

        //public void GetRenderTargets(out DepthStencilBuffer depthStencilView, out RenderTarget2D renderTargetView)
        //{
        //    depthStencilView = currentDepthStencil;
        //    renderTargetView = currentRenderTarget;
        //}

        //public RenderTarget2D GetRenderTarget()
        //{
        //    return currentRenderTarget;
        //}

        //public void ResetTargets()
        //{
        //    Context.OutputMerger.ResetTargets();
        //}

        //public void ClearRenderTarget(RenderTargetView renderTarget, Color color)
        //{
        //    Context.ClearRenderTargetView(renderTarget, color);
        //}

        //public void ClearDepthStencil(DepthStencilView depthView, DepthStencilClearFlags clearOptions, int depthValue, byte stencilValue)
        //{
        //    Context.ClearDepthStencilView(depthView, clearOptions, depthValue, stencilValue);
        //}

        //public void ClearTargets(Color color, params RenderTarget2D[] renderTargetViews)
        //{
        //    for (int i = 0; i < renderTargetViews.Length; i++)
        //    {
        //        Context.ClearRenderTargetView(renderTargetViews[i], color);
        //    }
        //}

        //public void ClearTargets(Color color, DepthStencilBuffer depthStencilView, RenderTarget2D renderTargetView)
        //{
        //    Context.ClearRenderTargetView(renderTargetView, color);
        //    //Context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
        //    //For reversed depth buffer
        //    Context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 0.0f, 0);
        //}

        //public void ClearTargets(Color color, DepthStencilBuffer depthStencilView, params RenderTarget2D[] renderTargetViews)
        //{
        //    for (int i = 0; i < renderTargetViews.Length; i++)
        //    {
        //        Context.ClearRenderTargetView(renderTargetViews[i], color);
        //    }
        //    Context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 0.0f, 0);
        //}

        //public void ClearTargets(Color color, ClearOptions clearOptions = ClearOptions.Both)
        //{
        //    switch (clearOptions)
        //    {
        //        case ClearOptions.Both:
        //            if (currentRenderTarget != null)
        //            {
        //                Context.ClearRenderTargetView(currentRenderTarget, color);
        //            }
        //            if (currentDepthStencil != null)
        //            {
        //                Context.ClearDepthStencilView(currentDepthStencil, DepthStencilClearFlags.Depth, 0.0f, 0);
        //            }

        //            foreach (var rendertarget in rendertargets)
        //            {
        //                Context.ClearRenderTargetView(rendertarget, color);
        //            }
        //            break;
        //        case ClearOptions.DepthBuffer:
        //            Context.ClearDepthStencilView(currentDepthStencil, DepthStencilClearFlags.Depth, 0.0f, 0);
        //            break;
        //        case ClearOptions.RenderTarget:
        //            Context.ClearRenderTargetView(currentRenderTarget, color);
        //            break;
        //    }
        //}


        ///// <summary>
        ///// Returns shared resource from the texture
        ///// </summary>
        ///// <returns></returns>
        //public T GetSharedResource<T>(Resource resource) where T : ComObject
        //{
        //    using (var shared = resource.QueryInterface<SharpDX.DXGI.Resource>())
        //    {
        //        return Device.OpenSharedResource<T>(shared.SharedHandle);
        //    }
        //}

        //public void ReportLiveDeviceObjects(ReportingLevel level)
        //{
        //    debug?.ReportLiveDeviceObjects(level);
        //}

        ///// <summary>
        ///// Resets the stream output targets bound to the StreamOutput stage.
        ///// </summary>
        ///// <msdn-id>ff476484</msdn-id>
        ///// <unmanaged>void ID3D11DeviceContext::SOSetTargets([In] unsigned int NumBuffers,[In, Buffer, Optional] const ID3D11Buffer** ppSOTargets,[In, Buffer, Optional] const unsigned int* pOffsets)</unmanaged>
        ///// <unmanaged-short>ID3D11DeviceContext::SOSetTargets</unmanaged-short>
        //public void ResetStreamOutputTargets()
        //{
        //    Context.StreamOutput.SetTargets(null);
        //}

        ///// <summary>
        ///// Sets the stream output targets bound to the StreamOutput stage.
        ///// </summary>
        ///// <param name="buffer">The buffer to bind on the first stream output slot.</param>
        ///// <param name="offsets">The offsets in bytes of the buffer. An offset of -1 will cause the stream output buffer to be appended, continuing after the last location written to the buffer in a previous stream output pass.</param>
        ///// <msdn-id>ff476484</msdn-id>
        ///// <unmanaged>void ID3D11DeviceContext::SOSetTargets([In] unsigned int NumBuffers,[In, Buffer, Optional] const ID3D11Buffer** ppSOTargets,[In, Buffer, Optional] const unsigned int* pOffsets)</unmanaged>
        ///// <unmanaged-short>ID3D11DeviceContext::SOSetTargets</unmanaged-short>
        //public void SetStreamOutputTarget(Buffer buffer, int offsets = -1)
        //{
        //    Context.StreamOutput.SetTarget(buffer, offsets);
        //}


        ///// <summary>
        ///// Sets the stream output targets bound to the StreamOutput stage.
        ///// </summary>
        ///// <param name="buffers">The buffers.</param>
        ///// <msdn-id>ff476484</msdn-id>	
        ///// <unmanaged>void ID3D11DeviceContext::SOSetTargets([In] unsigned int NumBuffers,[In, Buffer, Optional] const ID3D11Buffer** ppSOTargets,[In, Buffer, Optional] const unsigned int* pOffsets)</unmanaged>	
        ///// <unmanaged-short>ID3D11DeviceContext::SOSetTargets</unmanaged-short>	
        //public void SetStreamOutputTargets(params StreamOutputBufferBinding[] buffers)
        //{
        //    Context.StreamOutput.SetTargets(buffers);
        //}

        //public void SetVertexBuffer(Buffer vertexBuffer, int vertexIndex = 0)
        //{
        //    SetVertexBuffer(0, vertexBuffer, vertexIndex);
        //}

        //public unsafe void SetVertexBuffer(Int32 slot, Buffer vertexBuffer, int vertexIndex = 0)
        //{
        //    IntPtr vertexBufferPtr = ((SharpDX.Direct3D11.Buffer)vertexBuffer).NativePointer;
        //    int stride = vertexBuffer.ElementSize;
        //    int offset = vertexIndex * vertexBuffer.ElementSize;
        //    Context.InputAssembler.SetVertexBuffers(slot, 1, new IntPtr(&vertexBufferPtr), new IntPtr(&stride), new IntPtr(&offset));
        //}

        //public void SetVertexBuffers(Int32 slot, VertexBufferBinding vertexBufferBinding)
        //{
        //    Context.InputAssembler.SetVertexBuffers(slot, vertexBufferBinding);
        //}

        //public void SetVertexBuffers(Int32 firstSlot, params VertexBufferBinding[] vertexBufferBindings)
        //{
        //    Context.InputAssembler.SetVertexBuffers(firstSlot, vertexBufferBindings);
        //}

        //public void SetVertexBuffers(Int32 slot, SharpDX.Direct3D11.Buffer[] vertexBuffers, int[] stridesRef, int[] offsetsRef)
        //{
        //    Context.InputAssembler.SetVertexBuffers(slot, vertexBuffers, stridesRef, offsetsRef);
        //}

        //public void SetVertexBuffers(Int32 slot, int numBuffers, IntPtr verteBufferOut, IntPtr stridesRef, IntPtr offsetsRef)
        //{
        //    Context.InputAssembler.SetVertexBuffers(slot, numBuffers, verteBufferOut, stridesRef, offsetsRef);
        //}

        //public void SetIndexBuffer(Buffer indexBuffer, Boolean is32BitIndices = true, int offset = 0)
        //{
        //    if (is32BitIndices)
        //    {
        //        Context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, offset);
        //    }
        //    else
        //    {
        //        Context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R16_UInt, offset);
        //    }
        //}

        //public void Draw(PrimitiveType primitiveType, Int32 vertexCount, Int32 startVertexLocation = 0)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.Draw(vertexCount, startVertexLocation);
        //}

        //public void DrawAuto()
        //{
        //    SetupInputLayout();
        //    Context.DrawAuto();
        //}

        //public void DrawIndexed(PrimitiveType primitiveType, Int32 indexCount, Int32 startIndexLocation = 0, Int32 startVertexLocation = 0)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.DrawIndexed(indexCount, startIndexLocation, startVertexLocation);
        //}

        //public void DrawIndexedInstanced(PrimitiveType primitiveType, Int32 indexCountPerInstance, Int32 instanceCount, Int32 startIndexLocation, Int32 baseVertexLocation, Int32 startInstanceLocation)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        //}

        //public void DrawIndexedInstancedIndirect(PrimitiveType primitiveType, Buffer bufferForArgsRef, Int32 alignedByteOffsetForArgs)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.DrawIndexedInstancedIndirect(bufferForArgsRef, alignedByteOffsetForArgs);
        //}

        //public void DrawInstanced(PrimitiveType primitiveType, Int32 vertexCountPerInstance, Int32 instanceCount, Int32 startVertexLocation, Int32 startInstanceLocation)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.DrawInstanced(vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
        //}

        //public void DrawInstancedIndirect(PrimitiveType primitiveType, Buffer bufferForArgsRef, Int32 alignedByteOffsetForArgs)
        //{
        //    SetupInputLayout();
        //    Context.InputAssembler.PrimitiveTopology = primitiveType;
        //    Context.DrawInstancedIndirect(bufferForArgsRef, alignedByteOffsetForArgs);
        //}

        //public void ResolveSubresource(Resource source, Int32 sourceSubresource, Resource destination, Int32 destinationSubresource, Format format)
        //{
        //    Context.ResolveSubresource(source, sourceSubresource, destination, destinationSubresource, format);
        //}

        //public DataBox MapSubresource(Buffer resource, MapMode mapMode, MapFlags mapFlags, out DataStream dataStream)
        //{
        //    return Context.MapSubresource(resource, mapMode, mapFlags, out dataStream);
        //}

        //public DataBox MapSubresource(Resource resource, Int32 mipSlice, Int32 arraySlice, MapMode mapMode, MapFlags mapFlags, out Int32 mipSize)
        //{
        //    return Context.MapSubresource(resource, mipSlice, arraySlice, mapMode, mapFlags, out mipSize);
        //}

        //public DataBox MapSubresource(Resource resource, Int32 subresource, MapMode mapMode, MapFlags mapFlags)
        //{

        //    return Context.MapSubresource(resource, subresource, mapMode, mapFlags);
        //}

        //public void CopyResource(Resource source, Resource destination)
        //{
        //    Context.CopyResource(source, destination);
        //}

        //public void CopySubresourceRegion(Resource destination, int dstSubRes, int dstX, int dstY, int dstZ, Resource source, int srcSubRes, ResourceRegion? srcBox = null, int copyFlags = 0)
        //{
        //    Context.CopySubresourceRegion1(destination, dstSubRes, dstX, dstY, dstZ, source, srcSubRes, srcBox, copyFlags);
        //}

        //public T OpenSharedResource<T>(IntPtr resourceHandle) where T : ComObject
        //{
        //    return Device.OpenSharedResource<T>(resourceHandle);
        //}

        //public DataBox MapSubresource(Buffer resource, Int32 subResource, MapMode mapMode, MapFlags mapFlags, out DataStream dataStream)
        //{
        //    return Context.MapSubresource(resource, subResource, mapMode, mapFlags, out dataStream);
        //}

        //public DataBox MapSubresource(Buffer resource, Int32 subResource, MapMode mapMode, MapFlags mapFlags)
        //{
        //    return Context.MapSubresource(resource, subResource, mapMode, mapFlags);
        //}

        //public DataBox MapSubresource(SharpDX.Direct3D11.Texture1D resource, Int32 mipSlice, Int32 arraySlice, MapMode mapMode, MapFlags mapFlags, out DataStream dataStream)
        //{
        //    return Context.MapSubresource(resource, mipSlice, arraySlice, mapMode, mapFlags, out dataStream);
        //}

        //public DataBox MapSubresource(SharpDX.Direct3D11.Texture2D resource, Int32 mipSlice, Int32 arraySlice, MapMode mapMode, MapFlags mapFlags, out DataStream dataStream)
        //{
        //    return Context.MapSubresource(resource, mipSlice, arraySlice, mapMode, mapFlags, out dataStream);
        //}

        //public DataBox MapSubresource(SharpDX.Direct3D11.Texture3D resource, Int32 mipSlice, Int32 arraySlice, MapMode mapMode, MapFlags mapFlags, out DataStream dataStream)
        //{
        //    return Context.MapSubresource(resource, mipSlice, arraySlice, mapMode, mapFlags, out dataStream);
        //}

        //public void UnmapSubresource(Resource resourceRef, Int32 sourceSubresource)
        //{
        //    Context.UnmapSubresource(resourceRef, sourceSubresource);
        //}

        //public void UpdateSubresource(DataBox source, Resource resource, Int32 subresource = 0)
        //{
        //    Context.UpdateSubresource(source, resource, subresource);
        //}

        //public void UpdateSubresource(DataBox source, Resource resource, Int32 subresource, ResourceRegion region)
        //{
        //    Context.UpdateSubresource(source, resource, subresource, region);
        //}

        //public void UpdateSubresource(Resource destinationResourceRef, Int32 destinationResource,
        //   ResourceRegion? destinationBox, IntPtr sourceDataRef, Int32 sourceRowPitch, Int32 sourceDepthPitch)
        //{
        //    Context.UpdateSubresource(destinationResourceRef, destinationResource, destinationBox, sourceDataRef,
        //       sourceRowPitch, sourceDepthPitch);
        //}

        //public void UpdateSubresource<T>(T[] data, Resource resource, Int32 subresource = 0, Int32 rowPitch = 0, Int32 depthPitch = 0,
        //   ResourceRegion? region = null) where T : struct
        //{
        //    Context.UpdateSubresource(data, resource, subresource, rowPitch, depthPitch, region);
        //}

        //public void UpdateSubresource<T>(ref T data, Resource resource, Int32 subresource = 0, Int32 rowPitch = 0, Int32 depthPitch = 0,
        //   ResourceRegion? region = null) where T : struct
        //{
        //    Context.UpdateSubresource(ref data, resource, subresource, rowPitch, depthPitch, region);
        //}

        //public CommandList FinishCommandList(Boolean restoreState = false)
        //{
        //    return Context.FinishCommandList(restoreState);
        //}

        //public void Flush()
        //{
        //    Context.Flush();
        //}

        //public void ExecuteCommandList(CommandList commandList, Boolean restoreContextState = false)
        //{
        //    lock (MainDevice)
        //    {
        //        Context.ExecuteCommandList(commandList, restoreContextState);
        //    }
        //}

        //public void ClearState()
        //{
        //    Context.ClearState();
        //}


        //private void SetupInputLayout()
        //{
        //    if (CurrentPass == null)
        //        throw new InvalidOperationException(
        //           "Cannot perform a Draw/Dispatch operation without an EffectPass applied.");

        //    var inputLayout = CurrentPass.GetInputLayout(_vertexInputLayout);
        //    Context.InputAssembler.InputLayout = inputLayout;
        //}

        public static implicit operator PhysicalDevice (GraphicsDevice device)
        {
            return device.physicalDevice;
        }

        public static implicit operator Device(GraphicsDevice device)
        {
            return device.logicalDevice;
        }
    }
}
