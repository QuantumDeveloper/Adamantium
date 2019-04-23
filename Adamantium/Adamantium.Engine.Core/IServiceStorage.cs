using System;

namespace Adamantium.Engine.Core
{
   /// <summary>
   /// A service registry is a <see cref="IServiceProvider"/> that provides methods to register and unregister services.
   /// </summary>
   public interface IServiceStorage
   {
      /// <summary>
      /// Event firing when service added
      /// </summary>
      event EventHandler ServiceAdded;

      /// <summary>
      /// Event firing when service removed
      /// </summary>
      event EventHandler ServiceRemoved;

      /// <summary>
      /// Gets the service object of specified type. The service must be registered with the <typeparamref name="T"/> type key.
      /// </summary>
      /// <remarks>This method will thrown an exception if the service is not registered, it null value can be accepted - use the <see cref="IServiceProvider.GetService"/> method.</remarks>
      /// <typeparam name="T">The type of the service to get.</typeparam>
      /// <returns>The service instance.</returns>
      /// <exception cref="ArgumentException">Is thrown when the corresponding service is not registered.</exception>
      T Get<T>();

      /// <summary>
      /// Gets the service object of specified type. The service must be registered with the <typeparamref name="T"/> type key.
      /// </summary>
      /// <param name="serviceType">Type of the service to Get from storage</param>
      /// <remarks>This method will thrown an exception if the service is not registered, it null value can be accepted - use the <see cref="IServiceProvider.GetService"/> method.</remarks>
      /// <returns>The service instance.</returns>
      /// <exception cref="ArgumentException">Is thrown when the corresponding service is not registered.</exception>
      object Get(Type serviceType);

      /// <summary>
      /// Adds a service to this service provider.
      /// </summary>
      /// <param name="type">The type of service to add.</param>
      /// <param name="provider">The instance of the service provider to add.</param>
      /// <exception cref="System.ArgumentNullException">Service type cannot be null</exception>
      /// <exception cref="System.ArgumentException">Service is already registered</exception>
      void Add(Type type, object provider);

      /// <summary>
      /// Adds a service to this service provider.
      /// </summary>
      /// <typeparam name="T">The type of the service to add.</typeparam>
      /// <param name="provider">The instance of the service provider to add.</param>
      /// <exception cref="System.ArgumentNullException">Service type cannot be null</exception>
      /// <exception cref="System.ArgumentException">Service is already registered</exception>
      void Add<T>(T provider);

      /// <summary>
      /// Removes the object providing a specified service.
      /// </summary>
      /// <param name="type">The type of service.</param>
      void Remove(Type type);

   }
}
