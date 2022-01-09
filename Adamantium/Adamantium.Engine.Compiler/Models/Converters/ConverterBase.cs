using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Parsers;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;
using Adamantium.Win32;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public abstract class ConverterBase
   {
      protected ConversionConfig Config { get; }
      protected UpAxis UpAxis { get; set; }
      protected string FileName { get; }
      protected string FilePath { get; }

      protected SceneData SceneDataContainer;

      protected string debugMessage = String.Empty;
      protected Boolean IsCancelled { get; set; }
      
      protected ModelFileParser Parser { get; set; }

      protected static Matrix4x4F ZupYup;

      static ConverterBase()
      {
         ZupYup = new Matrix4x4F();
         ZupYup.M11 = 1;
         ZupYup.M23 = 1;
         ZupYup.M32 = 1;
         ZupYup.M44 = 1;

      }


      protected ConverterBase(string filePath, ConversionConfig config, UpAxis upAxis = UpAxis.Y_UP_RH)
      {
         Config = config;
         UpAxis = upAxis;
         FilePath = filePath;
         FileName = Path.GetFileName(filePath);
         SceneDataContainer = new SceneData {Name = FileName};
      }

      public virtual SceneData StartConvertion()
      {
         try
         {
            Convert();
            if (!IsCancelled)
            {
               AssembleModel();
               return SceneDataContainer;
            }
         }
         catch (Exception exception)
         {
            MessageBox.Show(exception.Message + exception.StackTrace);
         }
         return null;
      }

      protected abstract void Convert();

      /// <summary>
      /// Assemble all parts of converted data. Here coordinate system can be changed/optimized, normales/tangents/bitangents calculated
      /// Also textures for mesh (if present) will be copied to new directory from where they will be loaded.
      /// </summary>
      protected virtual void AssembleModel()
      {
         //Собираем разнозренные части файла в единую структуру, находим связи между компонентами, оптимизируем и т.д.
         Stack<SceneData.Model> stack = new Stack<SceneData.Model>();
         stack.Push(SceneDataContainer.Models);
         while (stack.Count > 0)
         {
            var currentModel = stack.Pop();
            //Если включена опция оптимизации мешей, оптимизируем их
            if (Config.OptimizeMeshes)
            {
               for (int i = 0; i < currentModel.Meshes.Count; i++)
               {
                  //Оптимизация меша
                   if (!currentModel.Meshes[i].HasIndices)
                   {
                       currentModel.Meshes[i].GenerateBasicIndices();
                   }
                  currentModel.Meshes[i].Optimize();
               }
            }
            for (int i = 0; i < currentModel.Meshes.Count; i++)
            {
               var mesh = currentModel.Meshes[i];
               mesh.UpAxis = UpAxis;
               //Преобразовываем расположение вершин, нормалей и текс. координат таким образом, 
               //чтобы их положение соответствовало системе координат в движке
               //Ковертация меша из правосторонней в левостороннюю и смена осей
               UpAxis destinationUpAxis = UpAxis.Y_DOWN_RH;
               if (Config.ConvertToRHDirectX || Config.ConvertToOGL)
               {
                  destinationUpAxis = UpAxis.Y_UP_RH;
               }
               mesh.ChangeCoordinateSystem(destinationUpAxis);

               mesh.CalculateNormals();

               if (Config.CalculateTangentsBitangentsIfNotPresent)
               {
                  mesh.CalculateTangentsAndBinormals();
               }

               //Определяем баундбокс меша
               mesh.CalculateBoundingVolumes();
            }

            foreach (var mesh in currentModel.Dependencies)
            {
               stack.Push(mesh);
            }
         }
      }

      //Алгоритм оптимизации мешей
      public SceneData.Geometry OptimizeMesh(SceneData.Geometry nonOptimizedGeometry)
      {
         //Оптимальный алгоритм, работает очень быстро
         var vertexDict = new Dictionary<Vertex, int>();
         var newGeometry = new SceneData.Geometry
         {
            Semantic = nonOptimizedGeometry.Semantic,
            MaterialId = nonOptimizedGeometry.MaterialId,
            MeshTopology = nonOptimizedGeometry.MeshTopology
         };

         int uniqueIndex = 0;
         for (int i = 0; i < nonOptimizedGeometry.Positions.Count; ++i)
         {
            Vector2F uv0 = nonOptimizedGeometry.UV0.Count - 1 >= i ? nonOptimizedGeometry.UV0[i] : Vector2F.Zero;
            Vector2F uv1 = nonOptimizedGeometry.UV1.Count - 1 >= i ? nonOptimizedGeometry.UV1[i] : Vector2F.Zero;
            Vector2F uv2 = nonOptimizedGeometry.UV2.Count - 1 >= i ? nonOptimizedGeometry.UV2[i] : Vector2F.Zero;
            Vector2F uv3 = nonOptimizedGeometry.UV2.Count - 1 >= i ? nonOptimizedGeometry.UV2[i] : Vector2F.Zero;
            Color color = nonOptimizedGeometry.Colors.Count - 1 >= i ? nonOptimizedGeometry.Colors[i] : Colors.White;
            Vector4F jointIndex = nonOptimizedGeometry.JointIndices.Count - 1 >= i ? nonOptimizedGeometry.JointIndices[i] : Vector4F.Zero;
            Vector4F jointWeight = nonOptimizedGeometry.JointWeights.Count - 1 >= i ? nonOptimizedGeometry.JointWeights[i] : Vector4F.Zero;
            var vertex = new Vertex(nonOptimizedGeometry.Positions[i], uv0, uv1, uv2, uv3, color, jointIndex, jointWeight);

            if (!vertexDict.ContainsKey(vertex))
            {
               vertexDict.Add(vertex, uniqueIndex);
               newGeometry.Positions.Add(nonOptimizedGeometry.Positions[i]);
               newGeometry.IndexBuffer.Add(uniqueIndex);
               uniqueIndex++;
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.UV0))
               {
                  newGeometry.UV0.Add(vertex.UV0);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.UV1))
               {
                  newGeometry.UV1.Add(vertex.UV1);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.UV2))
               {
                  newGeometry.UV2.Add(vertex.UV2);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.UV3))
               {
                  newGeometry.UV3.Add(vertex.UV3);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.Color))
               {
                  newGeometry.Colors.Add(vertex.Color);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.JointIndices))
               {
                  newGeometry.JointIndices.Add(vertex.JointIndex);
               }
               if (nonOptimizedGeometry.Semantic.HasFlag(VertexSemantic.JointWeights))
               {
                  newGeometry.JointWeights.Add(vertex.JointWeight);
               }
            }
            else
            {
               var index = vertexDict[vertex];
               newGeometry.IndexBuffer.Add(index);
            }
         }

         nonOptimizedGeometry = newGeometry;
         return nonOptimizedGeometry;
      }

      //Считает мягкие нормали для каждой вершины
      protected void CalculateNormals(SceneData.Geometry geometryData)
      {
         if (geometryData.MeshTopology != PrimitiveType.LineList)
         {
            Vector3F[] normals = new Vector3F[geometryData.Positions.Count];
            for (int i = 0; i < geometryData.IndexBuffer.Count; i += 3)
            {
               Vector3F v0 = geometryData.Positions[geometryData.IndexBuffer[i + 1]] -
                            geometryData.Positions[geometryData.IndexBuffer[i]];
               Vector3F v1 = geometryData.Positions[geometryData.IndexBuffer[i + 2]] -
                            geometryData.Positions[geometryData.IndexBuffer[i]];

               Vector3F normal = Vector3F.Cross(v0, v1);

               normals[geometryData.IndexBuffer[i]] += normal;
               normals[geometryData.IndexBuffer[i + 1]] += normal;
               normals[geometryData.IndexBuffer[i + 2]] += normal;
            }

            for (int i = 0; i < normals.Length; i++)
            {
               normals[i] = Vector3F.Normalize(normals[i]);
            }
            geometryData.Normals = normals.ToList();
            geometryData.Semantic |= VertexSemantic.Normal;
         }
      }

      //Вычисление тангентов и бинормалей
      protected void CalculateTangentsAndBinormals(SceneData.Geometry geometryData)
      {
         if (geometryData.MeshTopology == PrimitiveType.TriangleList)
         {
            Vector3F[] tan1 = new Vector3F[geometryData.Positions.Count];
            Vector3F[] tan2 = new Vector3F[geometryData.Positions.Count];
            for (int i = 0; i < geometryData.IndexBuffer.Count; i += 3)
            {
               Vector3F v1 = geometryData.Positions[geometryData.IndexBuffer[i]];
               Vector3F v2 = geometryData.Positions[geometryData.IndexBuffer[i + 1]];
               Vector3F v3 = geometryData.Positions[geometryData.IndexBuffer[i + 2]];

               Vector2F UV1 = (Vector2F)geometryData.UV0[geometryData.IndexBuffer[i]];
               Vector2F UV2 = (Vector2F)geometryData.UV0[geometryData.IndexBuffer[i + 1]];
               Vector2F UV3 = (Vector2F)geometryData.UV0[geometryData.IndexBuffer[i + 2]];

               Vector3F v1v0 = v2 - v1;
               Vector3F v2v0 = v3 - v1;

               float s1 = UV2.X - UV1.X;
               float t1 = UV2.Y - UV1.Y;

               float s2 = UV3.X - UV1.X;
               float t2 = UV3.Y - UV1.Y;

               Vector3F uDirection = new Vector3F((t2 * v1v0.X - t1 * v2v0.X), (t2 * v1v0.Y - t1 * v2v0.Y),
                  (t2 * v1v0.Z - t1 * v2v0.Z));
               Vector3F vDirection = new Vector3F((s1 * v2v0.X - s2 * v1v0.X), (s1 * v2v0.Y - s2 * v1v0.Y),
                  (s1 * v2v0.Z - s2 * v1v0.Z));

               tan1[geometryData.IndexBuffer[i]] += uDirection;
               tan1[geometryData.IndexBuffer[i + 1]] += uDirection;
               tan1[geometryData.IndexBuffer[i + 2]] += uDirection;

               tan2[geometryData.IndexBuffer[i]] += vDirection;
               tan2[geometryData.IndexBuffer[i + 1]] += vDirection;
               tan2[geometryData.IndexBuffer[i + 2]] += vDirection;
            }

            Vector3F[] tangentArray = new Vector3F[geometryData.Positions.Count];
            Vector3F[] bitangentArray = new Vector3F[geometryData.Positions.Count];
            for (int a = 0; a < geometryData.Positions.Count; a++)
            {
               Vector3F normal = geometryData.Normals[a];

               // Gram-Schmidt orthogonalize
               var value = tan1[a] - normal * Vector3F.Dot(normal, tan1[a]);
               tangentArray[a].X = value.X;
               tangentArray[a].Y = value.Y;
               tangentArray[a].Z = value.Z;
               //tangentArray[a] = Vector4F.Normalize(tangentArray[a]);

               bitangentArray[a] =
                  Vector3F.Cross(normal, new Vector3F(tangentArray[a].X, tangentArray[a].Y, tangentArray[a].Z));

               /*
               // Calculate handedness (for case if tangent is Vector4F
               tangentArray[a].W = Vector3F.Dot(Vector3F.Cross(normal, tan1[a]), tan2[a]) < 0.0F ? -1.0F : 1.0F;
               bitangentArray[a] =
                  Vector3F.Cross(normal, new Vector3F(tangentArray[a].X, tangentArray[a].Y, tangentArray[a].Z))*
                  tangentArray[a].W;
                */
            }
            geometryData.Bitangents.AddRange(bitangentArray);
            geometryData.Tangents.AddRange(tangentArray);
            geometryData.Semantic |= VertexSemantic.TangentBiNormal;
         }
      }

      private void ConvertUVs(SceneData.Geometry geometry, int index)
      {
         Vector2F uv;
         if (Config.ConvertToLHDirectX || Config.ConvertToRHDirectX)
         {
            if (geometry.Semantic.HasFlag(VertexSemantic.UV0))
            {
               uv = geometry.UV0[index];
               uv.Y = 1.0f - uv.Y;
               geometry.UV0[index] = uv;
            }

            if (geometry.Semantic.HasFlag(VertexSemantic.UV1))
            {
               uv = geometry.UV1[index];
               uv.Y = 1.0f - uv.Y;
               geometry.UV1[index] = uv;
            }

            if (geometry.Semantic.HasFlag(VertexSemantic.UV2))
            {
               uv = geometry.UV2[index];
               uv.Y = 1.0f - uv.Y;
               geometry.UV2[index] = uv;
            }

            if (geometry.Semantic.HasFlag(VertexSemantic.UV3))
            {
               uv = geometry.UV3[index];
               uv.Y = 1.0f - uv.Y;
               geometry.UV3[index] = uv;
            }
         }
      }

      //Меняем порядок отрисовки с "против часовой стрелки" на "по часовой стрелке" путём изменения порядка индексов
      //в индексном массиве (меняем каждый второй и третий индексы местами)
      public void ChangeDrawOrder(SceneData.Geometry geometryData)
      {
         Vector3F temp = new Vector3F();
         if (geometryData.MeshTopology == PrimitiveType.TriangleList)
         {
            try
            {
               for (int i = 0; i < geometryData.IndexBuffer.Count; i += 3)
               {
                  temp.Y = geometryData.IndexBuffer[i + 1];
                  temp.Z = geometryData.IndexBuffer[i + 2];
                  geometryData.IndexBuffer[i + 1] = (int)temp.Z;
                  geometryData.IndexBuffer[i + 2] = (int)temp.Y;
               }
            }
            catch (Exception e)
            {
               Console.WriteLine(e);
            }
         }
      }

   }
}
