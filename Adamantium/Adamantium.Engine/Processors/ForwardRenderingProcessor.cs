using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.NoiseGenerator;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game;
using Adamantium.Game.GameInput;
using Adamantium.Mathematics;
using Adamantium.Win32;
using AdamantiumVulkan.Core;

//using Buffer = Adamantium.Engine.Graphics.Buffer;
//using Texture2D = Adamantium.Engine.Graphics.Texture2D;
//using Texture3D = Adamantium.Engine.Graphics.Texture3D;

namespace Adamantium.Engine.Processors
{
    public class ForwardRenderingProcessor : RenderProcessor
    {
        private Sampler textureSampler;
        private Texture defaultTexture;
        
        public ForwardRenderingProcessor(EntityWorld world, GameOutput window) : base(world, window)
        {
            //DeferredDevice = GraphicsDevice.CreateDeferred();
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice;
        }

        public override void LoadContent()
        {
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice.CreateDeferred();
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice;
//            SpriteBatch = new SpriteBatch(DeferredDevice, 80000);
            CreateResources();
            CreateSystemResources();
            
        }

        protected override void OnWindowParametersChanging(ChangeReason reason)
        {
            DisposeWindowsResources();
        }

        protected override void OnWindowParametersChanged(ChangeReason reason)
        {
            CreateSystemResources();
        }


        public override void UnloadContent()
        {
//            solidWIreframeEffect?.Dispose();
//            normalsRenderEffect?.Dispose();
//            D2dDevice?.Dispose();
//            fractalEffect?.Dispose();
//            BasicEffect?.Dispose();
//            MarchingCubesEffect?.Dispose();
//            ProtoEffect?.Dispose();
//            base.UnloadContent();
        }

        public sealed override void CreateSystemResources()
        {
            CreateWindowResources();
//            if (DeferredDevice.IsD2dSupportEnabled)
//            {
//                D2dDevice = D2DGraphicDevice.New(DeferredDevice, backBuffer);
//            }
        }

        protected override void OnDeviceChangeEnd()
        {
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice.CreateDeferred();
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice;
            //spriteBatch = new SpriteBatch(DeferredDevice, 25000);
            //CreateResources();
        }

        protected override void OnDeviceChangeBegin()
        {
            //spriteBatch?.Dispose();
            ////DeferredDevice?.Dispose();
            //fractalEffect?.Dispose();
            //mcube?.Dispose();
            //BasicEffect?.Dispose();
            //BasicEffect2?.Dispose();
            //MarchingCubesEffect?.Dispose();
            //ProtoEffect?.Dispose();
        }


//        public Effect solidWIreframeEffect;
//        public Effect fractalEffect;
//        public Effect normalsRenderEffect;
//        private Texture2D pointer;
//        private TextureCube cube;
//        private Effect cubeMapEffect;
//        private Vector2F pointerCenterWhite;
//        private Vector2F pointerCenterBlack;
//        private Vector2F pointerScaleWhite;
//        private Vector2F pointerScaleBlack;
//
//        private Texture2D backBuffer;
//        public Texture2D lookupTexture;
//        private DepthStencilBuffer depthBuffer;
//        private RenderTarget2D renderTarget;
//        private RenderTarget2D fractalrenderTarget;
//        private ViewportF viewPort;
//
//        private Texture2D nullTexture = null;
//        private Texture2D nullTexture2 = null;
//        private Texture2D nullTexture3 = null;
//        private Texture2D nullTexture4 = null;
//        private Texture2D nullTexture5 = null;
//        private Texture2D nullTexture6 = null;
//        private Texture2D nullTexture7 = null;
//        private Effect skybox = null;
//        private Entity skyCube = null;

        // 3D texture containing all the density values for the chunk (block)
//        private Texture3D densityVolume;
//        private Buffer<int> densityIndexBuffer;
//        private Buffer<Vector4F> normalsStreamOutBuffer;
//        private Effect FillDensityEffect;
//        private VertexInputLayout normalsLayout = VertexInputLayout.New<SVPosition>(0);
//        private float wireframeWidthValue = 0.1f;
//        private float patternPeriod = 0.5f;
//        private MarchingCube mcube;
//
//
//
//        private Effect ProtoEffect;
//        private Effect MarchingCubesEffect;
//        private VertexInputLayout Layout;
//        private VertexInputLayout MCLayout = VertexInputLayout.New<VertexPosition>(0);
//        private Vector3F blockOrigin = Vector3F.Zero;
//        private Buffer<Vector3F> vertex;
//        private Buffer<VertexPositionNormalTexture> protoPlainStreamOut;
//        private StreamOutStatisticsQuery streamOutStatisticsQuery;
//        private VertexInputLayout ProtoPlainLayout = VertexInputLayout.New<VertexPositionNormalTexture>(0);
//        private Entity protoPlainEntity;
//        private int voxelsQuantity;
//
//        private SpriteFontData cambriaTrueTypeData;
//        private SpriteFontData cambriaBMFontData;
//
//        private SpriteFont cambriaTrueType;
//        private SpriteFont cambriaBMFont;

        private void CreateProtoPlainGrid(float plainSize, int sqrsInPlain)
        {
            List<Vector3F> positions = new List<Vector3F>();
            var sqrSize = plainSize / sqrsInPlain;

            for (float x = 0; x < plainSize; x += sqrSize)
            {
                for (float z = 0; z < plainSize; z += sqrSize)
                {
                    {
                        positions.Add(new Vector3F(x, 0, z));
                    }
                }
            }

            //vertex = Buffer.Vertex.New(DeferredDevice.Device, positions.ToArray(), ResourceUsage.Dynamic);
        }

