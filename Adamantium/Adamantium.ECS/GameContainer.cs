using System;
using System.Collections.Generic;
using Adamantium.ECS.Components;

namespace Adamantium.ECS
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
