using System;
using System.Globalization;
using Adamantium.Core;
using Adamantium.Engine.Services;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Multiverse;
using Adamantium.Multiverse.Input;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;

namespace Adamantium.Engine.EntityServices;

public class InputService : EntityService
{
    private const double WheelNotch = 120;

    private Entity userControlledEntity;
    private Entity selectedEntity;
    //private AudioManager audioManager;
    private Selection selection;
    private Observatory observatory;

    public InputService(EntityWorld world) : base(world)
    {
        selection = EntityWorld.Satellites.Get<Selection>();
        observatory = EntityWorld.Satellites.Get<Observatory>();
        EntityWorld.EntityManager.EntityRemoved += EntityManagerEntityRemoved;
        //audioManager = new AudioManager();
    }

    public Boolean InstrumentsEnabled { get; set; }

    public override void UnloadContent()
    {
        //audioManager.Dispose();
    }

    public override bool IsUpdateService => true;
    public override bool IsRenderingService => false;
    public override EntityServiceType ServiceType => EntityServiceType.Update;

    /// <summary>Whether the third-person keys follow the whole object or the exact entity picked. An editor turns it off
    /// and decides for itself.</summary>
    public bool FollowsWholeObject { get; set; } = true;

    /// <summary>The entity the application put the player in charge of - what the third-person keys follow when nothing
    /// is selected.</summary>
    public Entity UserControlledEntity { get; set; }

    /// <summary>
    /// The mouse turns and zooms the camera of the output under the pointer, the keys and gamepad drive the camera of
    /// the output taking the keyboard - which may be another output, or none. Every visible camera is updated either way.
    /// </summary>
    public override void Update(AppTime appTime)
    {
        userControlledEntity = selection.Current ?? UserControlledEntity;

        if (observatory.PointerOutput is { Camera: { } pointerCamera } pointerOutput)
        {
            HandlePointer(pointerOutput.Input, pointerCamera, appTime);
        }

        if (observatory.KeyboardOutput is { Camera: { } keyboardCamera } keyboardOutput)
        {
            HandleKeyboard(keyboardOutput, keyboardCamera, appTime);
        }

        var cameras = observatory.CurrentCameras;
        for (int i = 0; i < cameras.Count; i++)
        {
            cameras[i].Update(appTime);
        }
    }

    private void HandlePointer(InputWormhole inputManager, Camera currentCamera, AppTime appTime)
    {
        if (inputManager.IsMouseButtonDown(MouseButton.Right))
        {
            currentCamera.RotateRelativeXY(
                (inputManager.RawMouseDelta.Y * currentCamera.MouseSensitivity) *
                (float)appTime.FrameTime,
                (-inputManager.RawMouseDelta.X * currentCamera.MouseSensitivity) *
                (float)appTime.FrameTime);
        }

        if (inputManager.MouseWheelDelta != 0)
        {
            currentCamera.Zoom(inputManager.MouseWheelDelta / WheelNotch);
        }
    }

