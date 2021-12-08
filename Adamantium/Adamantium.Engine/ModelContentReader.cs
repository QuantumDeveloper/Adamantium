using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Compiler.Converter;
using Adamantium.Engine.Compiler.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;
using Vector3 = Adamantium.Mathematics.Vector3;

namespace Adamantium.Engine
{
   public class ModelContentReader:GraphicsResourceContentReader<Entity>
   {
       protected override async Task<Entity> ReadContentAsync(IContentManager contentManager, GraphicsDevice graphicsDevice,
         ContentReaderParameters parameters)
      {
         var converter = contentManager.ServiceProvider.Resolve<ModelConverter>();
         if (converter == null)
            throw new InvalidOperationException("Unable to retrieve a ModelConverter service provider");

         var entityWorld = contentManager.ServiceProvider.Resolve<EntityWorld>();
         if (entityWorld == null)
            throw new InvalidOperationException("Unable to retrieve EntityWorld service provider");
         //var camera = contentManager.ServiceProvider.Resolve<CameraService>().UserControlledCamera;
         var camera = contentManager.ServiceProvider.Resolve<CameraService>().Cameras.FirstOrDefault();
         if (parameters.AssetName.EndsWith(".aemf"))
         {
            return await entityWorld.CreateEntityFromTemplate(new EntityLoadTemplate(parameters.AssetPath, contentManager, graphicsDevice, Vector3.Zero));
         }
         else
         {
            var timer = Stopwatch.StartNew();
            var scene = converter.ImportFileAsync(parameters.AssetPath);
            timer.Stop();
            //MessageBox.Show("Time: "+timer.Elapsed);
            var initialPosition = Vector3F.Zero;
            return await entityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, contentManager, initialPosition));
         }
      }
   }
}
