using System;
using System.IO;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Templates;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates
{
   public class EntityLoadTemplate:IEntityTemplate
   {
      private String pathToFile;
      private IContentManager manager;
      private GraphicsDevice graphicsDevice;
      private Vector3 initialPosition;
      public EntityLoadTemplate(String pathToFile, IContentManager manager, GraphicsDevice graphicsDevice, Vector3 initialPosition)
      {
         this.pathToFile = pathToFile;
         this.manager = manager;
         this.graphicsDevice = graphicsDevice;
         this.initialPosition = initialPosition;
      }

      public Task<Entity> BuildEntity(Entity owner)
      {
         return LoadEntityFromFile(owner);
      }

      private Task<Entity> LoadEntityFromFile(Entity owner)
      {
         var input = File.ReadAllBytes(pathToFile);
         GameContainer container = null;
         /*
         try
         {
            container = (GameContainer)ModelDataSerializationScheme.ModelDataScheme.Deserialize(input, container, typeof(GameContainer));
         }
         catch (Exception exception)
         {
            MessageBox.Show(exception.Message + exception.StackTrace);
         }
         finally
         {
            input.Dispose();
         }
         
         if (container != null)
         {
            
            Stack<Entity> stack = new Stack<Entity>();
            stack.Push(container.EntityTree);
            while (stack.Count > 0)
            {
               Entity current = stack.Pop();
               entityWorld.AddEntity(current);
               var transform = new Transform();
               transform.InitialPosition = initialPosition;
               
               
               if (container.Components.ContainsKey(entityId))
               {
                  var components = container.Components[entityId];
                  foreach (var entityComponent in components)
                  {
                     if (entityComponent is MeshData)
                     {
                        var arguments = entityComponent.GetType().GetGenericArguments();
                        if (arguments[0] == typeof(MeshData))
                        {
                           MeshData component = entityComponent as MeshData;
                           //var vert = Buffer.Vertex.New(graphicsDevice, component.Vertex, ResourceUsage.Dynamic);
                           //var index = Buffer.Index.New(graphicsDevice, component.Indices.ToArray(), ResourceUsage.Dynamic);
                           //SkinnedGeometry geometry = new SkinnedGeometry(vert, index, component.PrimitiveTopology);
                           //current.AddComponent(geometry);
                        }
                     }

                     Components.Physics.Collision collision = entityComponent as Components.Physics.Collision;
                     if (collision != null)
                     {
                        current.AddComponent(collision);
                        var boundCorners = current.CalculateCorners(graphicsDevice, BoundingBoxVariant.FullBox);
                        current.AddComponent(boundCorners);
                     }

                     AnimationComponent animation = entityComponent as AnimationComponent;
                     if (animation != null)
                     {
                        current.AddComponent(animation);
                     }
                     AnimationController controller = entityComponent as AnimationController;
                     if (controller != null)
                     {
                        current.AddComponent(controller);
                     }
                  }
               }

               foreach (var newEntity in current.Dependencies)
               {
                  stack.Push(newEntity);
               }
               
            }
         }*/
         return Task.FromResult(container?.EntityTree);
      }
   }
}