    private void HandleKeyboard(UniverseOutput output, Camera currentCamera, AppTime appTime)
    {
        var inputManager = output.Input;
        var gamepadState = inputManager.GetGamepadState(0);
        Double cameraMovementSpeed = currentCamera.Velocity * appTime.FrameTime;
        float rotationAngle = currentCamera.RotationSpeed * (float)appTime.FrameTime;

        if (inputManager.IsKeyPressed(Keys.Divide))
        {
            currentCamera.RotationSpeed -= 1f;
        }

        if (inputManager.IsKeyPressed(Keys.Multiply))
        {
            currentCamera.RotationSpeed += 1f;
        }

        if (inputManager.IsKeyPressed(Keys.Add))
        {
            currentCamera.Velocity *= 2;
        }
        if (inputManager.IsKeyPressed(Keys.Subtract))
        {
            currentCamera.Velocity /= 2;
        }

        if (inputManager.IsKeyPressed(Keys.Digit0))
        {
            currentCamera.DragVelocity *= 2;
        }
        if (inputManager.IsKeyPressed(Keys.Digit9))
        {
            currentCamera.DragVelocity /= 2;
        }

        if (inputManager.IsKeyPressed(Keys.OemOpenBrackets))
        {
            currentCamera.MouseSensitivity -= 0.1f;
        }

        if (inputManager.IsKeyPressed(Keys.OemCloseBrackets))
        {
            currentCamera.MouseSensitivity += 0.1f;
        }

        if (gamepadState.RightThumb.X != 0)
        {
            currentCamera.RotateUp(-rotationAngle * gamepadState.RightThumb.X);
        }

        if (gamepadState.RightThumb.Y != 0)
        {
            currentCamera.RotateRight(rotationAngle * gamepadState.RightThumb.Y);
        }

        if (gamepadState.LeftTrigger > 0)
        {
            currentCamera.RotateForward(rotationAngle);
        }

        if (gamepadState.RightTrigger > 0)
        {
            currentCamera.RotateForward(-rotationAngle);
        }

        if (gamepadState.LeftThumb.Y != 0)
        {
            currentCamera.TranslateForward(gamepadState.LeftThumb.Y * cameraMovementSpeed);
        }

        if (gamepadState.LeftThumb.X != 0)
        {
            currentCamera.TranslateRight(gamepadState.LeftThumb.X * cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.RightArrow) || inputManager.IsKeyDown(Keys.NumPad6))
        {
            currentCamera.RotateUp(-rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.LeftArrow) || inputManager.IsKeyDown(Keys.NumPad4))
        {
            currentCamera.RotateUp(rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.UpArrow) || inputManager.IsKeyDown(Keys.NumPad8))
        {
            currentCamera.RotateRight(-rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.DownArrow) || inputManager.IsKeyDown(Keys.NumPad5))
        {
            currentCamera.RotateRight(rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.PageUp))
        {
            currentCamera.RotateForward(rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.PageDown))
        {
            currentCamera.RotateForward(-rotationAngle);
        }

        if (inputManager.IsKeyDown(Keys.W))
        {
            currentCamera.TranslateForward(cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.S))
        {
            currentCamera.TranslateForward(-cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.A))
        {
            currentCamera.TranslateRight(-cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.D))
        {
            currentCamera.TranslateRight(cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.Q))
        {
            currentCamera.TranslateUp(cameraMovementSpeed);
        }

        if (inputManager.IsKeyDown(Keys.E))
        {
            currentCamera.TranslateUp(-cameraMovementSpeed);
        }

        if (inputManager.IsKeyPressed(Keys.F1))
        {
            if (currentCamera.Type != CameraType.Free)
            {
                currentCamera.SetFreeCamera();
            }
        }

        if (inputManager.IsKeyPressed(Keys.F3))
        {
            if (selection.Current?.GetComponent<Camera>() is { } selectedCamera)
            {
                output.Camera = selectedCamera;
                selection.Current = null;
            }
            else
            {
                Follow(currentCamera, userControlledEntity, Vector3F.Zero, CameraType.ThirdPersonFree);
            }
        }

        if (inputManager.IsKeyPressed(Keys.F4))
        {
            Follow(currentCamera, userControlledEntity, new Vector3F(-10, 0, 0), CameraType.ThirdPersonFreeAlt);
        }

        if (inputManager.IsKeyPressed(Keys.F5))
        {
            Follow(currentCamera, userControlledEntity, new Vector3F(-10, 0, 0), CameraType.ThirdPersonLocked);
        }

        if (inputManager.IsKeyPressed(Keys.C))
        {
            if ((currentCamera.Type == CameraType.ThirdPersonFree) ||
                (currentCamera.Type == CameraType.ThirdPersonFreeAlt))
            {
                currentCamera.SetThirdPersonLookBackwards(true);
            }
        }

        if (inputManager.IsKeyReleased(Keys.C))
        {
            if ((currentCamera.Type == CameraType.ThirdPersonFree) ||
                (currentCamera.Type == CameraType.ThirdPersonFreeAlt))
            {
                currentCamera.SetThirdPersonLookBackwards(false);
            }
        }

        if (inputManager.IsKeyPressed(Keys.F11))
        {
            selection.Current?.SetWireFrame();
        }

        if (inputManager.IsKeyPressed(Keys.F12))
        {
            foreach (var window in observatory.Outputs)
            {
                var filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".jpg";
                window?.TakeScreenshotAsync(filename, ImageFileType.Png);
            }
        }
    }

    private void Follow(Camera camera, Entity subject, Vector3F relativeRotation, CameraType type)
    {
        if (subject == null)
        {
            return;
        }

        if (FollowsWholeObject)
        {
            subject = RootOf(subject);
        }

        if (camera.Type == type && camera.Subject == subject)
        {
            return;
        }

        camera.SetThirdPersonCamera(subject, relativeRotation, type);
    }

    private static Entity RootOf(Entity entity)
    {
        var root = entity;
        while (root.Owner != null)
        {
            root = root.Owner;
        }

        return root;
    }

    private void EntityManagerEntityRemoved(object sender, EntityEventArgs e)
    {
        var outputs = observatory.Outputs;
        for (int i = 0; i < outputs.Count; i++)
        {
            var cameras = outputs[i].Cameras;
            for (int j = 0; j < cameras.Count; j++)
            {
                if (e.Entity == cameras[j].Owner)
                {
                    cameras[j].Type = CameraType.Free;
                }
            }
        }
    }
}
