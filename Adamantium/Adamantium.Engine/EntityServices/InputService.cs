using System;
using System.Globalization;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Imaging;
using Adamantium.Mathematics;

namespace Adamantium.Engine.EntityServices
{
    public class InputService : EntityService
    {
        private Entity userControlledEntity;
        private Entity selectedEntity;

        public Boolean InstrumentsEnabled { get; set; }
        private GameInputManager inputManager;

        private GamePlayManager gamePlayManager;
        //private AudioManager audioManager;
        private ToolsManager toolsManager;
        private IGamePlatform gamePlatform;

        public InputService(EntityWorld world) : base(world)
        {
            gamePlayManager = DependencyResolver.Resolve<GamePlayManager>();
            toolsManager = DependencyResolver.Resolve<ToolsManager>();
            EntityWorld.EntityRemoved += EntityManagerEntityRemoved;
            inputManager = DependencyResolver.Resolve<GameInputManager>();
            //audioManager = new AudioManager();
            gamePlatform = world.DependencyResolver.Resolve<IGamePlatform>();
        }

        public override void UnloadContent()
        {
            //audioManager.Dispose();
        }

        public override void Update(AppTime gameTime)
        {
            //userControlledEntity = gamePlayManager.SelectedEntity;
            userControlledEntity = toolsManager.SelectedEntity;
            var cameraController = DependencyResolver.Resolve<CameraManager>();

            var currentCamera = cameraController?.UserControlledCamera;
            if (currentCamera == null)
            {
                return;
            }
            
            //currentCamera.Velocity = 1000;

            //if (currentCamera.Type == CameraType.Free || currentCamera.Type == CameraType.Special)
            //{
            //    userControlledEntity = currentCamera.Owner;
            //}
            var gamepadState = inputManager.GetGamepadState(0);
            Double cameraMovementSpeed = currentCamera.Velocity * gameTime.FrameTime;
            float rotationAngle = currentCamera.RotationSpeed * (float)gameTime.FrameTime;
            
            if (inputManager.IsMouseButtonDown(MouseButton.Right))
            {
                currentCamera.RotateRelativeXY(
                      (-inputManager.RawMouseDelta.Y * currentCamera.MouseSensitivity) *
                      (float)gameTime.FrameTime,
                      (-inputManager.RawMouseDelta.X * currentCamera.MouseSensitivity) *
                      (float)gameTime.FrameTime);
            }

            if (inputManager.MouseWheelDelta != 0 && currentCamera.Type != CameraType.FirstPerson)
            {
                currentCamera.TranslateForward(inputManager.MouseWheelDelta * currentCamera.Velocity * gameTime.FrameTime);
            }

            /**********************************************************************
            /* Controls the rotation/move speed of the camera and mouse sensitivity
            /*********************************************************************/

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


            //Gamepad input
            /**********************************************************************/
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
            
            /******************************************************************/

            /**********************************************************************
                           Controls the rotation/move of the camera
            /*********************************************************************/

            if (inputManager.IsKeyDown(Keys.RightArrow) || inputManager.IsKeyDown(Keys.NumPad6))
            {
                currentCamera.RotateUp(-rotationAngle);
                //userControlledEntity?.Transform.RotateUp(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.LeftArrow) || inputManager.IsKeyDown(Keys.NumPad4))
            {
                currentCamera.RotateUp(rotationAngle);
                //userControlledEntity?.Transform.RotateUp(rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.UpArrow) || inputManager.IsKeyDown(Keys.NumPad8))
            {
                currentCamera.RotateRight(rotationAngle);
                //userControlledEntity?.Transform.RotateRight(rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.DownArrow) || inputManager.IsKeyDown(Keys.NumPad5))
            {
                currentCamera.RotateRight(-rotationAngle);
                //userControlledEntity?.Transform.RotateRight(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.PageUp))
            {
                currentCamera.RotateForward(rotationAngle);
                //userControlledEntity?.Transform.RotateForward(rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.PageDown))
            {
                currentCamera.RotateForward(-rotationAngle);
                //userControlledEntity?.Transform.RotateForward(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputManager.IsKeyDown(Keys.W))
            {
                currentCamera.TranslateForward(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateForward(-cameraMovementSpeed);
            }

            if (inputManager.IsKeyDown(Keys.S))
            {
                currentCamera.TranslateForward(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateForward(cameraMovementSpeed);
            }

            if (inputManager.IsKeyDown(Keys.A))
            {
                currentCamera.TranslateRight(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateRight(-cameraMovementSpeed);
            }

            if (inputManager.IsKeyDown(Keys.D))
            {
                currentCamera.TranslateRight(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateRight(cameraMovementSpeed);
            }

            if (inputManager.IsKeyDown(Keys.Q))
            {
                currentCamera.TranslateUp(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateUp(-cameraMovementSpeed);
            }

            if (inputManager.IsKeyDown(Keys.E))
            {
                currentCamera.TranslateUp(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateUp(cameraMovementSpeed);
            }


            /**********************************************************************
            /* Controls the type of the camera
            /*********************************************************************/

            if (inputManager.IsKeyPressed(Keys.F1))
            {
                if (currentCamera.Type != CameraType.Free)
                {
                    currentCamera.SetFreeCamera();
                }
            }

            if (inputManager.IsKeyPressed(Keys.F2))
            {
                // @TODO - think this mode through and implement
                /*UserControlledCamera.CameraType = CameraType.FirstPerson;
                      decimal radius = 2000;
                      SetFirstPersonCamera(UserControlledCamera, UserControlledCamera.Offset, UserControlledCamera.Rotation, radius);*/
            }

            if (inputManager.IsKeyDown(Keys.F3))
            {
                if (cameraController.SetUserControlled(toolsManager.SelectedEntity))
                {
                    toolsManager.SelectedEntity = null;
                }
                else
                {
                    if (currentCamera.Type != CameraType.ThirdPersonFree ||
                        currentCamera.Owner != userControlledEntity)
                    {
                        currentCamera.SetThirdPersonCamera(userControlledEntity, Vector3F.Zero, CameraType.ThirdPersonFree);
                    }
                }
            }

            if (inputManager.IsKeyPressed(Keys.F4))
            {
                if (currentCamera.Type != CameraType.ThirdPersonFreeAlt ||
                    currentCamera.Owner != userControlledEntity)
                {
                    currentCamera.SetThirdPersonCamera(userControlledEntity, new Vector3F(-10, 0, 0), CameraType.ThirdPersonFreeAlt);
                }
            }

            if (inputManager.IsKeyPressed(Keys.F5))
            {
                if (currentCamera.Type != CameraType.ThirdPersonLocked ||
                    currentCamera.Owner != userControlledEntity)
                {
                    currentCamera.SetThirdPersonCamera(userControlledEntity,
                       new Vector3F(-10, 0, 0), CameraType.ThirdPersonLocked);
                }
            }

            // for camera look backwards
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

            // Final ViewMatrix update after all the changes
            currentCamera.Update(gameTime);

            if (inputManager.IsKeyPressed(Keys.F11))
            {
                toolsManager.SelectedEntity?.SetWireFrame();
            }

            if (inputManager.IsKeyPressed(Keys.F12))
            {
                foreach (var window in gamePlatform.Outputs)
                {
                    var filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".jpg";
                    window?.TakeScreenshot(filename, ImageFileType.Jpg);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".tga";
                    window?.TakeScreenshot(filename, ImageFileType.Tga);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".bmp";
                    window?.TakeScreenshot(filename, ImageFileType.Bmp);
//                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".tiff";
//                    window?.TakeScreenShot(filename, ImageFileType.Tiff);
                }
            }

            if (inputManager.IsKeyDown(Keys.F10))
            {
                //audioManager.Play();
            }

            if (inputManager.IsKeyReleased(Keys.F10))
            {
                //audioManager.Stop();
            }
        }



        private void EntityManagerEntityRemoved(object sender, EntityEventArgs e)
        {
            var cameraService = DependencyResolver.Resolve<CameraManager>();
            if (e.Entity == cameraService.UserControlledCamera.Owner)
            {
                cameraService.UserControlledCamera.Type = CameraType.Free;
            }
        }
    }
}
