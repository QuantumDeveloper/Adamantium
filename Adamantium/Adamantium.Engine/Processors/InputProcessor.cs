using System;
using System.Globalization;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Processors
{
    public class InputProcessor : EntityProcessor
    {
        private Entity userControlledEntity;
        private Entity selectedEntity;

        public Boolean InstrumentsEnabled { get; set; }
        private InputService inputService;

        private GamePlayManager gamePlayManager;
        //private AudioManager audioManager;
        private ToolsService toolsService;
        private IGamePlatform gamePlatform;

        public InputProcessor(EntityWorld world) : base(world)
        {
            gamePlayManager = Services.Get<GamePlayManager>();
            toolsService = Services.Get<ToolsService>();
            EntityWorld.EntityRemoved += EntityManagerEntityRemoved;
            inputService = Services.Get<InputService>();
            //audioManager = new AudioManager();
            gamePlatform = world.Services.Get<IGamePlatform>();
        }

        public override void UnloadContent()
        {
            //audioManager.Dispose();
        }

        public override void Update(IGameTime gameTime)
        {
            //userControlledEntity = gamePlayManager.SelectedEntity;
            userControlledEntity = toolsService.SelectedEntity;
            var cameraController = Services.Get<CameraService>();

            var currentCamera = cameraController?.UserControlledCamera;
            if (currentCamera == null)
            {
                return;
            }

            //if (currentCamera.Type == CameraType.Free || currentCamera.Type == CameraType.Special)
            //{
            //    userControlledEntity = currentCamera.Owner;
            //}
            var gamepadState = inputService.GetGamepadState(0);
            Double cameraMovementSpeed = currentCamera.Velocity * gameTime.FrameTime;
            float rotationAngle = currentCamera.RotationSpeed * (float)gameTime.FrameTime;
            
            if (inputService.IsMouseButtonDown(MouseButton.Right))
            {
                currentCamera.RotateRelativeXY(
                      (-inputService.RawMouseDelta.Y * currentCamera.MouseSensitivity) *
                      (float)gameTime.FrameTime,
                      (-inputService.RawMouseDelta.X * currentCamera.MouseSensitivity) *
                      (float)gameTime.FrameTime);
            }

            if (inputService.MouseWheelDelta != 0 && currentCamera.Type != CameraType.FirstPerson)
            {
                currentCamera.TranslateForward(inputService.MouseWheelDelta * currentCamera.Velocity * gameTime.FrameTime);
            }

            /**********************************************************************
            /* Controls the rotation/move speed of the camera and mouse sensitivity
            /*********************************************************************/

            if (inputService.IsKeyPressed(Keys.Divide))
            {
                currentCamera.RotationSpeed -= 1f;
            }

            if (inputService.IsKeyPressed(Keys.Multiply))
            {
                currentCamera.RotationSpeed += 1f;
            }

            if (inputService.IsKeyPressed(Keys.Add))
            {
                currentCamera.Velocity *= 2;
            }
            if (inputService.IsKeyPressed(Keys.Subtract))
            {
                currentCamera.Velocity /= 2;
            }

            if (inputService.IsKeyPressed(Keys.Digit0))
            {
                currentCamera.DragVelocity *= 2;
            }
            if (inputService.IsKeyPressed(Keys.Digit9))
            {
                currentCamera.DragVelocity /= 2;
            }

            if (inputService.IsKeyPressed(Keys.OemOpenBrackets))
            {
                currentCamera.MouseSensitivity -= 0.1f;
            }

            if (inputService.IsKeyPressed(Keys.OemCloseBrackets))
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

            if (inputService.IsKeyDown(Keys.RightArrow) || inputService.IsKeyDown(Keys.NumPad6))
            {
                currentCamera.RotateUp(-rotationAngle);
                //userControlledEntity?.Transform.RotateUp(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.LeftArrow) || inputService.IsKeyDown(Keys.NumPad4))
            {
                currentCamera.RotateUp(rotationAngle);
                //userControlledEntity?.Transform.RotateUp(rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.UpArrow) || inputService.IsKeyDown(Keys.NumPad8))
            {
                currentCamera.RotateRight(rotationAngle);
                //userControlledEntity?.Transform.RotateRight(rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.DownArrow) || inputService.IsKeyDown(Keys.NumPad5))
            {
                currentCamera.RotateRight(-rotationAngle);
                //userControlledEntity?.Transform.RotateRight(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.PageUp))
            {
                currentCamera.RotateForward(rotationAngle);
                //userControlledEntity?.Transform.RotateForward(rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.PageDown))
            {
                currentCamera.RotateForward(-rotationAngle);
                //userControlledEntity?.Transform.RotateForward(-rotationAngle, RotationUnits.Degrees);
            }

            if (inputService.IsKeyDown(Keys.W))
            {
                currentCamera.TranslateForward(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateForward(-cameraMovementSpeed);
            }

            if (inputService.IsKeyDown(Keys.S))
            {
                currentCamera.TranslateForward(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateForward(cameraMovementSpeed);
            }

            if (inputService.IsKeyDown(Keys.A))
            {
                currentCamera.TranslateRight(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateRight(-cameraMovementSpeed);
            }

            if (inputService.IsKeyDown(Keys.D))
            {
                currentCamera.TranslateRight(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateRight(cameraMovementSpeed);
            }

            if (inputService.IsKeyDown(Keys.Q))
            {
                currentCamera.TranslateUp(-cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateUp(-cameraMovementSpeed);
            }

            if (inputService.IsKeyDown(Keys.E))
            {
                currentCamera.TranslateUp(cameraMovementSpeed);
                //userControlledEntity?.Transform.TranslateUp(cameraMovementSpeed);
            }


            /**********************************************************************
            /* Controls the type of the camera
            /*********************************************************************/

            if (inputService.IsKeyPressed(Keys.F1))
            {
                if (currentCamera.Type != CameraType.Free)
                {
                    currentCamera.SetFreeCamera();
                }
            }

            if (inputService.IsKeyPressed(Keys.F2))
            {
                // @TODO - think this mode through and implement
                /*UserControlledCamera.CameraType = CameraType.FirstPerson;
                      decimal radius = 2000;
                      SetFirstPersonCamera(UserControlledCamera, UserControlledCamera.Offset, UserControlledCamera.Rotation, radius);*/
            }

            if (inputService.IsKeyDown(Keys.F3))
            {
                if (cameraController.SetUserControlled(toolsService.SelectedEntity))
                {
                    toolsService.SelectedEntity = null;
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

            if (inputService.IsKeyPressed(Keys.F4))
            {
                if (currentCamera.Type != CameraType.ThirdPersonFreeAlt ||
                    currentCamera.Owner != userControlledEntity)
                {
                    currentCamera.SetThirdPersonCamera(userControlledEntity, new Vector3F(-10, 0, 0), CameraType.ThirdPersonFreeAlt);
                }
            }

            if (inputService.IsKeyPressed(Keys.F5))
            {
                if (currentCamera.Type != CameraType.ThirdPersonLocked ||
                    currentCamera.Owner != userControlledEntity)
                {
                    currentCamera.SetThirdPersonCamera(userControlledEntity,
                       new Vector3F(-10, 0, 0), CameraType.ThirdPersonLocked);
                }
            }

            // for camera look backwards
            if (inputService.IsKeyPressed(Keys.C))
            {
                if ((currentCamera.Type == CameraType.ThirdPersonFree) ||
                    (currentCamera.Type == CameraType.ThirdPersonFreeAlt))
                {
                    currentCamera.SetThirdPersonLookBackwards(true);
                }
            }

            if (inputService.IsKeyReleased(Keys.C))
            {
                if ((currentCamera.Type == CameraType.ThirdPersonFree) ||
                    (currentCamera.Type == CameraType.ThirdPersonFreeAlt))
                {
                    currentCamera.SetThirdPersonLookBackwards(false);
                }
            }

            // Final ViewMatrix update after all the changes
            currentCamera.Update(gameTime);

            if (inputService.IsKeyPressed(Keys.F11))
            {
                toolsService.SelectedEntity?.SetWireFrame();
            }

            if (inputService.IsKeyPressed(Keys.F12))
            {
                foreach (var window in gamePlatform.Windows)
                {
                    var filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".jpg";
                    window?.TakeScreenShot(filename, ImageFileType.Jpg);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".tga";
                    window?.TakeScreenShot(filename, ImageFileType.Tga);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".bmp";
                    window?.TakeScreenShot(filename, ImageFileType.Bmp);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".wmp";
                    window?.TakeScreenShot(filename, ImageFileType.Wmp);
                    filename = $"Screenshot_{window.Name}" + DateTime.Now.ToString("dd_MM_yyyy hh_mm_ss_ffff", CultureInfo.InvariantCulture) + ".tiff";
                    window?.TakeScreenShot(filename, ImageFileType.Tiff);
                }
            }

            if (inputService.IsKeyDown(Keys.F10))
            {
                //audioManager.Play();
            }

            if (inputService.IsKeyReleased(Keys.F10))
            {
                //audioManager.Stop();
            }
        }



        private void EntityManagerEntityRemoved(object sender, EntityEventArgs e)
        {
            var cameraController = Services.Get<CameraService>();
            if (e.Entity == cameraController.UserControlledCamera.Owner)
            {
                cameraController.UserControlledCamera.Type = CameraType.Free;
            }
        }
    }
}