        private void CreateVertexGrid(float chunkSize, int voxelsInChunk)
        {
//            this.voxelsQuantity = voxelsInChunk;
//            var voxelSize = chunkSize / voxelsInChunk;
//            //PerlinNoise.SetSeed(123);
//            Stopwatch time = Stopwatch.StartNew();
//            List<Vector3F> positions = new List<Vector3F>();
//            for (float x = blockOrigin.X; x < chunkSize; x += voxelSize)
//            {
//                for (float y = blockOrigin.Y; y < chunkSize; y += voxelSize)
//                {
//                    for (float z = blockOrigin.Z; z < chunkSize; z += voxelSize)
//                    {
//                        {
//                            positions.Add(new Vector3F(x, y, z));
//                        }
//                    }
//                }
//            }
//
//            time.Stop();
//            var elapsed = time.ElapsedMilliseconds;
//            //positions.Clear();
//            //positions.Add(new Vector3F(-1,-1,10));
//            //positions.Add(new Vector3F(-1, 1, 10));
//            //positions.Add(new Vector3F(1, 1, 10));
//            //positions.Add(new Vector3F(1, 1, 10));
//            //positions.Add(new Vector3F(-1, -1, 10));
//            //positions.Add(new Vector3F(1, -1, 10));
//
//            vertex = Buffer.Vertex.New(DeferredDevice.Device, positions.ToArray(), ResourceUsage.Dynamic);
//            //streamOut = Buffer.New<Vector3F>(DeferredDevice.Device, positions.Count *15, BufferFlags.StreamOutput);
//            FillDecals(voxelSize);
        }

        private void CreateLookUpTexture2D()
        {
//            GCHandle handle = GCHandle.Alloc(MarchingCubes.TrianglesTable, GCHandleType.Pinned);
//            try
//            {
//                var ptr = handle.AddrOfPinnedObject();
//                DataBox box = new DataBox(ptr, 16 * sizeof(int), 0);
//                lookupTexture = Texture2D.New(DeferredDevice, box, 16, 256, SurfaceFormat.R32.SInt);
//            }
//            finally
//            {
//                handle.Free();
//            }
        }

        private void FillDecals(float voxelSize)
        {
            decals = new Vector3F[8];
            //decals[0] = new Vector3F(0, 0, 0);
            //decals[1] = new Vector3F(voxelSize, 0, 0);
            //decals[2] = new Vector3F(voxelSize, voxelSize, 0);
            //decals[3] = new Vector3F(0, voxelSize, 0);
            //decals[4] = new Vector3F(0, 0, voxelSize);
            //decals[5] = new Vector3F(voxelSize, 0, voxelSize);
            //decals[6] = new Vector3F(voxelSize, voxelSize, voxelSize);
            //decals[7] = new Vector3F(0, voxelSize, voxelSize);

            decals[0] = new Vector3F(0, 0, voxelSize);
            decals[1] = new Vector3F(voxelSize, 0, voxelSize);
            decals[2] = new Vector3F(voxelSize, 0, 0);
            decals[3] = new Vector3F(0, 0, 0);
            decals[4] = new Vector3F(0, voxelSize, voxelSize);
            decals[5] = new Vector3F(voxelSize, voxelSize, voxelSize);
            decals[6] = new Vector3F(voxelSize, voxelSize, 0);
            decals[7] = new Vector3F(0, voxelSize, 0);
        }
        
        Sampler CreateTextureSampler()
        {
            SamplerCreateInfo samplerInfo = new SamplerCreateInfo();
            samplerInfo.MagFilter = Filter.Linear;
            samplerInfo.MinFilter = Filter.Linear;
            samplerInfo.AddressModeU = SamplerAddressMode.Repeat;
            samplerInfo.AddressModeV = SamplerAddressMode.Repeat;
            samplerInfo.AddressModeW = SamplerAddressMode.Repeat;
            samplerInfo.AnisotropyEnable = true;
            samplerInfo.MaxAnisotropy = 16;
            samplerInfo.BorderColor = BorderColor.IntOpaqueWhite;
            samplerInfo.UnnormalizedCoordinates = false;
            samplerInfo.CompareEnable = false;
            samplerInfo.CompareOp = CompareOp.Always;
            samplerInfo.MipmapMode = SamplerMipmapMode.Linear;

            return GraphicsDevice.CreateSampler(samplerInfo);
        }

