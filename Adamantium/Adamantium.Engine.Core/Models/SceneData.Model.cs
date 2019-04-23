using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      //Класс для хранения меша, собранного из текстурных координат, вершин и нормалей.
      //Содержит в себе всю информацию о геометрии, положении меша в пространстве и его видимости, а так же анимацию, если она присутствует.
      public class Model
      {
         public Model(Model parent = null, String id = "", String name = "")
         {
            ID = id;
            Name = name;

            Parent = parent;
            Dependencies = new List<Model>();
            Meshes = new List<Mesh>();
            Rotation = QuaternionF.Identity;
            Scale = Vector3F.One;
         }

         internal void AddDependency(Model data)
         {
            if (!Dependencies.Contains(data))
            {
               Dependencies.Add(data);
            }
         }

         public override string ToString()
         {
            return Name + ID;
         }

         public Model Parent { get; set; }

         public List<Model> Dependencies { get; set; }

         public String Name { get; }

         public String ID { get; }

         public Vector3F Scale { get; set; }

         public Vector3F Position { get; set; }

         public QuaternionF Rotation { get; set; }

         public List<Mesh> Meshes { get; set; }

        }
   }
}
