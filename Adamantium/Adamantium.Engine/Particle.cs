using System;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using SharpDX.Direct3D11;
using Tex2D = SharpDX.Direct3D11.Texture2D;

namespace Adamantium.Engine
{
   public class Particle
   {
      private Tex2D Texture { get; set; }
      public ShaderResourceView TextureView { get; set; }
      public Vector2F Position { get; set; }
      public Vector2F Velocity { get; set; }
      public float Angle { get; set; }
      public float AngularVelocity { get; set; }
      public Color Color { get; set; }
      public float Size { get; set; }
      public int TTL { get; set; }

      public Particle(ShaderResourceView texture, Vector2F position, Vector2F velocity, Single size, int ttl)
      {
         TextureView = texture;
         Position = position;
         Velocity = velocity;
         Size = size;
         TTL = ttl;
      }

      public void Update()
      {
         TTL--;
         Position += Velocity;
      }

      public void Draw(SpriteBatch spriteBatch)
      {
         //spriteBatch.Draw(TextureView, Position,);
      }
   }
}
