using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Compiler.Converter.Parsers
{
   /*
     0x4D4D // Main Chunk
      ├─ 0x0002 // M3D Version
      ├─ 0x3D3D // 3D Editor Chunk
      │  ├─ 0x4000 // Object Block
      │  │  ├─ 0x4100 // Triangular Mesh
      │  │  │  ├─ 0x4110 // Vertices List
      │  │  │  ├─ 0x4120 // Faces Description
      │  │  │  │  ├─ 0x4130 // Faces Material
      │  │  │  │  └─ 0x4150 // Smoothing Group List
      │  │  │  ├─ 0x4140 // Mapping Coordinates List
      │  │  │  └─ 0x4160 // Local Coordinates System
      │  │  ├─ 0x4600 // Light
      │  │  │  └─ 0x4610 // Spotlight
      │  │  └─ 0x4700 // Camera
      │  └─ 0xAFFF // Material Block
      │     ├─ 0xA000 // Material Name
      │     ├─ 0xA010 // Ambient Color
      │     ├─ 0xA020 // Diffuse Color
      │     ├─ 0xA030 // Specular Color
      │     ├─ 0xA200 // Texture Map 1
      │     ├─ 0xA230 // Bump Map
      │     └─ 0xA220 // Reflection Map
      │        │  // Sub Chunks For Each Map //
      │        ├─ 0xA300 // Mapping Filename
      │        └─ 0xA351 // Mapping Parameters
      └─ 0xB000 // Keyframer Chunk
         ├─ 0xB002 // Mesh Information Block
         ├─ 0xB007 // Spot Light Information Block
         └─ 0xB008 // Frames (Start and End)
            ├─ 0xB010 // Object Name
            ├─ 0xB013 // Object Pivot Point
            ├─ 0xB020 // position Track
            ├─ 0xB021 // Rotation Track
            ├─ 0xB022 // Scale Track
            └─ 0xB030 // Hierarchy position
   */

   public class Autodesk3DSFileParser : ModelFileParser
   {
      public Autodesk3DSFileParser(string filePath) : base(filePath)
      {

      }

      private Autodesk3DsDataContainer dataContainer;
      private FileStream fileStream;
      private BinaryReader binaryReader;

      protected override DataContainer ParseData(ConversionConfig config)
      {
         dataContainer = new Autodesk3DsDataContainer(FilePath);
         if (!File.Exists(FilePath))
         {
            dataContainer.IsFileValid = false;
            return dataContainer;
         }
         dataContainer.Metadata = new FileMetadata();
         fileStream = new FileStream(FilePath, FileMode.Open);
         binaryReader = new BinaryReader(fileStream);
         bytesCount = (int)fileStream.Length;
         var root =  dataContainer.Data.CreateMesh(null, "", Path.GetFileNameWithoutExtension(FilePath));
         dataContainer.Data.Models = root;
         fileStream.Position = 0;
         ReadData(root);

         binaryReader.Close();
         fileStream.Close();

         return dataContainer;

      }

      private int bytesCount;

      private void ReadData(SceneData.Model rootMesh)
      {
         SceneData.Model currentMesh = null;
         Mesh currentGeometry = null;
         SceneData.Material material = null;
         MaterialTextureType textureType = MaterialTextureType.None;
         ColorType colorType = ColorType.None;
         while (fileStream.Position < bytesCount)
         {
            Autodesk3DSChunks chunk = (Autodesk3DSChunks)binaryReader.ReadUInt16();
            int length = binaryReader.ReadInt32();
            ushort count;
            switch (chunk)
            {
               case Autodesk3DSChunks.Main3DS:
                  break;

               case Autodesk3DSChunks.EditorConfig3DS:
                  break;

               case Autodesk3DSChunks.Version:
                  var version = binaryReader.ReadUInt32();
                  dataContainer.Metadata.Version = version.ToString();
                  break;

               case Autodesk3DSChunks.OneUnit:
                  var units = binaryReader.ReadSingle();
                  dataContainer.Data.Units = new SceneData.Unit(UnitType.Meter, units);
                  break;

               case Autodesk3DSChunks.ObjectDefinition:
                  var meshName = ReadName();
                  currentMesh = dataContainer.Data.CreateMesh(rootMesh, "", meshName);
                  break;

               case Autodesk3DSChunks.Mesh:
                  if (length == 6)
                  {
                     ReadData(currentMesh);
                  }
                  break;

               case Autodesk3DSChunks.VertexList:
                  count = binaryReader.ReadUInt16();
                  var vertexCount = Convert.ToInt32(count);
                  if (vertexCount > 0)
                  {
                     currentGeometry = new Mesh();
                     currentGeometry.MeshTopology = PrimitiveType.TriangleList;
                     var positions = new List<Vector3>();
                     for (int x = 0; x < vertexCount; x++)
                     {
                        Vector3 vertex;
                        vertex.X = binaryReader.ReadSingle();
                        vertex.Y = binaryReader.ReadSingle();
                        vertex.Z = binaryReader.ReadSingle();
                        positions.Add(vertex);
                     }
                     currentGeometry.SetPoints(positions);
                     currentMesh.Meshes.Add(currentGeometry);
                  }
                  else
                  {
                     ReadData(currentMesh);
                  }
                  break;

               case Autodesk3DSChunks.FaceDescription:
                  count = binaryReader.ReadUInt16();
                  var polygonCount = Convert.ToInt32(count);
                  if (polygonCount > 0)
                  {
                     List<int> indices = new List<int>();
                     for (int j = 0; j < polygonCount; j++)
                     {
                        var index = binaryReader.ReadUInt16();
                        indices.Add(index);
                        index = binaryReader.ReadUInt16();
                        indices.Add(index);
                        index = binaryReader.ReadUInt16();
                        indices.Add(index);
                        var faceFlags = binaryReader.ReadUInt16();
                     }
                     currentGeometry.AssemblePoints(indices);
                  }
                  break;

               case Autodesk3DSChunks.FaceMaterial:
                  currentGeometry.MaterialID = ReadName();
                  
                  count = binaryReader.ReadUInt16();
                  for (ushort y = 0; y < count; y++)
                  {
                     int index = binaryReader.ReadUInt16();
                  }
                  break;

               case Autodesk3DSChunks.UVCoordinates:
                  count = binaryReader.ReadUInt16();
                  if (count > 0)
                  {
                     List<Vector2F> uvs = new List<Vector2F>();
                     for (ushort y = 0; y < count; y++)
                     {
                        Vector2F uv;
                        uv.X = binaryReader.ReadSingle();
                        uv.Y = binaryReader.ReadSingle();
                     }
                     currentGeometry.SetUVs(0, uvs);
                  }
                  break;

               case Autodesk3DSChunks.WorldMatrix:
                  Matrix4x4F matrix = Matrix4x4F.Identity;

                  matrix.M11 = binaryReader.ReadSingle();
                  matrix.M12 = binaryReader.ReadSingle();
                  matrix.M13 = binaryReader.ReadSingle();

                  matrix.M21 = binaryReader.ReadSingle();
                  matrix.M22 = binaryReader.ReadSingle();
                  matrix.M23 = binaryReader.ReadSingle();

                  matrix.M31 = binaryReader.ReadSingle();
                  matrix.M32 = binaryReader.ReadSingle();
                  matrix.M33 = binaryReader.ReadSingle();

                  matrix.M41 = binaryReader.ReadSingle();
                  matrix.M42 = binaryReader.ReadSingle();
                  matrix.M43 = binaryReader.ReadSingle();

                  if (currentMesh != null)
                  {
                     Vector3F scale, translation;
                     QuaternionF rotation;
                     matrix.Decompose(out scale, out rotation, out translation);
                     currentMesh.Scale = scale;
                     currentMesh.Rotation = rotation;
                     currentMesh.Position = translation;
                  }
                  break;

               case Autodesk3DSChunks.MaterialBlock:
                  break;

               case Autodesk3DSChunks.MaterialName:
                  var name = ReadName();
                  material = new SceneData.Material();
                  material.ID = name;

                  if (!dataContainer.Data.Materials.ContainsKey(name))
                  {
                     dataContainer.Data.Materials.Add(material.ID, material);
                  }
                  break;

               case Autodesk3DSChunks.AmbientColor:
                  colorType = ColorType.Ambient;
                     break;

               case Autodesk3DSChunks.DiffuseColor:
                  colorType = ColorType.Diffuse;
                  break;

               case Autodesk3DSChunks.SpecularColor:
                  colorType = ColorType.Specular;
                  break;

               case Autodesk3DSChunks.RgbColorByte:
                  Vector4F color = new Vector4F();
                  color.X = binaryReader.ReadByte();
                  color.Y = binaryReader.ReadByte();
                  color.Z = binaryReader.ReadByte();
                  color.W = 1;
                  switch (colorType)
                  {
                     case ColorType.Ambient:
                        material.AmbientColor = color;
                        break;
                     case ColorType.Diffuse:
                        material.DiffuseColor = color;
                        break;
                     case ColorType.Specular:
                        material.SpecularColor = color;
                        break;
                  }
                  break;

               case Autodesk3DSChunks.TextureMap1:
                  textureType = MaterialTextureType.TextureMap;
                  break;

               case Autodesk3DSChunks.ReflectionMap:
                  textureType = MaterialTextureType.ReflectionMap;
                  break;

               case Autodesk3DSChunks.BumpMap:
                  textureType = MaterialTextureType.BumpMap;
                  break;

               case Autodesk3DSChunks.MappingFileName:
                  var filePath = ReadName();
                  if (material == null)
                  {
                     material = new SceneData.Material();
                  }
                  switch (textureType)
                  {
                     case MaterialTextureType.TextureMap:
                        material.DiffuseMap = filePath;
                        break;
                     case MaterialTextureType.BumpMap:
                        material.BumpMap = filePath;
                        break;
                     case MaterialTextureType.ReflectionMap:
                        material.ReflectionMap = filePath;
                        break;
                  }
                  SceneData.Image image = new SceneData.Image();
                  image.FilePath = filePath;
                  image.ImageName = Path.GetFileName(filePath);
                  dataContainer.Data.Images.Add(filePath, image);
                  break;


               default:
                  fileStream.Position += length - 6;
                  break;
            }
         }
      }

      private string ReadName()
      {
         List<char> chars = new List<char>();
         char symbol = '\0';
         do
         {
            symbol = (char)binaryReader.ReadByte();
            if (symbol != '\0')
            {
               chars.Add(symbol);
            }
         } while (symbol != '\0');

         return new string(chars.ToArray());
      }

      private enum MaterialTextureType
      {
         None,
         BumpMap,
         TextureMap,
         ReflectionMap
      }

      private enum ColorType
      {
         None,
         Ambient,
         Diffuse,
         Specular
      }
   }
}
