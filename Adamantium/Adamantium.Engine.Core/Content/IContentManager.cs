using System;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.Engine.Core.Content
{
   public interface IContentManager
   {
      /// <summary>
      /// Gets the service provider associated with the ContentManager.
      /// </summary>
      /// <value>The service provider.</value>
      /// <remarks>
      /// The service provider can be used by some <see cref="IContentReader"/> when for example a <see cref="Adamantium.Engine.Graphics.GraphicsDevice"/> needs to be 
      /// used to instantiate a content.
      /// </remarks>
      IDependencyResolver ServiceProvider { get; }
      
      /// <summary>
      /// Checks if the specified assets exists.
      /// </summary>
      /// <param name="assetName">The asset name with extension.</param>
      /// <returns><c>true</c> if the specified assets exists, <c>false</c> otherwise</returns>
      bool Exists(string assetName);

      /// <summary>
      /// Loads an asset that has been processed by the Content Pipeline.  Reference page contains code sample.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="assetName">Full asset name (with its extension)</param>
      /// <param name="options">The options to pass to the content reader (null by default).</param>
      /// <returns>``0.</returns>
      /// <exception cref="AssetNotFoundException">If the asset was not found from all <see cref="IContentResolver" />.</exception>
      /// <exception cref="System.NotSupportedException">If no content reader was suitable to decode the asset.</exception>
      T Load<T>(string assetName, object options = null);

      /// <summary>
      /// Loads an asset that has been processed by the Content Pipeline.  Reference page contains code sample.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="assetName">Full asset name (with its extension)</param>
      /// <param name="options">The options to pass to the content reader (null by default).</param>
      /// <returns>``0.</returns>
      /// <exception cref="AssetNotFoundException">If the asset was not found from all <see cref="IContentResolver" />.</exception>
      /// <exception cref="System.NotSupportedException">If no content reader was suitable to decode the asset.</exception>
      Task<T> LoadAsync<T>(string assetName, object options = null);

      /// <summary>
      /// Loads an asset that has been processed by the Content Pipeline.  Reference page contains code sample.
      /// </summary>
      /// <param name="assetType">Asset Type</param>
      /// <param name="assetName">Full asset name (with its extension)</param>
      /// <param name="options">The options to pass to the content reader (null by default).</param>
      /// <returns>Asset</returns>
      /// <exception cref="AssetNotFoundException">If the asset was not found from all <see cref="IContentResolver" />.</exception>
      /// <exception cref="System.NotSupportedException">If no content reader was suitable to decode the asset.</exception>
      object Load(Type assetType, string assetName, object options = null);

      /// <summary>
      /// Unloads all data that was loaded by this ContentManager. All data will be disposed.
      /// </summary>
      /// <remarks>
      /// Unlike <see cref="ContentManager.Load{T}"/> method, this method is not thread safe and must be called by a single caller at a single time.
      /// </remarks>
      void Unload();

      /// <summary>
      ///	Unloads and disposes an asset.
      /// </summary>
      /// <param name="assetName">The asset name</param>
      /// <returns><c>true</c> if the asset exists and was unloaded, <c>false</c> otherwise.</returns>
      bool Unload<T>(string assetName);

      /// <summary>
      /// Unloads and disposes an asset.
      /// </summary>
      /// <param name="assetType">The asset type</param>
      /// <param name="assetName">The asset name</param>
      /// <returns><c>true</c> if the asset exists and was unloaded, <c>false</c> otherwise.</returns>
      bool Unload(Type assetType, string assetName);
   }
}
