﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Templates.Camera;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Game;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Services
{
    ///<summary>
    ///Class for control all cameras present in game.
    ///</summary>
    public class CameraService
    {
        private readonly Object syncRoot = new object();
        private const float DefaultZNear = 0.1f;
        private const float DefaultZFar = 100000000.0f;

        ///<summary>
        ///Collection of all cameras.
        ///</summary>
        private readonly System.Collections.Generic.Dictionary<GameWindow, List<Camera>> windowToCameras;
        private readonly System.Collections.Generic.Dictionary<Camera, GameWindow> cameraToWindow;

        private readonly AdamantiumCollection<Camera> windowToCamerasCollection;

        ///<summary>
        ///Collection of active cameras.
        ///</summary>
        private readonly System.Collections.Generic.Dictionary<GameWindow, Camera> activeCameras;

        private readonly AdamantiumCollection<Camera> activeCamerasCollection;

        public ReadOnlyCollection<Camera> ActiveCameras => activeCamerasCollection.AsReadOnly();

        public ReadOnlyCollection<Camera> Cameras => windowToCamerasCollection.AsReadOnly();

        private readonly Game _game;

        ///<summary>
        ///The camera that currently controlled by user.
        ///</summary>
        public Camera UserControlledCamera { get; set; }

        private Entity CameraIcon;
        private Entity CameraVisual;
        private RenderableComponent cameraIconRenderer;

        private Entity SelectedCamera;

        private EntityGroup cameraGroup;
        private static int cameraNumber = 1;

        ///<summary>
        ///Constructor.
        ///</summary>
        public CameraService(Game game)
        {
            game.Services.Add(this);
            windowToCameras = new System.Collections.Generic.Dictionary<GameWindow, List<Camera>>();
            cameraToWindow = new System.Collections.Generic.Dictionary<Camera, GameWindow>();
            activeCameras = new System.Collections.Generic.Dictionary<GameWindow, Camera>();
            windowToCamerasCollection = new AdamantiumCollection<Camera>();
            activeCamerasCollection = new AdamantiumCollection<Camera>();
            _game = game;
            cameraGroup = _game.EntityWorld.CreateGroup("Cameras");

            foreach (var window in _game.Windows)
            {
                CreateCameraForWindowInternal(window);
            }

            _game.WindowCreated += GameBaseWindowCreated;
            _game.WindowRemoved += GameBaseWindowRemoved;
            _game.WindowSizeChanged += WindowSizeChanged;
            _game.WindowActivated += GameBaseWindowActivated;
            _game.WindowDeactivated += GameBaseWindowDeactivated;

            CreateCameraIcon();
            CreateCameraVisual();
        }

        private void CreateCameraIcon()
        {
            CameraIcon = new CameraIconTemplate().BuildEntity(null, "Camera icon");
            cameraIconRenderer = CameraIcon.GetComponent<RenderableComponent>();
        }

        private void CreateCameraVisual()
        {
            CameraVisual = new CameraVisualTemplate().BuildEntity(null, "Camera Debug");
        }

        private void GameBaseWindowDeactivated(object sender, GameWindowEventArgs e)
        {
            //UserControlledCamera = null;
        }

        public bool Contains(Entity camera)
        {
            return cameraGroup.Contains(camera);
        }

        public void SetSelected(Entity camera)
        {
            SelectedCamera = camera;
        }

        private void GameBaseWindowActivated(object sender, GameWindowEventArgs e)
        {
            if (activeCameras.ContainsKey(e.Window))
            {
                UserControlledCamera = activeCameras[e.Window];
            }
        }

        private void WindowSizeChanged(object sender, GameWindowSizeChangedEventArgs e)
        {
            UpdateDimensions(e.Window, e.Window.Width, e.Window.Height);
        }

        private void GameBaseWindowRemoved(object sender, GameWindowEventArgs e)
        {
            RemoveCamera(e.Window);
        }

        private void GameBaseWindowCreated(object sender, GameWindowEventArgs e)
        {
            CreateCameraForWindowInternal(e.Window);
        }

        public void CreateCamera(string name)
        {

            foreach (var window in _game.Windows)
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = $"Camera ({cameraNumber})";
                    cameraNumber++;
                }
                CreateCameraForWindow(window, name);
            }
        }

        private void CreateCameraForWindowInternal(GameWindow window)
        {
            var camera = CreateCamera(window.Width, window.Height, $"Main camera for {window.Name}");
            camera.Owner.Transform.Position = new Vector3D(0,0,-20);
            AddCamera(window, camera);
            if (_game.ActiveWindow != null)
            {
                UserControlledCamera = camera;
            }
        }


        public Camera CreateCameraForWindow(GameWindow window, string name)
        {
            var camera = CreateCamera(window.Width, window.Height, name);
            AddCamera(window, camera);
            if (_game.ActiveWindow != null)
            {
                UserControlledCamera = camera;
            }
            return camera;
        }

        private Camera CreateCamera(uint width, uint height, string name)
        {
            Vector3D position = Vector3D.Zero;
            if (UserControlledCamera != null)
            {
                position = (CameraIcon.GetDiameter() * 2 * (Vector3D)UserControlledCamera.Forward) + UserControlledCamera.GetOwnerPosition();
            }
            
            var entity = new CameraTemplate().BuildEntity(null, name, position, Vector3F.ForwardLH, Vector3F.Up, width, height, DefaultZNear, DefaultZFar);
            return entity.GetComponent<Camera>();
        }

        private Camera CreateCamera(uint width, uint height, float znear, float zfar, string name)
        {
            var entity = new CameraTemplate().BuildEntity(null, name, Vector3D.Zero, Vector3F.ForwardLH, Vector3F.Up, width, height, znear, zfar);
            return entity.GetComponent<Camera>();
        }

        ///<summary>
        ///Adds camera with control handler to collection.
        ///</summary>
        ///<remarks>
        ///Sets added camera as Active and User Controlled.
        ///</remarks>
        public void AddCamera(GameWindow window, Camera camera)
        {
            lock (syncRoot)
            {
                if (!windowToCameras.ContainsKey(window))
                {
                    windowToCameras.Add(window, new List<Camera>() { camera });
                    windowToCamerasCollection.Add(camera);
                }
                else
                {
                    windowToCameras[window].Add(camera);
                    windowToCamerasCollection.Add(camera);
                }

                if (!cameraToWindow.ContainsKey(camera))
                {
                    cameraToWindow.Add(camera, window);
                }
                else
                {
                    cameraToWindow[camera] = window;
                }

                if (!activeCameras.ContainsKey(window))
                {
                    activeCameras.Add(window, camera);
                    activeCamerasCollection.Add(camera);
                }

                if (!cameraGroup.Contains(camera.Owner))
                {
                    cameraGroup.Add(camera.Owner);
                    _game.EntityWorld.AddEntity(camera.Owner);
                }
            }
        }

        public void UpdateDimensions(GameWindow handle, UInt32 width, UInt32 height)
        {
            lock (syncRoot)
            {
                if (!windowToCameras.ContainsKey(handle)) return;
                var list = windowToCameras[handle];
                foreach (var cameraComponent in list)
                {
                    cameraComponent.Width = width;
                    cameraComponent.Height = height;
                    cameraComponent.Initialize();
                }
            }
        }

        ///<summary>
        ///Sets camera as User Controlled.
        ///</summary>
        public void SetUserControlled(Camera camera)
        {
            UserControlledCamera = camera;
        }

        public bool SetUserControlled(Entity camera)
        {
            if (cameraGroup.Contains(camera))
            {
                var component = camera.GetComponent<Camera>();
                UserControlledCamera = component;
                SetActive(component);
                return true;
            }
            return false;
        }

        ///<summary>
        ///Deletes camera from collection.
        ///</summary>
        public void RemoveCamera(Camera camera)
        {
            lock (syncRoot)
            {
                foreach (var camera1 in windowToCameras)
                {
                    var cameraList = camera1.Value;
                    if (cameraList.Contains(camera))
                    {
                        cameraList.Remove(camera);
                        windowToCamerasCollection.Remove(camera);
                        break;
                    }
                }

                foreach (var activeCamera in activeCameras)
                {
                    if (activeCamera.Value == camera)
                    {
                        activeCameras.Remove(activeCamera.Key);
                        activeCamerasCollection.Remove(activeCamera.Value);
                        break;
                    }
                }
            }
        }

        ///<summary>
        ///Deletes camera from collection.
        ///</summary>
        public void RemoveCamera(GameWindow bindingContext)
        {
            lock (syncRoot)
            {
                if (windowToCameras.ContainsKey(bindingContext))
                {
                    var cameras = windowToCameras[bindingContext];
                    windowToCamerasCollection.Remove(cameras);
                    windowToCameras.Remove(bindingContext);
                }

                if (activeCameras.ContainsKey(bindingContext))
                {
                    var cameras = activeCameras[bindingContext];
                    activeCamerasCollection.Remove(cameras);
                    activeCameras.Remove(bindingContext);
                }
            }
        }

        ///<summary>
        ///Adds group of cameras to collection.
        ///</summary>
        public void AddCameras(GameWindow bindTarget, params Camera[] cameraGroup)
        {
            lock (syncRoot)
            {
                if (windowToCameras.ContainsKey(bindTarget))
                {
                    var list = windowToCameras[bindTarget];
                    foreach (var cameraComponent in cameraGroup)
                    {
                        if (!list.Contains(cameraComponent))
                        {
                            list.Add(cameraComponent);
                            windowToCamerasCollection.Add(cameraComponent);
                        }
                    }
                }
                else
                {
                    windowToCameras.Add(bindTarget, new List<Camera>(cameraGroup));
                    windowToCamerasCollection.AddRange(cameraGroup);
                }
            }
        }

        ///<summary>
        ///Sets camera as Active.
        ///</summary>
        public void SetActive(Camera camera, GameWindow window)
        {
            lock (syncRoot)
            {
                var old = activeCameras[window];
                activeCamerasCollection.Remove(old);
                activeCamerasCollection.Add(camera);
                activeCameras[window] = camera;
            }
        }

        ///<summary>
        ///Sets camera as Active.
        ///</summary>
        public void SetActive(Camera camera)
        {
            lock (syncRoot)
            {
                var window = cameraToWindow[camera];
                var old = activeCameras[window];
                activeCamerasCollection.Remove(old);
                activeCamerasCollection.Add(camera);
                activeCameras[window] = camera;
            }
        }

        public Camera GetActive(GameWindow window)
        {
            lock (syncRoot)
            {
                if (activeCameras.ContainsKey(window))
                {
                    return activeCameras[window];
                }
                return null;
            }
        }

        public CollisionResult Intersects(Camera camera, Vector2F cursorPosition, CollisionMode collisionMode)
        {
            lock (syncRoot)
            {
                CollisionResult collisionResult = new CollisionResult(CompareOrder.Less);
                var projectionMatrix = camera.ProjectionMatrix;
                foreach (var currentCamera in Cameras)
                {
                    if (currentCamera == UserControlledCamera || !currentCamera.Owner.IsEnabled)
                    {
                        continue;
                    }

                    var transform = currentCamera.Owner.Transform.GetMetadata(camera);
                    Vector3F point;
                    var billboard = Matrix4x4F.BillboardLH(transform.RelativePosition, Vector3F.Zero, camera.Up, camera.Forward);
                    var rotation = MathHelper.GetRotationFromMatrix(billboard);
                    var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);
                    var ray = Collisions.CalculateRay(cursorPosition, camera, world, projectionMatrix, true);

                    var collision = CameraIcon.GetComponent<Collider>();
                    if (collision != null)
                    {
                        var intersects = collision.Intersects(ref ray, out point);
                        if (intersects)
                        {
                            collisionResult.ValidateAndSetValues(currentCamera.Owner, point, true);
                        }
                    }
                }
                if (collisionResult.Intersects)
                {
                    SelectedCamera = collisionResult.Entity;
                }
                return collisionResult;
            }
        }

