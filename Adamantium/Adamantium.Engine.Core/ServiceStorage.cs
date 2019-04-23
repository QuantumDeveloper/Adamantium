using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core
{
   /// <summary>
   /// Class stores different objects as a game services to get them later from the game
   /// </summary>
   public class ServiceStorage:IServiceStorage
   {
      private Dictionary<Type, Object> services = new Dictionary<Type, Object>();

      /// <summary>
      /// Event firing when service added
      /// </summary>
      public event EventHandler ServiceAdded;

      /// <summary>
      /// Event firing when service removed
      /// </summary>
      public event EventHandler ServiceRemoved;

      /// <summary>
      /// Add service to the list of services
      /// </summary>
      /// <param name="type">type of added service</param>
      /// <param name="service">Any object you want to add as service</param>
      /// <exception cref="NullReferenceException">Null reference exception if type or service is null</exception>
      public void Add(Type type, Object service)
      {
         if (type == null)
         {
            throw  new NullReferenceException("type is null");
         }

         if (service == null)
         {
            throw new NullReferenceException("service is null");
         }

         lock (service)
         {
            if (!services.ContainsKey(type))
            {
               services.Add(type, service);
               ServiceAdded?.Invoke(this, new ServiceEventArgs(service));
            }
         }
      }

      /// <summary>
      /// Add service to the list of services
      /// </summary>
      /// <param name="service">Any object you want to add as service</param>
      /// <exception cref="NullReferenceException">Null reference exception if type or service is null</exception>
      public void Add<T>(T service)
      {
         if (typeof(T) == null)
         {
            throw new NullReferenceException("type is null");
         }

         if (service == null)
         {
            throw new NullReferenceException("service is null");
         }

         lock (services)
         {
            if (!services.ContainsKey(typeof(T)))
            {
               services.Add(typeof(T), service);
               ServiceAdded?.Invoke(this, new ServiceEventArgs(service));
            }
         }
      }

      /// <summary>
      /// Remove service of requested type
      /// </summary>
      /// <param name="type">type to remove</param>
      /// <exception cref="NullReferenceException">Null reference exception if type is null</exception>
      public void Remove(Type type)
      {
         if (type == null)
         {
            throw new NullReferenceException("type is null");
         }

         lock (services)
         {
            if (services.ContainsKey(type))
            {
               var service = services[type];
               services.Remove(type);
               ServiceRemoved?.Invoke(this, new ServiceEventArgs(service));
            }
         }
      }

      /// <summary>
      /// Get service of requested type
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <returns>Returns requested type</returns>
      /// <exception cref="ArgumentException">ArgumentException if there is no requested type</exception>
      public T Get<T>()
      {
         return (T)Get(typeof (T));
      }


      public object Get(Type serviceType)
      {
         lock (services)
         {
            if (services.ContainsKey(serviceType))
            {
               return services[serviceType];
            }
         }
         throw new ArgumentException($"Service of type {serviceType} is not registered.");
      }
   }
}
