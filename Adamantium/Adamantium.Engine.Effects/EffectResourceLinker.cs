using System.Collections.Generic;
using Adamantium.Engine.Core.Effects;

namespace Adamantium.Engine.Graphics
{
   internal class EffectResourceLinker
   {
      /// <summary>
      /// Real object resources, as they were set on the parameter.
      /// </summary>
      public Dictionary<EffectData.Parameter, EffectConstantBuffer> ConstantBuffers;

      /// <summary>
      /// Total number of resources.
      /// </summary>
      public int Count;

      public Dictionary<EffectData.Parameter, SharpDX.Direct3D11.SamplerState[]> SamplerStates;
      public Dictionary<EffectData.Parameter, SharpDX.Direct3D11.ShaderResourceView[]> ShaderResourceViews;
      public Dictionary<EffectData.Parameter, SharpDX.Direct3D11.UnorderedAccessView[]> UnorderedAccessViews;
      public Dictionary<EffectData.Parameter, object> BoundResources;

      private static SharpDX.Direct3D11.ShaderResourceView[] EmptyResourceViews = new SharpDX.Direct3D11.ShaderResourceView[0];
      private static SharpDX.Direct3D11.SamplerState[] EmptySamplers = new SharpDX.Direct3D11.SamplerState[0];
      private static SharpDX.Direct3D11.UnorderedAccessView[] EmptyUAVs = new SharpDX.Direct3D11.UnorderedAccessView[0];

      /// <summary>
      /// Initializes this instance.
      /// </summary>
      public void Initialize()
      {
         ConstantBuffers = new Dictionary<EffectData.Parameter, EffectConstantBuffer>();

         SamplerStates = new Dictionary<EffectData.Parameter, SharpDX.Direct3D11.SamplerState[]>();
         ShaderResourceViews = new Dictionary<EffectData.Parameter, ShaderResourceView[]>();
         UnorderedAccessViews = new Dictionary<EffectData.Parameter, UnorderedAccessView[]>();
         BoundResources = new Dictionary<EffectData.Parameter, object>();
      }

      public T GetResource<T>(EffectData.Parameter resourceName) where T : class
      {
         object res;
         BoundResources.TryGetValue(resourceName, out res);
         return (T)res;
      }

      public T[] GetResources<T>(EffectData.Parameter resourceName) where T : class
      {
         object res;
         BoundResources.TryGetValue((EffectData.ResourceParameter)resourceName, out res);
         return (T[])res;
      }

      public ShaderResourceView[] GetShaderResources(EffectData.Parameter resourceName)
      {
         ShaderResourceView[] views;
         if (ShaderResourceViews.TryGetValue(resourceName, out views))
         {
            return views;
         }
         return EmptyResourceViews;
      }

      public SharpDX.Direct3D11.SamplerState[] GetSamplers(EffectData.Parameter resourceName)
      {
         SharpDX.Direct3D11.SamplerState[] samplers;
         if (SamplerStates.TryGetValue(resourceName, out samplers))
         {
            return samplers;
         }
         return EmptySamplers;
      }

      public UnorderedAccessView[] GetUAVs(EffectData.Parameter resourceName)
      {
         UnorderedAccessView[] uavs;
         if (UnorderedAccessViews.TryGetValue(resourceName, out uavs))
         {
            return uavs;
         }
         return EmptyUAVs;
      }

      public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type,
       UnorderedAccessView view)
      {
         ResolveResource(resourceName, type, view, 0);
      }

      public void SetResource<T>(EffectData.ResourceParameter paramDescription, EffectResourceType type, T value)
      {
         ResolveResource(paramDescription, type, value, 0);
      }

      public void SetResource<T>(EffectData.ResourceParameter resourceName, EffectResourceType type,
         params T[] valueArray) where T : class
      {
         for (int i = 0; i < valueArray.Length; ++i)
         {
            ResolveResource(resourceName, type, valueArray[i], i);
         }
      }

      public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type,
         UnorderedAccessView[] valueArray,
         int[] uavInitialCount)
      {
         for (int i = 0; i < valueArray.Length; ++i)
         {
            ResolveResource(resourceName, type, valueArray[i], i);
         }
      }

      private void ResolveResource(EffectData.Parameter resourceName, EffectResourceType type, object value, int index)
      {
         switch (type)
         {
            case EffectResourceType.ConstantBuffer:
               ProcessConstantBuffer(resourceName, value);
               break;
            case EffectResourceType.SamplerState:
            case EffectResourceType.ShaderResourceView:
            case EffectResourceType.UnorderedAccessView:
               ProcessReferenceResources((EffectData.ResourceParameter)resourceName, type, value, index);
               break;
         }
      }

      private void ProcessConstantBuffer(EffectData.Parameter resourceName, object value)
      {
         var constantBuffer = value as EffectConstantBuffer;
         if (ConstantBuffers.ContainsKey(resourceName))
         {
            ConstantBuffers[resourceName] = constantBuffer;
            BoundResources[resourceName] = constantBuffer;
         }
         else
         {
            ConstantBuffers.Add(resourceName, constantBuffer);
            BoundResources.Add(resourceName, constantBuffer);
         }
      }

      private void ProcessReferenceResources(EffectData.ResourceParameter parameter, EffectResourceType type,
         object value, int index)
      {
         if (index >= parameter.Count)
         {
            return;
         }

         switch (type)
         {
            case EffectResourceType.SamplerState:
               {

                  SharpDX.Direct3D11.SamplerState[] states = null;
                  if (!SamplerStates.TryGetValue(parameter, out states))
                  {
                     states = new SharpDX.Direct3D11.SamplerState[parameter.Count];
                     SamplerStates.Add(parameter, states);
                     BoundResources.Add(parameter, states);
                  }

                  SharpDX.Direct3D11.SamplerState state = null;
                  if (value is SamplerState)
                  {
                     state = value as SamplerState;
                  }
                  else if (value is SharpDX.Direct3D11.SamplerState)
                  {
                     state = value as SharpDX.Direct3D11.SamplerState;
                  }
                  states[index] = state;

               }
               break;
            case EffectResourceType.ShaderResourceView:
               {
                  ShaderResourceView[] views = null;
                  if (!ShaderResourceViews.TryGetValue(parameter, out views))
                  {
                     views = new ShaderResourceView[parameter.Count];
                     ShaderResourceViews.Add(parameter, views);
                     BoundResources.Add(parameter, views);
                  }

                  ShaderResourceView view = null;
                  if (value is ShaderResourceView)
                  {
                     view = value as ShaderResourceView;
                  }
                  else if (value is Texture)
                  {
                     view = value as Texture;
                  }
                  else if (value is Buffer)
                  {
                     view = value as Buffer;

                  }
                  views[index] = view;
               }
               break;
            case EffectResourceType.UnorderedAccessView:
               {
                  UnorderedAccessView[] uavs = null;

                  if (!UnorderedAccessViews.TryGetValue(parameter, out uavs))
                  {
                     uavs = new UnorderedAccessView[parameter.Count];
                     UnorderedAccessViews.Add(parameter, uavs);
                     BoundResources.Add(parameter, uavs);
                  }

                  UnorderedAccessView view = null;
                  if (value is UnorderedAccessView)
                  {
                     view = value as UnorderedAccessView;
                  }
                  else if (value is Texture)
                  {
                     view = value as Texture;
                  }
                  else if (value is Buffer)
                  {
                     view = value as Buffer;
                  }

                  uavs[index] = view;
               }
               break;
         }
      }
   }
}
