using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.EntityFramework.Templates;
using Adamantium.Mathematics;
using Adamantium.Win32;
using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.Engine.Templates
{
    public class EntityImportTemplate : IEntityTemplate
    {
        private SceneData sceneData;
        private IContentManager contentManager;
        private EntityFramework.Components.Camera _camera;

        public EntityImportTemplate(SceneData sceneData, IContentManager manager, EntityFramework.Components.Camera camera)
        {
            this.sceneData = sceneData;
            contentManager = manager;
            _camera = camera;
        }

        public Task<Entity> BuildEntity(Entity owner)
        {
            return ImportEntity(owner);
        }

        private Task<Entity> ImportEntity(Entity owner)
        {
            try
            {
                if (sceneData == null)
                {
                    return null;
                }

                Dictionary<String, Entity> nameToEntityId = new Dictionary<string, Entity>();
                owner.Name = sceneData.Name;
                nameToEntityId.Add(sceneData.Models.ToString(), owner);
                Stack<SceneData.Model> stack = new Stack<SceneData.Model>();
                stack.Push(sceneData.Models);
                while (stack.Count > 0)
                {
                    Entity entity = null;
                    var currentMesh = stack.Pop();
                    if (currentMesh.Parent == null)
                    {
                        entity = owner;
                    }
                    else
                    {
                        if (nameToEntityId.TryGetValue(currentMesh.Parent.ToString(), out var parentEntity))
                        {
                            entity = new Entity(parentEntity, currentMesh.ToString());
                            nameToEntityId.Add(currentMesh.ToString(), entity);
                        }
                    }

                    if (currentMesh.Meshes.Count == 1)
                    {
                        var mesh = currentMesh.Meshes[0];
                        WriteMeshData(entity, mesh);
                    }
                    //Если меш разбит на части, тогда создаём дополнительные энтити, которые будут дочерними по отношению к текущему
                    else
                    {
                        int count = 1;
                        foreach (var mesh in currentMesh.Meshes)
                        {
                            var entityPart = new Entity(entity, $"{currentMesh.Name} ({count})");
                            count++;
                            WriteMeshData(entityPart, mesh);

                            SetTransformation(entityPart, currentMesh.Position,
                               currentMesh.Scale * new Vector3F(sceneData.Units.Value), currentMesh.Rotation);
                        }
                    }

                    SetTransformation(entity, currentMesh.Position, currentMesh.Scale * new Vector3F(sceneData.Units.Value),
                       currentMesh.Rotation);

                    if (sceneData.Controllers.ContainsKey(currentMesh.ID))
                    {
                        var controllerData = sceneData.Controllers[currentMesh.ID];
                        AnimationController controller = new AnimationController();
                        controller.BindShapeMatrix = controllerData.BindShapeMatrix;
                        controller.ControllerId = controllerData.Name;
                        controller.JointDictionary = controllerData.JointDictionary;

                        entity.AddComponent(controller);
                    }

                    foreach (var newEntity in currentMesh.Dependencies)
                    {
                        stack.Push(newEntity);
                    }
                }

                if (sceneData.Animation.Count > 0)
                {
                    AnimationComponent animation = new AnimationComponent();
                    animation.Animations = sceneData.Animation;
                    animation.Skeletons = sceneData.Skeletons;

                    owner.AddComponent(animation);
                }

                CalculateBoundBoxes(owner);
                var collider = owner.GetComponent<Collider>();
                owner.Transform.SetPosition(owner.GetPositionForNewObject(_camera, Vector3F.Max(collider.Bounds.Size)));
                return Task.FromResult(owner);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace);
            }
            return null;
        }


        private void CalculateBoundBoxes(Entity root)
        {
            List<Entity> roots = new List<Entity>();
            Queue<Entity> queue = new Queue<Entity>();
            queue.Enqueue(root);
            if (root.Dependencies.Count > 0)
            {
                roots.Add(root);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var entity in current.Dependencies)
                {
                    if (entity.Dependencies.Count > 0)
                    {
                        queue.Enqueue(entity);
                        roots.Add(entity);
                    }
                }
            }

            for (int i = roots.Count - 1; i >= 0; --i)
            {
                var rootCollider = roots[i].GetOrCreateComponent<BoxCollider>();
                foreach (var entity in roots[i].Dependencies)
                {
                    var collider = entity.GetComponent<Collider>();
                    rootCollider.Merge(collider);
                }
            }
        }

        private static void SetTransformation(Entity entity, Vector3D pos, Vector3F scale, QuaternionF rot)
        {
            var transform = new Transform();
            transform.Position = pos;
            transform.Rotation = rot;
            transform.BaseScale = scale;
            entity.AddComponent(transform);
        }

        private void WriteMeshData(Entity entity, Mesh mesh)
        {
            MeshData meshData = new MeshData();
            meshData.Mesh = mesh;
            entity.AddComponent(meshData);

            Collider collider = new BoxCollider();
            entity.AddComponent(collider);
            collider.Initialize();

            if (mesh.Semantic.HasFlag(VertexSemantic.JointIndices))
            {
                var renderer = new SkinnedMeshRenderer();
                entity.AddComponent(renderer);
            }
            else
            {
                var renderer = new MeshRenderer();
                entity.AddComponent(renderer);
            }

            SceneData.Material materialData;
            if (!String.IsNullOrEmpty(mesh.MaterialID))
            {
                sceneData.Materials.TryGetValue(mesh.MaterialID, out materialData);
                if (materialData != null)
                {
                    Material material = new Material();
                    material.AmbientColor = materialData.AmbientColor;
                    material.DiffuseColor = materialData.DiffuseColor;
                    materialData.SpecularColor = materialData.SpecularColor;
                    material.Emission = materialData.Emission;
                    material.Reflective = materialData.Reflective;
                    material.Reflectivity = materialData.Reflectivity;
                    material.RefractionIndex = materialData.RefractionIndex;
                    material.Shininess = materialData.Shininess;
                    material.Transparent = materialData.Transparent;
                    material.Transparency = materialData.Transparency;

                    if (!String.IsNullOrEmpty(materialData.DiffuseMap))
                    {
                        material.TexturePath = sceneData.Images[materialData.DiffuseMap].FilePath;
                        if (File.Exists(material.TexturePath))
                        {
                            ContentLoadOptions options = new ContentLoadOptions()
                            {
                                AllowDuplication = false,
                                IgnoreRootDirectory = true
                            };
                            material.Texture = contentManager.Load<Texture2D>(material.TexturePath, options);
                        }
                    }
                    entity.AddComponent(material);
                }
                else
                {
                    Material material = new Material();
                    entity.AddComponent(material);
                }
            }
            else
            {
                Material material = new Material();
                entity.AddComponent(material);
            }
        }
    }
}