//        public void DrawCameraIcons(Effect effect, Camera camera, GraphicsDevice drawingContext, IGameTime gameTime)
//        {
//            lock (syncRoot)
//            {
//                var view = camera.ViewMatrix;
//                var proj = camera.ProjectionMatrix;
//                effect.Parameters["viewMatrix"].SetValue(view);
//                effect.Parameters["projectionMatrix"].SetValue(proj);
//
//                foreach (var currentCamera in Cameras)
//                {
//                    if (currentCamera == camera || !currentCamera.Owner.IsEnabled)
//                    {
//                        continue;
//                    }
//
//                    var transform = currentCamera.Owner.Transform.GetMetadata(camera);
//                    if (transform.RelativePosition.Length() < CameraIcon.GetDiameter())
//                    {
//                        continue;
//                    }
//
//                    var transparency = 1 - (1 / transform.RelativePosition.Length());
//
//                    var billboard = Matrix4x4F.BillboardRH(transform.RelativePosition, Vector3F.Zero, camera.Up, camera.Forward);
//                    var rotation = MathHelper.GetRotationFromMatrix(billboard);
//                    var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);
//
//                    effect.Parameters["transparency"].SetValue(transparency);
//                    effect.Parameters["worldMatrix"].SetValue(world);
//                    effect.Parameters["wvp"].SetValue(world * view * proj);
//                    effect.Parameters["meshColor"].SetValue(Colors.White.ToVector3());
//                    effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
//                    cameraIconRenderer.Draw(drawingContext, gameTime);
//                }
//            }
//
//            effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
//        }

