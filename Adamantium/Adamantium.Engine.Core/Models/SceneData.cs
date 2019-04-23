using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public SceneData()
      {
         Models = new Model();
         meshesDictionary = new Dictionary<String, Model>();
         meshToId = new Dictionary<string, Model>();

         Images = new ImageCollection();
         CameraData = new CameraCollection();
         LightData = new LightCollection();
         Animation = new AnimationCollection();
         Materials = new MaterialCollection();
         Controllers = new ControllerCollection();
         Skeletons = new SkeletonCollection();
         Units = new Unit();
      }

      public Model CreateMesh(Model parent, String id = "", String name = "")
      {
         lock (this)
         {
            Model meshdata = null;
            if (!meshesDictionary.ContainsKey(name + id))
            {
               meshdata = new Model(parent, id, name);
               meshesDictionary.Add(meshdata.ToString(), meshdata);
               if (!meshToId.ContainsKey(id))
               {
                  meshToId.Add(id, meshdata);
               }

               parent?.AddDependency(meshdata);
            }
            else
            {
               meshdata = meshesDictionary[name + id];
            }

            return meshdata;
         }
      }

      /// <summary>
      /// Removes a <see cref="Model"/> and all its sub meshes
      /// </summary>
      /// <param name="mesh"></param>
      public void RemoveMesh(Model mesh)
      {
         lock (this)
         {
            Stack<Model> stack = new Stack<Model>();
            stack.Push(mesh);
            while (stack.Count > 0)
            {
               var current = stack.Pop();

               if (meshesDictionary.ContainsKey(mesh.ToString()))
               {
                  meshesDictionary.Remove(mesh.ToString());
               }

               if (meshToId.ContainsKey(mesh.ID))
               {
                  meshToId.Remove(mesh.ID);
               }

               foreach (var mesh1 in current.Dependencies)
               {
                  stack.Push(mesh1);
               }
            }

            if (mesh.Parent != null)
            {
               mesh.Parent.Dependencies.Remove(mesh);
            }
            else
            {
                Models = null;
            }
         }
      }

      public Model GetModelByName(String name)
      {
         lock (this)
         {
            if (meshesDictionary.ContainsKey(name))
            {
               return meshesDictionary[name];
            }
            return null;
         }
      }

      public Model GetModelByID(String id)
      {
         lock (this)
         {
            if (meshToId.ContainsKey(id))
            {
               return meshToId[id];
            }
            return null;
         }
      }

      public String Name { get; set; }
      public Unit Units { get; set; }

      private readonly Dictionary<String, Model> meshesDictionary;
      private readonly Dictionary<String, Model> meshToId; 

      public Model Models { get; set; }
      public ImageCollection Images { get; set; }
      public ControllerCollection Controllers { get; set; }
      public MaterialCollection Materials { get; set; }
      public CameraCollection CameraData { get; set; }
      public LightCollection LightData { get; set; }
      public AnimationCollection Animation { get; set; }
      /// <summary>
      /// List of Hierarchical joints. One root for one skeleton
      /// </summary>
      public SkeletonCollection Skeletons { get; set; }
   }
}
