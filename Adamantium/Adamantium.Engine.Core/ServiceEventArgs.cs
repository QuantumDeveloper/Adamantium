using System;

namespace Adamantium.Engine.Core
{
   class ServiceEventArgs :EventArgs
   {
      public Object Service { get; internal set; }
      public ServiceEventArgs(Object service)
      {
         Service = service;
      }
   }
}