//        public void DrawDebugCamera(Effect effect, Camera camera, GraphicsDevice drawingContext, IGameTime gametime)
//        {
//            if (SelectedCamera == null || !SelectedCamera.IsEnabled)
//                return;
//
//            var cameraRender = CameraVisual.GetComponent<RenderableComponent>();
//            lock (syncRoot)
//            {
//                var view = camera.ViewMatrix;
//                var proj = camera.ProjectionMatrix;
//                effect.Parameters["viewMatrix"].SetValue(view);
//                effect.Parameters["projectionMatrix"].SetValue(proj);
//
//                if (SelectedCamera == camera.Owner)
//                {
//                    return;
//                }
//
//                var transform = SelectedCamera.Transform.GetMetadata(camera);
//                var world = Matrix4x4F.RotationQuaternion(camera.Rotation) * transform.WorldMatrix;
//                effect.Parameters["transparency"].SetValue(1.0f);
//                effect.Parameters["worldMatrix"].SetValue(world);
//                effect.Parameters["wvp"].SetValue(world * view * proj);
//                effect.Parameters["meshColor"].SetValue(Colors.Beige.ToVector3());
//                effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
//                cameraRender.Draw(drawingContext, gametime);
//            }
//
//            effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
//        }

        //Индексатор для ключа
        public List<Camera> this[GameWindow key]
        {
            get
            {
                lock (syncRoot)
                {
                    return windowToCameras[key];
                }
            }
            set
            {
                lock (syncRoot)
                {
                    var old = windowToCameras[key];
                    windowToCamerasCollection.Remove(old);
                    windowToCamerasCollection.AddRange(value);
                    windowToCameras[key] = value;
                }
            }
        }

        //Индексатор для значения
        public GameWindow this[Camera value]
        {
            get
            {
                lock (syncRoot)
                {
                    foreach (var camera in windowToCameras)
                    {
                        if (camera.Value.Contains(value))
                        {
                            return camera.Key;
                        }
                    }
                }
                return null;
            }
        }

    }
}