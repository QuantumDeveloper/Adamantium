using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using AdamantiumVulkan.Core;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.UI.Processors
{
    public class WindowRenderModule : DisposableObject
    {
        private readonly GraphicsDevice GraphicsDevice;
        //private D2DGraphicDevice d2D1Device;
        //private VertexInputLayout _vertexLayout;
        //private Effect _uiEffect;
        //private DrawingContext _context;
        //private bool windowSizeChanged = false;
        private IWindow window;
        //private Matrix4x4F projection;
        //private RenderTarget2D _backBuffer;
        //private DepthStencilBuffer _depthBuffer;
        //private MSAALevel _msaaLevel;
        private bool isWindowResized;
        private Buffer vertexBuffer;
        private Buffer indexBuffer;
        private Texture texture;
        private Sampler sampler;

        public WindowRenderModule(IWindow window, GraphicsDevice device, MSAALevel msaaLevel)
        {
            if (window == null)
            {
                throw new ArgumentException(nameof(window));
            }

            //_msaaLevel = msaaLevel;
            //_mainGraphicsDevice = mainDevice;
            this.window = window;
            window.ClientSizeChanged += Window_ClientSizeChanged;
            GraphicsDevice = device;
            isWindowResized = true;
            viewport = new Viewport();
            viewport.MaxDepth = 1;
            viewport.Width = window.ClientWidth;
            viewport.Height = window.ClientHeight;

            var vertices = GetVertexArray();
            var indices = new UInt32[] { 0, 1, 2, 0, 2, 3 };
            vertexBuffer = Buffer.Vertex.New(device, vertices);
            indexBuffer = Buffer.Index.New(device, indices);
            texture = Texture.Load(GraphicsDevice, Path.Combine("Textures", "texture.jpg"));
            sampler = CreateTextureSampler();
            //_vertexLayout = VertexInputLayout.FromType<VertexPositionTexture>();

            //_uiEffect = ToDispose(Effect.Load(@"Content\Effects\UIEffect.fx.compiled", _graphicsDevice));
            //InitializeResources();
        }

        //private void InitializeResources()
        //{
        //    if (_backBuffer != null)
        //    {
        //        DisposeCollector.RemoveAndDispose(ref _backBuffer);
        //    }

        //    if (_depthBuffer != null)
        //    {
        //        DisposeCollector.RemoveAndDispose(ref _depthBuffer);
        //    }

        //    if (d2D1Device != null)
        //    {
        //        DisposeCollector.RemoveAndDispose(ref d2D1Device);
        //    }

        //    _backBuffer = ToDispose(RenderTarget2D.New(_graphicsDevice, _window.ClientWidth, _window.ClientHeight, _msaaLevel, SurfaceFormat.R8G8B8A8.UNorm));
        //    _depthBuffer = ToDispose(DepthStencilBuffer.New(_graphicsDevice, _window.ClientWidth, _window.ClientHeight, _msaaLevel, DepthFormat.Depth32Stencil8X24));
        //    d2D1Device = ToDispose(D2DGraphicDevice.New(_graphicsDevice, _backBuffer));
        //    _context = new DrawingContext(_graphicsDevice, d2D1Device);

        //    projection = Matrix4x4F.OrthoOffCenterLH(0, _window.ClientWidth, _window.ClientHeight, 0, 1000.0f, 1f);
        //}

        private Viewport viewport;
        private void Window_ClientSizeChanged(object sender, SizeChangedEventArgs e)
        {
            isWindowResized = true;
            viewport.Width = (uint)e.NewSize.Width;
            viewport.Height = (uint)e.NewSize.Height;
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
            samplerInfo.BorderColor = BorderColor.IntOpaqueBlack;
            samplerInfo.UnnormalizedCoordinates = false;
            samplerInfo.CompareEnable = false;
            samplerInfo.CompareOp = CompareOp.Always;
            samplerInfo.MipmapMode = SamplerMipmapMode.Linear;

            return GraphicsDevice.CreateSampler(samplerInfo);
        }

        public bool Prepare()
        {
            if (window.ClientWidth == 0 || window.ClientHeight == 0)
            {
                return false;
            }

            // if (isWindowResized)
            // {
            //     isWindowResized = false;
            //     //InitializeResources();
            //     GraphicsDevice.ResizeBuffers((uint)window.ClientWidth, (uint)window.ClientHeight, 2, SurfaceFormat.R8G8B8A8.UNorm, DepthFormat.Depth32Stencil8X24);
            //     return false;
            // }

            if (isWindowResized || !GraphicsDevice.BeginDraw(Colors.CornflowerBlue, 1.0f, 0))
            {
                GraphicsDevice.ResizePresenter((uint)window.ClientWidth, (uint)window.ClientHeight);
                isWindowResized = false;
                return false;
            }

            //_graphicsDevice.SetRenderTargets(_depthBuffer, _backBuffer);
            //_graphicsDevice.ClearTargets(Colors.White);
            //_graphicsDevice.SetViewport(Presenter.Viewport);
            //_graphicsDevice.BlendState = _graphicsDevice.BlendStates.AlphaBlend;
            //TraverseByLayer(_window, ProcessControl);

            return true;
        }

        private VertexPositionColorTexture[] GetVertexArray()
        {
            var v = new VertexPositionColorTexture[]
            {
                // new VertexPositionColorTexture(){Position = new Vector3F(-0.5f, -0.5f), Color = new Color4F(0.0f, 0.0f, 1.0f, 1.0f), UV = new Vector2F(0.0f, 0.0f)},
                // new VertexPositionColorTexture(){Position = new Vector3F(0.5f, -0.5f), Color = new Color4F(1.0f, 1.0f, 1.0f, 1.0f), UV = new Vector2F(1.0f, 0.0f)},
                // new VertexPositionColorTexture(){Position = new Vector3F(0.5f, 0.5f), Color = new Color4F(0.0f, 1.0f, 0.0f, 1.0f), UV = new Vector2F(1.0f, 1.0f)},
                // new VertexPositionColorTexture(){Position = new Vector3F(-0.5f, 0.5f), Color = new Color4F(1.0f, 0.0f, 1.0f, 1.0f), UV = new Vector2F(0.0f, 1.0f)},
                
                new VertexPositionColorTexture(){Position = new Vector3F(-0.5f, -0.5f), Color = Colors.Red, UV = new Vector2F(0.0f, 0.0f)},
                new VertexPositionColorTexture(){Position = new Vector3F(0.5f, -0.5f), Color = Colors.Green, UV = new Vector2F(1.0f, 0.0f)},
                new VertexPositionColorTexture(){Position = new Vector3F(0.5f, 0.5f), Color = Colors.Blue, UV = new Vector2F(1.0f, 1.0f)},
                new VertexPositionColorTexture(){Position = new Vector3F(-0.5f, 0.5f), Color = Colors.Green, UV = new Vector2F(0.0f, 1.0f)},
            };

            return v;
        }

        public void Render(IGameTime gameTime)
        {
            //var commandList = _graphicsDevice.FinishCommandList(true);
            //mainDevice.ExecuteCommandList(commandList);
            //commandList.Dispose();

            //d2D1Device.BeginDraw();
            //d2D1Device.DrawText("FPS: " + gameTime.FpsCount);
            //d2D1Device.EndDraw();

            //mainDevice.CopyResource(_backBuffer, Presenter.BackBuffer);
            GraphicsDevice.VertexType = typeof(VertexPositionColorTexture);
            GraphicsDevice.PrimitiveTopology = PrimitiveTopology.TriangleList;
            //GraphicsDevice.ApplyViewports(viewport);
            GraphicsDevice.SetViewports(viewport);
            //GraphicsDevice.SetVertexBuffer(vertexBuffer);
            //GraphicsDevice.SetIndexBuffer(indexBuffer);
            GraphicsDevice.BasicEffect.Parameters["fillColor"].SetValue(Colors.Fuchsia.ToVector4());
            GraphicsDevice.BasicEffect.Parameters["sampleType"].SetResource(sampler);
            GraphicsDevice.BasicEffect.Parameters["shaderTexture"].SetResource(texture);

            //GraphicsDevice.BasicEffect.Techniques[0].Passes["Debug"].Apply();
            GraphicsDevice.BasicEffect.Techniques[0].Passes["Textured"].Apply();
            GraphicsDevice.DrawIndexed(vertexBuffer, indexBuffer);
            GraphicsDevice.EndDraw();
            GraphicsDevice.Present();
            
            if (isWindowResized)
            {
                isWindowResized = false;
                GraphicsDevice.ResizePresenter((uint)window.ClientWidth, (uint)window.ClientHeight);
            }
        }

        //public void TraverseByLayer(IVisual visualElement, Action<IVisual> action)
        //{
        //    var queue = new Queue<IVisual>();
        //    queue.Enqueue(visualElement);
        //    while (queue.Count > 0)
        //    {
        //        var control = queue.Dequeue();

        //        action(control);

        //        foreach (var visual in control.GetVisualDescends())
        //        {
        //            queue.Enqueue(visual);
        //        }
        //    }
        //}

        //private void ProcessControl(IVisual element)
        //{
        //    var control = (FrameworkElement)element;
        //    if (control.Visibility == Visibility.Visible)
        //    {
        //        if (control.GetType() == typeof(RenderTargetPanel))
        //        {
        //            int x = 0;
        //        }

        //        if (!control.IsGeometryValid)
        //        {
        //            if (_context == null)
        //            {
        //                throw new ArgumentNullException(nameof(_context));
        //            }

        //            control.Render(_context);

        //        }

        //        if (_context.VisualPresentations.ContainsKey(control))
        //        {
        //            var location = new Vector3F((float)control.Location.X, (float)control.Location.Y, 10);
        //            var world = Matrix4x4F.Scaling(new Vector3D(control.Scale.X, control.Scale.Y, 1)) *
        //                        Matrix4x4F.RotationZ((float)control.Rotation) * Matrix4x4F.Translation(location);

        //            _uiEffect.Parameters["wvp"].SetValue(world * projection);
        //            _uiEffect.Parameters["transparency"].SetValue((float)(control.Opacity));
        //            _uiEffect.Parameters["sampleType"].SetResource(_graphicsDevice.SamplersStates.AnisotropicWrap);

        //            var presentation = _context.VisualPresentations[control];

        //            foreach (var presentationItem in presentation)
        //            {
        //                //if (presentationItem.RenderGeometry != null)
        //                {
        //                    _uiEffect.Parameters["fillColor"].
        //                        SetValue(
        //                            ((SolidColorBrush)presentationItem.RenderBrush).Color.ToVector4());

        //                    if (control.VisualParent == null)
        //                    {
        //                        //_graphicsDevice.SetScissorRectangle(
        //                        //    (int)(control.ClipPosition.X),
        //                        //    (int)(control.ClipPosition.Y),
        //                        //    (int)Math.Round(
        //                        //        control.ClipRectangle.Width + control.ClipPosition.X,
        //                        //        MidpointRounding.AwayFromZero),
        //                        //    (int)Math.Round(
        //                        //        control.ClipRectangle.Height + control.ClipPosition.Y,
        //                        //        MidpointRounding.AwayFromZero));
        //                        if (control.Name == "rect1")
        //                        {
        //                            //Debug.WriteLine("rect1 bounds = " + (int) (control.ClipPosition.X) + " ; " +
        //                            //                (int) (control.ClipPosition.Y) + " ; " +
        //                            //                (int)
        //                            //                   Math.Round(control.ClipRectangle.Width + control.ClipPosition.X,
        //                            //                      MidpointRounding.AwayFromZero) + " ; " +
        //                            //                (int)
        //                            //                   Math.Round(control.ClipRectangle.Height + control.ClipPosition.Y,
        //                            //                      MidpointRounding.AwayFromZero));
        //                        }
        //                    }
        //                    else
        //                    {
        //                        //RawRectangle rectangle = new RawRectangle();
        //                        //rectangle.Left = (int)(control.ClipPosition.X);
        //                        //rectangle.Top = (int)(control.ClipPosition.Y);
        //                        //rectangle.Right = (int)Math.Round(
        //                        //    control.ClipRectangle.Width + control.ClipPosition.X,
        //                        //    MidpointRounding.AwayFromZero);
        //                        //rectangle.Bottom = (int)Math.Round(
        //                        //    control.ClipRectangle.Height + control.ClipPosition.Y,
        //                        //    MidpointRounding.AwayFromZero);

        //                        //rectangle.Right = Math.Max(rectangle.Right, rectangle.Left);
        //                        //rectangle.Bottom = Math.Max(rectangle.Bottom, rectangle.Top);

        //                        //if (control.ClipPosition.X < control.VisualParent.ClipPosition.X)
        //                        //{
        //                        //    rectangle.Left = (int)(control.VisualParent.ClipPosition.X);
        //                        //}

        //                        //if (control.ClipPosition.Y < control.VisualParent.ClipPosition.Y)
        //                        //{
        //                        //    rectangle.Top = (int)(control.VisualParent.ClipPosition.Y);
        //                        //}

        //                        //if (control.ClipPosition.X + control.ClipRectangle.Width >
        //                        //    control.VisualParent.ClipPosition.X + control.VisualParent.ClipRectangle.Width)
        //                        //{
        //                        //    rectangle.Right =
        //                        //        (int)Math.Max(
        //                        //            control.VisualParent.ClipRectangle.Width +
        //                        //            control.VisualParent.ClipPosition.X,
        //                        //            rectangle.Left);
        //                        //}

        //                        //if (control.ClipPosition.Y + control.ClipRectangle.Height >
        //                        //    control.VisualParent.ClipPosition.Y + control.VisualParent.ClipRectangle.Height)
        //                        //{
        //                        //    rectangle.Bottom =
        //                        //        (int)Math.Max(
        //                        //            control.VisualParent.ClipRectangle.Height +
        //                        //            control.VisualParent.ClipPosition.Y,
        //                        //            rectangle.Top);
        //                        //}

        //                        if (control.Name == "rect1")
        //                        {
        //                            //Debug.WriteLine("rect1 clip bounds = " + rectangle.Left + " ; " + rectangle.Top + " ; " + rectangle.Right + " ; " + rectangle.Bottom);
        //                        }

        //                        //_graphicsDevice.SetScissorRectangles(rectangle);
        //                    }


        //                    //_graphicsDevice.VertexInputLayout = _vertexLayout;

        //                    //_graphicsDevice.SetVertexBuffer(presentationItem.RenderGeometry);
        //                    //_graphicsDevice.SetIndexBuffer(presentationItem.RenderGeometryIndices, true);

        //                    //if (presentationItem.HasTexture)
        //                    //{
        //                    //    _uiEffect.Parameters["shaderTexture"].SetResource(presentationItem.Texture);
        //                    //    _uiEffect.Techniques[0].Passes["Textured"].Apply();
        //                    //}
        //                    //else
        //                    //{

        //                    //    _uiEffect.Techniques[0].Passes["SolidColor"].Apply();
        //                    //}

        //                    //_graphicsDevice.DrawIndexed(
        //                    //    presentationItem.RenderGeometryType,
        //                    //    presentationItem.RenderGeometryIndices.ElementCount);

        //                }

        //                //if (presentationItem.StrokeGeometry != null)
        //                {
        //                    _uiEffect.Parameters["fillColor"].
        //                        SetValue(
        //                            ((SolidColorBrush)presentationItem.StrokeBrush).Color.ToVector4());
        //                    if (control.VisualParent == null)
        //                    {
        //                        //_graphicsDevice.SetScissorRectangle(
        //                            //(int)(control.ClipPosition.X),
        //                            //(int)(control.ClipPosition.Y),
        //                            //(int)Math.Round(
        //                            //    control.ClipRectangle.Width + control.ClipPosition.X,
        //                            //    MidpointRounding.AwayFromZero),
        //                            //(int)Math.Round(
        //                            //    control.ClipRectangle.Height + control.ClipPosition.Y,
        //                            //    MidpointRounding.AwayFromZero));
        //                    }
        //                    else
        //                    {
        //                        var rectangle = new Mathematics.Rectangle();
        //                        rectangle.Left = (int)(control.ClipPosition.X);
        //                        rectangle.Top = (int)(control.ClipPosition.Y);
        //                        rectangle.Right = (int)Math.Round(
        //                            control.ClipRectangle.Width + control.ClipPosition.X,
        //                            MidpointRounding.AwayFromZero);
        //                        rectangle.Bottom = (int)Math.Round(
        //                            control.ClipRectangle.Height + control.ClipPosition.Y,
        //                            MidpointRounding.AwayFromZero);

        //                        rectangle.Right = Math.Max(rectangle.Right, 0);
        //                        rectangle.Bottom = Math.Max(rectangle.Bottom, 0);

        //                        if (control.ClipPosition.X < control.VisualParent.ClipPosition.X)
        //                        {
        //                            rectangle.Left = (int)(control.VisualParent.ClipPosition.X);
        //                        }

        //                        if (control.ClipPosition.Y < control.VisualParent.ClipPosition.Y)
        //                        {
        //                            rectangle.Top = (int)(control.VisualParent.ClipPosition.Y);
        //                        }

        //                        if (control.ClipPosition.X + control.ClipRectangle.Width >
        //                            control.VisualParent.ClipPosition.X + control.VisualParent.ClipRectangle.Width)
        //                        {
        //                            rectangle.Right =
        //                                (int)(control.VisualParent.ClipRectangle.Width +
        //                                       control.VisualParent.ClipPosition.X);
        //                        }

        //                        if (control.ClipPosition.Y + control.ClipRectangle.Height >
        //                            control.VisualParent.ClipPosition.Y + control.VisualParent.ClipRectangle.Height)
        //                        {
        //                            rectangle.Bottom =
        //                                (int)(control.VisualParent.ClipRectangle.Height +
        //                                       control.VisualParent.ClipPosition.Y);

        //                            if (control.Name == "rect4")
        //                            {
        //                                Debug.WriteLine("rect4 clip bounds = " + rectangle.Bottom);
        //                            }
        //                        }

        //                        //_graphicsDevice.SetScissorRectangles(rectangle);
        //                    }

        //                    //_graphicsDevice.VertexInputLayout = _vertexLayout;

        //                    //_graphicsDevice.SetVertexBuffer(presentationItem.StrokeGeometry);
        //                    //_graphicsDevice.SetIndexBuffer(presentationItem.StrokeGeometryIndices, true);

        //                    //_uiEffect.Techniques[0].Passes["SolidColor"].Apply();

        //                    //_graphicsDevice.DrawIndexed(
        //                    //    presentationItem.StrokeGeometryType,
        //                    //    presentationItem.StrokeGeometryIndices.ElementCount);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}