using System;
using System.Collections.Generic;
using Adamantium.EntityFramework.ComponentsBasics;

namespace Adamantium.EntityFramework
{
    public class GameContainer
   {
      public GameContainer()
      {
         Components = new Dictionary<Int64, List<Component>>();
      }
      public Entity EntityTree { get; set; }
      public Dictionary<Int64, List<Component>> Components { get; set; }
   }
}
