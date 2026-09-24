using Adamantium.Core;
using Adamantium.Engine.Rendering;
using Adamantium.Engine.Templates.CameraTemplates;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Game.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Managers;

/// <summary>
/// Cameras as objects in the scene, for the editor: an icon where each camera stands - picked to select it and drag
/// it - and a frustum showing where the selected one looks. A game has no use for any of it.
/// </summary>
public class CameraGizmo
{
    private const float DefaultZNear = 0.1f;
    private const float DefaultZFar = 1000000.0f;

    private readonly EntityWorld entityWorld;
    private readonly Observatory observatory;

    private Entity CameraIcon;
    private Entity CameraVisual;
    private MeshData cameraIconRenderer;

    private Entity SelectedCamera;

    private static int cameraNumber = 1;


    public CameraGizmo(EntityWorld entityWorld, Observatory observatory)
    {
        this.entityWorld = entityWorld;
        this.observatory = observatory;
        CreateCameraIcon();
        CreateCameraVisual();
    }

    private void CreateCameraIcon()
    {
        CameraIcon = new CameraIconTemplate().BuildEntity(null, "Camera icon");
        cameraIconRenderer = CameraIcon.GetComponent<MeshData>();
    }

    private void CreateCameraVisual()
    {
        CameraVisual = new CameraVisualTemplate().BuildEntity(null, "Camera Debug");
    }

    public void SetSelected(Entity camera)
    {
        SelectedCamera = camera;
    }

    /// <summary>
    /// Adds a camera to <paramref name="output"/> just in front of the one it looks through, and makes it current.
    /// </summary>
    public Camera CreateCamera(UniverseOutput output, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = $"Camera ({cameraNumber})";
            cameraNumber++;
        }

        var position = Vector3.Zero;
        var current = output.Camera;
        if (current != null)
        {
            position = (CameraIcon.GetDiameter() * 2 * (Vector3)current.Forward) + current.GetOwnerPosition();
        }

        var entity = new CameraTemplate().BuildEntity(null, name, position, Vector3.ForwardLH, -Vector3.Up, output.Width, output.Height, DefaultZNear, DefaultZFar);
        entityWorld.EntityManager.AddEntity(entity);
        var camera = entity.GetComponent<Camera>();
        output.Camera = camera;
        return camera;
    }

    /// <summary>
    /// Picks a camera icon under the pointer, as seen through the camera of the output the pointer is over.
    /// </summary>
    public CollisionResult Intersects(CollisionMode collisionMode)
    {
        var output = observatory.PointerOutput;
        var camera = output.Camera;
        var cursorPosition = output.Input.RelativePosition;
        CollisionResult collisionResult = new CollisionResult(CompareOrder.Less);
        var projectionMatrix = camera.ProjectionMatrix;
        var sceneCameras = observatory.AllCameras;
        for (int i = 0; i < sceneCameras.Count; i++)
        {
            var currentCamera = sceneCameras[i];
            if (currentCamera == camera || !currentCamera.Owner.IsEnabled)
            {
                continue;
            }

            var transform = currentCamera.Owner.Transform.GetMetadata(camera);
            var billboard = Matrix4x4F.BillboardLH(transform.RelativePosition, Vector3F.Zero, camera.Up, camera.Forward);
            var rotation = MathHelper.GetRotationFromMatrix(billboard);
            var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);
            var ray = Collisions.CalculateRay(cursorPosition, camera, world, projectionMatrix, true);

            var collision = CameraIcon.GetComponent<Collider>();
            if (collision != null)
            {
                Vector3F point;
                var intersects = collision.Intersects(ref ray, out point);
                if (intersects)
                {
                    collisionResult.ValidateAndSetValues(currentCamera.Owner, (Vector3)point, true);
                }
            }
        }
        if (collisionResult.Intersects)
        {
            SelectedCamera = collisionResult.Entity;
        }
        return collisionResult;
    }

    public void DrawCameraIcons(Effect effect, Camera camera, GraphicsDevice drawingContext, MeshGeometryCache geometryCache, AppTime gameTime)
    {
        var view = camera.ViewMatrix;
        var proj = camera.ProjectionMatrix;
        effect.Parameters["viewMatrix"].SetValue(view);
        effect.Parameters["projectionMatrix"].SetValue(proj);

        var sceneCameras = observatory.AllCameras;
        for (int i = 0; i < sceneCameras.Count; i++)
        {
            var currentCamera = sceneCameras[i];
            if (currentCamera == camera || !currentCamera.Owner.IsEnabled)
            {
                continue;
            }

            var transform = currentCamera.Owner.Transform.GetMetadata(camera);
            if (transform.RelativePosition.Length() < CameraIcon.GetDiameter())
            {
                continue;
            }

            var transparency = 1 - (1 / transform.RelativePosition.Length());

            var billboard = Matrix4x4F.BillboardRH(transform.RelativePosition, Vector3F.Zero, camera.Up, camera.Forward);
            var rotation = MathHelper.GetRotationFromMatrix(billboard);
            var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);

            effect.Parameters["transparency"].SetValue(transparency);
            effect.Parameters["worldMatrix"].SetValue(world);
            effect.Parameters["wvp"].SetValue(world * view * proj);
            effect.Parameters["meshColor"].SetValue(Colors.White.ToVector3());
            effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
            geometryCache.DrawMesh(drawingContext, cameraIconRenderer);
        }

        effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
    }

    public void DrawDebugCamera(Effect effect, Camera camera, GraphicsDevice drawingContext, MeshGeometryCache geometryCache, AppTime gametime)
    {
        if (SelectedCamera == null || !SelectedCamera.IsEnabled)
        {
            return;
        }

        var cameraRender = CameraVisual.GetComponent<MeshData>();
        var view = camera.ViewMatrix;
        var proj = camera.ProjectionMatrix;
        effect.Parameters["viewMatrix"].SetValue(view);
        effect.Parameters["projectionMatrix"].SetValue(proj);

        if (SelectedCamera == camera.Owner)
        {
            return;
        }

        var transform = SelectedCamera.Transform.GetMetadata(camera);
        var world = Matrix4x4F.RotationQuaternion(camera.Rotation) * transform.WorldMatrixF;
        effect.Parameters["transparency"].SetValue(1.0f);
        effect.Parameters["worldMatrix"].SetValue(world);
        effect.Parameters["wvp"].SetValue(world * view * proj);
        effect.Parameters["meshColor"].SetValue(Colors.Beige.ToVector3());
        effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
        geometryCache.DrawMesh(drawingContext, cameraRender);

        effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
    }
}