        private void CreateResources()
        {
            textureSampler = CreateTextureSampler();
            //defaultTexture = Texture.Load(GraphicsDevice, Path.Combine("Textures", "texture.png"));
            defaultTexture = Texture.Load(GraphicsDevice, Path.Combine("Textures", "sdf.png"));
            //DeferredDevice.SetTargets(null);

            //var vbFlags = BufferFlags.VertexBuffer | BufferFlags.StreamOutput;
            //normalsStreamOutBuffer = Buffer.New<Vector4F>(DeferredDevice.Device, 1000000, vbFlags);
            //cambriaTrueTypeData = Content.Load<SpriteFontData>("Font/cambria.aefnt");
            //cambriaBMFontData = Content.Load<SpriteFontData>("Font/BM_Cambria/cambria_bm.aefnt");

            //cambriaTrueType = SpriteFont.New(DeferredDevice, cambriaTrueTypeData);
            //cambriaBMFont = SpriteFont.New(DeferredDevice, cambriaBMFontData);

            //CreateProtoPlainGrid(10.0f, 10);
            //streamOutStatisticsQuery = new StreamOutStatisticsQuery(DeferredDevice);
            //protoPlainStreamOut = Buffer.New<VertexPositionNormalTexture>(DeferredDevice.Device,
            //                  vertex.ElementCount * 6,
            //                  BufferFlags.StreamOutput | BufferFlags.VertexBuffer);

            //protoPlainEntity = EntityWorld.CreateEntity("ProtoPlainEntity", null);

            //CreateVertexGrid(3f, 16);
            //CreateLookUpTexture2D();
            //var tex = game.Content.Load<Texture2D>("Textures/1.jpg");
            //tex.Save("tgaTestSaving.tga", ImageFileType.Tga);
            //tex.Dispose();

            //nullTexture = game.Content.Load<Texture2D>("Textures/tgatest.tga");
            //nullTexture = game.Content.Load<Texture2D>("Textures/2RLEExpand.tga");
            //nullTexture = game.Content.Load<Texture2D>("Textures/TestExpand.tga");
            //nullTexture2 = Content.Load<Texture2D>("Textures/luxfon.jpg");
            //nullTexture = game.Content.Load<Texture2D>("Textures/BaseAlbedoTextureRLE.tga");
//            nullTexture = Content.Load<Texture2D>("Textures/BaseAlbedoTexture_Text.png");
//            nullTexture3 = Content.Load<Texture2D>("Textures/BaseAlbedoTexture16.tga");
//            nullTexture4 = Content.Load<Texture2D>("Textures/UntitledRLE.tga");
//            nullTexture5 = Content.Load<Texture2D>("Textures/1.jpg");
//            nullTexture6 = Content.Load<Texture2D>("Textures/19093.jpg");
//            pointer = Content.Load<Texture2D>("Textures/circle.png");
            //cube = game.Content.Load<TextureCube>("Textures/CubeTest.dds");
            //cube = (TextureCube)Texture.Load(DeferredDevice, new[]
            //{
            //   "Content/Textures/skybox/skyblue_c00.tga", "Content/Textures/skybox/skyblue_c01.tga", "Content/Textures/skybox/skyblue_c02.tga",
            //   "Content/Textures/skybox/skyblue_c03.tga", "Content/Textures/skybox/skyblue_c04.tga", "Content/Textures/skybox/skyblue_c05.tga"
            //});

//            pointerScaleWhite = new Vector2F(0.25f);
//            pointerScaleBlack = new Vector2F(0.35f);
//            pointerCenterWhite = new Vector2F(pointer.Width / 2.0f * pointerScaleWhite.X, pointer.Height / 2.0f * pointerScaleWhite.Y);
//            pointerCenterBlack = new Vector2F(pointer.Width / 2.0f * pointerScaleBlack.X,
//               pointer.Height / 2.0f * pointerScaleBlack.Y);

            //cubeMapEffect = Asset.Load<Effect>("Effects/SkyboxEffect.fx");

            //skybox = Asset.Load<Effect>("Effects/SkyboxEffect.fx");

            //var result = EffectData.Load(@"Content\Effects\Fractal.fx.compiled");
            //fractalEffect = new Effect(DeferredDevice, result);
            //result = EffectData.Load(@"Content\Effects\SkyboxEffect.fx.compiled");
            //skybox = new Effect(DeferredDevice, result);
            //skyCube = ShapesGenerator.CreateCubeSphere(DeferredDevice.Device, entityWorld, 1, 30);
            //skyCube.IsVisible = false;
            //var move = new MoveToolTemplate(DeferredDevice, 10,0.1f,Vector3F.One, true);
            //move.BuildEntity(EntityWorld, null, "");

            //mcube = new MarchingCube(DeferredDevice, entityWorld);

            //result = EffectData.Load(@"Content\Effects\Debug\SolidWireframe.fx.compiled");
            //solidWIreframeEffect = new Effect(DeferredDevice, result);

            //result = EffectData.Load(@"Content\Effects\Debug\NormalsRendering.fx.compiled");
            //normalsRenderEffect = new Effect(DeferredDevice, result);

            //var fx = EffectData.Load(@"Content\Effects\TerrainGenShaders\MarchingCubes.fx.compiled");
            //MarchingCubesEffect = new Effect(DeferredDevice, fx);

            //var proto_fx = EffectData.Load(@"Content\Effects\TerrainGenShaders\Prototype.fx.compiled");
            //ProtoEffect = new Effect(DeferredDevice.Device, proto_fx);
        }

        private void CreateWindowResources()
        {
//            var desc = Window.GetDescription();
//            backBuffer = Texture2D.New(DeferredDevice, desc);
//
//            // Renderview on the backbuffer
//            renderTarget = RenderTarget2D.New(DeferredDevice, backBuffer);
//            fractalrenderTarget = RenderTarget2D.New(DeferredDevice, desc);
//
//            // Create the depth buffer and depth buffer view
//            depthBuffer = DepthStencilBuffer.New(DeferredDevice, new Texture2DDescription()
//            {
//                Format = Format.D32_Float_S8X24_UInt,
//                ArraySize = 1,
//                MipLevels = 1,
//                Width = Window.Width,
//                Height = Window.Height,
//                SampleDescription = new SampleDescription((Int32)Window.MSAALevel, 0),
//                Usage = ResourceUsage.Default,
//                BindFlags = BindFlags.DepthStencil,
//                CpuAccessFlags = CpuAccessFlags.None,
//                OptionFlags = ResourceOptionFlags.None
//            });
//
//            viewPort = new ViewportF(0.0f, 0.0f, Window.Width, Window.Height, 0.0f, 1.0f);

            CreateLookUpTexture2D();
        }

        private void DisposeWindowsResources()
        {
//            D2dDevice?.Dispose();
//            backBuffer?.Dispose();
//            depthBuffer?.Dispose();
//            renderTarget?.Dispose();
//            fractalrenderTarget?.Dispose();
//            mcube?.Dispose();
//            lookupTexture?.Dispose();
        }

        public override bool BeginDraw()
        {
            if (!Window.IsUpToDate()) return false;

            if (base.BeginDraw())
            {
                if (GraphicsDevice.BeginDraw(1.0f, 0))
                {
                    GraphicsDevice.SetViewports(Window.Viewport);
                    GraphicsDevice.SetScissors(Window.Scissor);
                    return true;
                }

                return false;
            }

            return false;
        }

        public override void Draw(IGameTime gameTime)
        {
            base.Draw(gameTime);
            GraphicsDevice.SetViewports(Window.Viewport);
            GraphicsDevice.SetScissors(Window.Scissor);
            //GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
            if (ActiveCamera == null)
            {
                return;
            }
            
            foreach (var entity in Entities)
            {
                try
                {
                    entity.TraverseInDepth(
                        current =>
                        {
                            if (!current.Visible)
                            {
                                return;
                            }

                            
                            ContainmentType intersects = ContainmentType.Contains;
                            var collision = current.GetComponent<Collider>();
                            if (collision != null)
                            {
                                if (collision.ContainsDataFor(ActiveCamera))
                                {
                                    //var transform = current.Transform.GetMetadata(ActiveCamera);
                                    //intersects = collision.IsInsideCameraFrustum(ActiveCamera);
                                    //var wvp = transform.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                                    // BasicEffect.Parameters["worldMatrix"].SetValue(transform.WorldMatrix);
                                    // BasicEffect.Parameters["wvp"].SetValue(wvp);
                                    // BasicEffect.Parameters["worldInverseTransposeMatrix"].SetValue(Matrix4x4F.Transpose(Matrix4x4F.Invert(transform.WorldMatrix)));
                                    // BasicEffect.Parameters["meshColor"].SetValue(Colors.White.ToVector3());
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
                                    //collision.Draw(DeferredDevice, ActiveCamera);
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
                                }
                            }

                            //if (intersects != ContainmentType.Disjoint)
                            {
                                var controller = current.GetComponent<AnimationController>();
                                if (controller != null && controller.FinalMatrices.Count > 0)
                                {
                                    var arr = controller.FinalMatrices.Values.ToArray();
                                    //BasicEffect.Parameters["Bones"].SetValue(arr);
                                }

                                var renderers = current.GetComponents<MeshRendererBase>();

                                var transformation = current.Transform.GetMetadata(ActiveCamera);
                                if (!transformation.Enabled)
                                {
                                    return;
                                }

                                if (renderers.Length > 0)
                                {
                                    foreach (var component in renderers)
                                    {
                                        var material = current.GetComponent<Material>();
                                        var wvp = transformation.WorldMatrix *ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                                        //var orthoProj = Matrix4x4F.OrthoOffCenter(0, Window.Width, 0, Window.Height, 1f, 100000f);
                                        //var wvp = transformation.WorldMatrix * ActiveCamera.UiProjection;
                                        //var wvp = transformation.WorldMatrix * Matrix4x4F.Scaling(1, -1, 1) * Matrix4x4F.Scaling(2.0f / Window.Width, 2.0f/Window.Height, 1.0f / (100000f - 1f));
                                        GraphicsDevice.BasicEffect.Parameters["wvp"].SetValue(wvp);
                                        GraphicsDevice.BasicEffect.Parameters["meshColor"].SetValue(Colors.Black.ToVector3());
                                        //GraphicsDevice.BasicEffect.Parameters["transparency"].SetValue(0.5f);
                                        //GraphicsDevice.BasicEffect.Parameters["worldMatrix"].SetValue(transformation.WorldMatrix);
                                        //GraphicsDevice.BasicEffec.SetValue(Matrix4x4F.Transpose(Matrix4x4F.Invert(transformation.WorldMatrix)));
                                        
                                        //DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.Default;
                                        //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.CullBackClipDisabled);
                                        
                                        GraphicsDevice.BasicEffect.Parameters["sampleType"].SetResource(textureSampler);

                                        if (material?.Texture != null)
                                        {
                                            GraphicsDevice.BasicEffect.Parameters["shaderTexture"].SetResource(material.Texture);
                                        }
                                        else
                                        {
                                            GraphicsDevice.BasicEffect.Parameters["shaderTexture"].SetResource(defaultTexture);
                                        }

                                        if (component is SkinnedMeshRenderer)
                                        {
                                            GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["Skinned"].Apply();
                                            component.Draw(GraphicsDevice, gameTime);
                                        }

                                        if (component is MeshRenderer)
                                        {
                                            
                                            //GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["Colored"].Apply();
                                            GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["Colored2"].Apply();
                                            //GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["MSDF"].Apply();
                                            //GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["SDF"].Apply();

                                            //BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].Apply();
                                            component.Draw(GraphicsDevice, gameTime);
                                            //BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].UnApply(true);
                                        }

                                        //drawingCounts++;


                                        /*
                                        DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
                                        //DeferredDevice.SetBlendState(DeferredDevice.BlendStates.NonPremultiplied);

                                        
                                        solidWIreframeEffect.Parameters["World"].SetValue(transformation.WorldMatrix);
                                        solidWIreframeEffect.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["WorldView"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["WorldViewProjection"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["Viewport"].SetValue(new Vector4F(Window.Viewport.Width, Window.Viewport.Height, Window.Viewport.X, Window.Viewport.Y));
                                        solidWIreframeEffect.Parameters["PatternPeriod"].SetValue(patternPeriod);
                                        solidWIreframeEffect.Parameters["LineWidth"].SetValue(wireframeWidthValue);

                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].Apply();
                                        component.Draw(GraphicsDevice, gameTime);
                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].UnApply(true);
                                        */
                                    }
                                }
                            }
                        });
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }

//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;

            /*
            Text += "Camera view direction = " + ActiveCamera.Forward + "\n";
            Text += "Offset: " + ActiveCamera.GetOwnerPosition() + "\n";
            Text += "Velocity: " + ActiveCamera.Velocity + "\n";
            Text += "DragVelocity: " + ActiveCamera.DragVelocity + "\n";
            Text += "Mouse sensitivity: " + ActiveCamera.MouseSensitivity + "\n";
            Text += "Keyboard rotation speed: " + ActiveCamera.RotationSpeed + "\n";
            Text += "Camera type: " + ActiveCamera.Type + "\n";
            Text += ToolsService.Text;

            Text += wireframeWidthValue + "\n";
            Text += patternPeriod + "\n";


            if (InputService.IsKeyDown(Keys.Digit1))
            {
                wireframeWidthValue += 0.1f;
            }
            if (InputService.IsKeyDown(Keys.Digit2))
            {
                wireframeWidthValue -= 0.1f;
                if (wireframeWidthValue < 0)
                {
                    wireframeWidthValue = 0;
                }
            }
            if (InputService.IsKeyDown(Keys.Digit3))
            {
                patternPeriod += 0.1f;
            }
            if (InputService.IsKeyDown(Keys.Digit4))
            {
                patternPeriod -= 0.1f;
                if (patternPeriod < 0)
                {
                    patternPeriod = 0;
                }
            }

            //DeferredDevice.SetDepthStencilState(DeferredDevice.DepthStencilStates.DepthDisable);
            //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.Default);

            //skybox.Parameters["WVP"].SetValue(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
            //skybox.Parameters["skyboxTexture"].SetResource(cube);
            //skybox.Parameters["skyBoxSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);

            //skybox.Techniques[0].Passes[0].Apply();
            //var celestialGeometry = skyCube.GetComponent<CelestialBodyGeometry>();
            //celestialGeometry.Draw(DeferredDevice);

            //DeferredDevice.SetDepthStencilState(DeferredDevice.DepthStencilStates.DepthEnable);

            BasicEffect.Parameters["viewMatrix"].SetValue(ActiveCamera.ViewMatrix);
            BasicEffect.Parameters["projectionMatrix"].SetValue(ActiveCamera.ProjectionMatrix);
            BasicEffect.Parameters["sampleType"].SetResource(DeferredDevice.SamplersStates.AnisotropicWrap);

            BasicEffect.Parameters["LightPosition"].SetValue(new Vector3F(0, 1000, 0));
            BasicEffect.Parameters["globalAmbient"].SetValue(new Vector4F(0.5f, 0.5f, 0.5f, 1.0f));


            BasicEffect.Parameters["cameraPos"].SetValue(Vector3F.Zero);


            BasicEffect.Parameters["LightDirection"].SetValue(Vector3F.Down);
            BasicEffect.Parameters["LightColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightDiffuseColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightSpecularColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightAmbientColor"].SetValue(new Vector4F(0.5f, 0.5f, 0.5f,
               0.2f));

            BasicEffect.Parameters["MaterialDiffuseColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["MaterialSpecularColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["MaterialAmbientColor"].SetValue(new Vector4F(0.2f, 0.2f, 0.2f, 0.2f));
            BasicEffect.Parameters["Shininess"].SetValue(1.2f);
            

            int drawingCounts = 0;
            int verticesCount = 0;
            foreach (var entity in Entities)
            {
                try
                {
                    entity.TraverseInDepth(
                        current =>
                        {
                            if (!current.Visible)
                            {
                                return;
                            }

                            ContainmentType intersects = ContainmentType.Contains;
                            var collision = current.GetComponent<Collider>();
                            if (collision != null)
                            {
                                if (collision.ContainsDataFor(ActiveCamera))
                                {
                                    var transform = current.Transform.GetMetadata(ActiveCamera);
                                    intersects = collision.IsInsideCameraFrustum(ActiveCamera);
                                    var wvp = transform.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                                    BasicEffect.Parameters["worldMatrix"].SetValue(transform.WorldMatrix);
                                    BasicEffect.Parameters["wvp"].SetValue(wvp);
                                    BasicEffect.Parameters["worldInverseTransposeMatrix"].SetValue(Matrix4x4F.Transpose(Matrix4x4F.Invert(transform.WorldMatrix)));
                                    BasicEffect.Parameters["meshColor"].SetValue(Colors.White.ToVector3());
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
                                    //collision.Draw(DeferredDevice, ActiveCamera);
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
                                }
                            }

                            if (intersects != ContainmentType.Disjoint)
                            {
                                var controller = current.GetComponent<AnimationController>();
                                if (controller != null && controller.FinalMatrices.Count > 0)
                                {
                                    var arr = controller.FinalMatrices.Values.ToArray();
                                    BasicEffect.Parameters["Bones"].SetValue(arr);
                                }

                                var renderers = current.GetComponents<MeshRendererBase>();

                                var transformation = current.Transform.GetMetadata(ActiveCamera);
                                if (!transformation.Enabled)
                                {
                                    return;
                                }

                                if (renderers.Length > 0)
                                {
                                    foreach (var component in renderers)
                                    {
                                        var material = current.GetComponent<Material>();
                                        BasicEffect.Parameters["worldMatrix"].SetValue(transformation.WorldMatrix);
                                        BasicEffect.Parameters["wvp"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                                        BasicEffect.Parameters["worldInverseTransposeMatrix"]
                                            .SetValue(
                                                Matrix4x4F.Transpose(Matrix4x4F.Invert(transformation.WorldMatrix)));


                                        DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.Default;
                                        //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.CullBackClipDisabled);

                                        if (material?.Texture != null)
                                        {
                                            BasicEffect.Parameters["shaderTexture"].SetResource(material.Texture);
                                            BasicEffect.Parameters["texturePresent"].SetValue(true);
                                        }
                                        else
                                        {
                                            BasicEffect.Parameters["texturePresent"].SetValue(false);
                                            BasicEffect.Parameters["shaderTexture"].SetResource(nullTexture);
                                        }


                                        if (component is SkinnedMeshRenderer)
                                        {
                                            BasicEffect.Techniques["MeshVertex"].Passes["Skinned"].Apply();
                                            component.Draw(DeferredDevice, gameTime);
                                            BasicEffect.Techniques["MeshVertex"].Passes["Skinned"].UnApply(true);
                                        }

                                        if (component is MeshRenderer)
                                        {
                                            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].Apply();
                                            component.Draw(DeferredDevice, gameTime);
                                            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].UnApply(true);
                                        }


                                        drawingCounts++;


                                        DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
                                        //DeferredDevice.SetBlendState(DeferredDevice.BlendStates.NonPremultiplied);

                                        /*
                                        solidWIreframeEffect.Parameters["World"].SetValue(transformation.WorldMatrix);
                                        solidWIreframeEffect.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["WorldView"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["WorldViewProjection"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["Viewport"].SetValue(new Vector4F(Window.Viewport.Width, Window.Viewport.Height, Window.Viewport.X, Window.Viewport.Y));
                                        solidWIreframeEffect.Parameters["PatternPeriod"].SetValue(patternPeriod);
                                        solidWIreframeEffect.Parameters["LineWidth"].SetValue(wireframeWidthValue);

                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].Apply();
                                        component.Draw(DeferredDevice, gameTime);
                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].UnApply(true);
                                        
                                    }
                                }
                            }
                        });
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }
            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].UnApply(true);
            DrawLightIcons();
            DrawCameraIcons();
            DrawAdditionalStuff();
*/

            //DrawFractal();
            /*
            if (InputService.IsKeyPressed(Keys.B) && InputService.IsKeyPressed(Keys.Control))
            {

                var rootEntity = EntityWorld.CreateEntity("Root Perlin", null);
                for (int k = 0; k < 3; ++k)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        for (int j = 0; j < 3; ++j)
                        {

                            Buffer<VertexPositionNormalTexture> streamOut =
                               Buffer.New<VertexPositionNormalTexture>(DeferredDevice.Device,
                                  voxelsQuantity * voxelsQuantity * voxelsQuantity * 15,
                                  BufferFlags.StreamOutput | BufferFlags.VertexBuffer);
                            DeferredDevice.SetVertexBuffer(vertex);
                            DeferredDevice.SetVertexInputLayout(MCLayout);
                            DeferredDevice.SetStreamOutputTarget(streamOut, 0);

                            //Matrix4x4F wvp = Matrix4x4F.Identity * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                            //MarchingCubesEffect.Parameters["WVP"].SetValue(wvp);
                            //MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);

                            MarchingCubesEffect.Parameters["permutationTable"].SetValue(PerlinNoise.permutationTable);
                            MarchingCubesEffect.Parameters["gradientSet"].SetValue(PerlinNoise.gradientSet.ToArray());
                            MarchingCubesEffect.Parameters["mX"].SetValue(PerlinNoise.mX);
                            MarchingCubesEffect.Parameters["mY"].SetValue(PerlinNoise.mY);
                            MarchingCubesEffect.Parameters["mZ"].SetValue(PerlinNoise.mZ);

                            //MC params
                            MarchingCubesEffect.Parameters["EdgeTable"].SetValue(MarchingCubes.EdgeTable);
                            MarchingCubesEffect.Parameters["tritableTex"].SetResource(lookupTexture);
                            MarchingCubesEffect.Parameters["decal"].SetValue(decals);
                            MarchingCubesEffect.Parameters["isolevel"].SetValue(0.0f);
                            MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);


                            Stopwatch timer = Stopwatch.StartNew();
                            MarchingCubesEffect.Techniques[0].Passes[0].Apply();
                            DeferredDevice.Draw(PrimitiveType.PointList, vertex.ElementCount);
                            MarchingCubesEffect.Techniques[0].Passes[0].UnApply(true);
                            timer.Stop();
                            var ms = timer.ElapsedMilliseconds;
                            //var v = MarchingCubesEffect.Parameters["ContainsTriangles"].GetValue<Vector3F>();
                            string name = "Perlin MC";
                            var entity = EntityWorld.CreateEntity(name, null);

                            //var data = streamOut.GetData();
                            //var mesh = new SkinnedVertex[data.Length];
                            //for (int i = 0; i < data.Length; ++i)
                            //{
                            //   mesh[i] = new SkinnedVertex()
                            //   {
                            //      Position = data[i]
                            //   };
                            //}
                            //Buffer<SkinnedVertex> skinned = Buffer.Vertex.New(DeferredDevice.Device, mesh);
                            //var skinnedGeom = new SkinnedGeometry(skinned, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(skinnedGeom);
                            //entity.AddComponent(new Collision(data));

                            //var geometry = new CelestialBodyGeometry(streamOut, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(geometry);

                            blockOrigin.X += 0.5f;


                        }
                        blockOrigin.Z += 0.5f;
                        blockOrigin.X = 0.0f;
                    }
                    blockOrigin.Y += 0.5f;
                }
                blockOrigin.Y = 0;



                /*
                PerlinNoiseManager noiseManager = new PerlinNoiseManager();
                List<Vector3F> vertexList = new List<Vector3F>();

                vertexList = noiseManager.GenerateBlockOfChunks(new Vector3F(-1.0f, -1.0f, -1.0f), 3, 3.0f, 10);

                if (vertexList.Count > 0)
                {
                   SkinnedVertex[] vertexArray = new SkinnedVertex[vertexList.Count];

                   for (int i = 0; i < vertexList.Count; ++i)
                   {
                      vertexArray[i].Position = vertexList[i];
                      vertexArray[i].Normal = Vector3F.Normalize(vertexArray[i].Position);
                   }

                   Buffer<SkinnedVertex> vertexBuf = Buffer.Vertex.New(DeferredDevice.Device, vertexArray);

                   Entity marchingCubesEntity = EntityWorld.CreateEntity(null, "Marching Cube");
                   marchingCubesEntity.AddComponent(new SkinnedGeometry(vertexBuf, null, PrimitiveType.TriangleList));
                   marchingCubesEntity.AddComponent(new Collision(BoundingOrientedBox.FromPoints(vertexList.ToArray())));
                   }
                  */
                //Perlin
                //mcube.Draw(0.5f, 16, 0, 3);
            /*}
            if (InputService.IsKeyPressed(Keys.N) && InputService.IsKeyPressed(Keys.Control))
            {
                var rootEntity = EntityWorld.CreateEntity("Root Simplex");
                //Simplex
                for (int z = 0; z < 3; ++z)
                {
                    for (int k = 0; k < 3; ++k)
                    {
                        for (int j = 0; j < 3; ++j)
                        {
                            Buffer<VertexPositionNormalTexture> streamOut =
                               Buffer.New<VertexPositionNormalTexture>(DeferredDevice.Device,
                                  voxelsQuantity * voxelsQuantity * voxelsQuantity * 15,
                                  BufferFlags.StreamOutput | BufferFlags.VertexBuffer);

                            DeferredDevice.Device.SetVertexBuffer(vertex);
                            DeferredDevice.Device.SetVertexInputLayout(MCLayout);
                            DeferredDevice.Device.SetStreamOutputTarget(streamOut, 0);

                            //Open simplex params
                            MarchingCubesEffect.Parameters["perm"].SetValue(noise.perm);
                            MarchingCubesEffect.Parameters["permGradIndex3D"].SetValue(noise.perm3D);
                            MarchingCubesEffect.Parameters["Gradients3D"].SetValue(OpenSimplexHLSLpreparation.Gradients3D);

                            //MC params
                            MarchingCubesEffect.Parameters["EdgeTable"].SetValue(MarchingCubes.EdgeTable);
                            MarchingCubesEffect.Parameters["tritableTex"].SetResource(lookupTexture);
                            MarchingCubesEffect.Parameters["decal"].SetValue(decals);
                            MarchingCubesEffect.Parameters["isolevel"].SetValue(0.0f);
                            MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);


                            Stopwatch timer = Stopwatch.StartNew();
                            MarchingCubesEffect.Techniques[1].Passes[0].Apply();
                            DeferredDevice.Device.Draw(PrimitiveType.PointList, vertex.ElementCount);
                            MarchingCubesEffect.Techniques[1].Passes[0].UnApply(true);
                            timer.Stop();
                            var ms = timer.ElapsedMilliseconds;
                            //var v = MarchingCubesEffect.Parameters["ContainsTriangles"].GetValue<Vector3F>();
                            string name = "Simplex MC";
                            var entity = EntityWorld.CreateEntity(name, rootEntity);
                            //var data = streamOut.GetData();
                            //var mesh = new SkinnedVertex[data.Length];
                            //for (int i = 0; i < data.Length; ++i)
                            //{
                            //   mesh[i] = new SkinnedVertex()
                            //   {
                            //      Position = data[i]
                            //   };
                            //}
                            //Buffer<SkinnedVertex> skinned = Buffer.Vertex.New(DeferredDevice.Device, mesh);
                            //var skinnedGeom = new SkinnedGeometry(skinned, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(skinnedGeom);
                            //entity.AddComponent(new Collision(data));

                            //var geometry = new CelestialBodyGeometry(streamOut, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(geometry);

                            blockOrigin.X += 0.5f;
                        }
                        blockOrigin.Z += 0.5f;
                        blockOrigin.X = 0f;
                    }
                    blockOrigin.Y += 0.5f;
                }
                blockOrigin.Y = 0;
                //mcube.Draw(0.5f, 16, 1, 3);
            }
            */
            // Precalculate some constants
//            int textureHalfSize = pointer.Width / 2;
//            int spriteSceneWidth = backBuffer.Width / 2;
//            int spriteSceneHeight = backBuffer.Height / 2;
//            int spriteSceneRadiusWidth = backBuffer.Width / 2 - textureHalfSize;
//            int spriteSceneRadiusHeight = backBuffer.Height / 2 - textureHalfSize;
//
//            // Time used to animate the balls
//            var time = (float)gameTime.TotalTime.TotalSeconds;
//
//            // Draw sprites on the screen
//            var random = new Random(0);
//            const int SpriteCount = 50000;
//
//            if (InputService.IsKeyPressed(Keys.F6))
//            {
//                mode = SpriteSortMode.NoSort;
//            }
//            if (InputService.IsKeyPressed(Keys.F7))
//            {
//                mode = SpriteSortMode.BackToFront;
//            }
//            if (InputService.IsKeyPressed(Keys.F8))
//            {
//                mode = SpriteSortMode.FrontToBack;
//            }
//            if (InputService.IsKeyPressed(Keys.F9))
//            {
//                mode = SpriteSortMode.Texture;
//            }
            /*
            //spriteBatch.Begin(SpriteSortMode.NoSort, GraphicsDeviceService.GraphicsDevice.BlendStates.NonPremultiplied);
            spriteBatch.Begin(mode, GraphicsDeviceService.GraphicsDevice.BlendStates.NonPremultiplied, null, GraphicsDevice.DepthStencilStates.DepthDisable);

            for (int i = 0; i < SpriteCount; i++)
            {
               var angleOffset = (float)random.NextDouble() * Math.PI * 2.0f;
               var radiusOffset = (float)random.NextDouble() * 0.8f + 0.2f;
               var spriteSpeed = (float)random.NextDouble() + 0.1f;

               var position = new Vector2F
               {
                  X = spriteSceneWidth + spriteSceneRadiusWidth * radiusOffset * (float)Math.Cos(time * spriteSpeed + angleOffset),
                  Y = spriteSceneHeight + spriteSceneRadiusHeight * radiusOffset * (float)Math.Sin(time * spriteSpeed + angleOffset)
               };

               spriteBatch.Draw(pointer, position, null, Color.Yellow, 0.0f, Vector2F.Zero, Vector2F.One);
            }

           spriteBatch.DrawString(cambriaBMFont, "Hello world", new Vector2F(300, 50), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.None, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 100), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.None, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 200), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipHorizontally, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 300), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipVertically, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 400), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipBoth, 0);

               rotation += 1 * (float)GameTime.FrameTime;
            //spriteBatch.Draw(pointer, mouseState.Position - pointerCenterBlack, Color.Red, pointerScaleBlack);
            spriteBatch.Draw(nullTexture6, new RectangleF(0, 0, 300, 300), null, Color.RosyBrown);
            spriteBatch.Draw(nullTexture5, new RectangleF(1050, 100, 300, 300), null, Color.White, rotation, new Vector2F(300, 300), SpriteEffects.None, 0.1f);
            spriteBatch.Draw(nullTexture2, new RectangleF(250, 100, 300, 300), null, Color.White, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.2f);
            spriteBatch.Draw(new RectangleF(550, 100, 300, 300), Color.Green, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.3f);
            spriteBatch.Draw(nullTexture4, new RectangleF(650, 100, 300, 300), null, Color.White, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.4f);
            spriteBatch.Draw(nullTexture4, new RectangleF(1100, 150, 10, 10), null, Color.White, 0f, new Vector2F(150, 150));
            spriteBatch.Draw(nullTexture3, new RectangleF(750, 100, 300, 300), null, Color.Gray, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.5f);

            spriteBatch.End();
            */
            /*
            var state = InputService.GetGamepadState(0);
            
            Text += "Rendered meshes count: " + drawingCounts + "\n";
            Text += "Rendered vertices count: " + verticesCount + "\n";
            Text += "Rendered triangles count: " + verticesCount / 3 + "\n";
            Text += "Real position: " + InputService.AbsolutePosition + "\n";
            Text += "Relative position: " + InputService.RelativePosition + "\n";
            Text += "Virtual position: " + InputService.VirtualPosition + "\n";
            Text += "Left thumb: " + state.LeftThumb + "\n";
            Text += "Right thumb: " + state.RightThumb + "\n";
            Text += "Left trigger: " + state.LeftTrigger + "\n";
            Text += "Right trigger: " + state.RightTrigger + "\n";
            base.Draw(gameTime);
            */
        }
        

        float rotation = 0;

//        SpriteSortMode mode = SpriteSortMode.NoSort;
        private OpenSimplexNoise noise = new OpenSimplexNoise(144);
        private float zoom = 3;
        private Vector2F pan = new Vector2F(0.0f, 0);
        private Vector2F seed = new Vector2F(0.39f, -0.2f);
        public Vector3F[] decals;

        private void DrawFractal()
        {
            if (InputService.IsKeyDown(Keys.Add))
            {
                zoom -= 0.001f;
            }
            if (InputService.IsKeyDown(Keys.Subtract))
            {
                zoom += 0.001f;
            }

            if (InputService.IsKeyDown(Keys.LeftArrow))
            {
                //pan.X += 0.0001f;
                seed.X += 0.001f;
            }
            if (InputService.IsKeyDown(Keys.RightArrow))
            {
                //pan.X -= 0.0001f;
                seed.X -= 0.001f;
            }
            if (InputService.IsKeyDown(Keys.UpArrow))
            {
                //pan.Y += 0.0001f;
                seed.Y += 0.001f;
            }
            if (InputService.IsKeyDown(Keys.DownArrow))
            {
                //pan.Y -= 0.0001f;
                seed.Y -= 0.001f;
            }

//            fractalEffect.Parameters["Zoom"].SetValue(zoom);
//            fractalEffect.Parameters["Pan"].SetValue(pan);
//            fractalEffect.Parameters["JuliaSeed"].SetValue(seed);
//            fractalEffect.Parameters["Iterations"].SetValue(128);
//            fractalEffect.Parameters["ColorScale"].SetValue(new Vector3F(7, 8, 9));
//            fractalEffect.Techniques["Julia"].Passes[0].Apply();
            //fractalEffect.Techniques["Mandelbrot"].Passes[0].Apply();
//            DeferredDevice.Quad.Draw();
        }

        public override void EndDraw()
        {
            base.EndDraw();
            GraphicsDevice.EndDraw();
        }
    }
}

