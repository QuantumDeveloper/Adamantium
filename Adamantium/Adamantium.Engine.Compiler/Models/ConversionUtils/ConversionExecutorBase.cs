using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Compiler.Converter.ConversionUtils
{
   internal abstract class ConversionExecutorBase
   {
      protected ConversionConfig config;
      protected UpAxis upAxis;

      //Матрица перевода осей Zup->Yup
      protected static Matrix4x4F ZupYup;

      static ConversionExecutorBase()
      {
         ZupYup = new Matrix4x4F();
         ZupYup.M11 = 1;
         ZupYup.M23 = 1;
         ZupYup.M32 = 1;
         ZupYup.M44 = 1;

         //logger = LogManager.GetLogger("ConversionHelper");
      }

      protected ConversionExecutorBase(ConversionConfig config, UpAxis upAxis)
      {
         this.config = config;
         this.upAxis = upAxis;
      }

      //загружаем индексы
      internal IndicesContainer DistributeIndices(RawIndicesSemanticData rawIndices)
      {
         IndicesContainer indicesContainer = null;
         try
         {
            //Заполняем массивы индексов
            indicesContainer = SplitRawIndices(rawIndices);

            //проверяем нужна ли триангуляция и выполняем её если нужно
            if (IsTriangulationRequired(rawIndices.VertexType))
            {
               indicesContainer = Triangulate(rawIndices.Semantic, indicesContainer, rawIndices.VertexType);
            }
         }
         catch (Exception ex)
         {
            throw;
         }
         return indicesContainer;
      }

      //Заполнение массива индексов для COLLADA 1.4.0 (1.4.1)/.obj
      internal IndicesContainer SplitRawIndices(RawIndicesSemanticData rawIndices)
      {
         IndicesContainer indicesContainer = new IndicesContainer();
         indicesContainer.MaterialId = rawIndices.MaterialId;
         indicesContainer.Semantic = rawIndices.Semantic;
         indicesContainer.MeshTopology = rawIndices.MeshTopology;

         for (int i = 0; i < rawIndices.RawIndices.Count; i += rawIndices.Offset.CommonOffset + 1)
         {
            for (var j = i; j < i + rawIndices.Offset.CommonOffset + 1; j++)
            {
               if (rawIndices.Offset.Position != null && j - i == (int)rawIndices.Offset.Position)
               {
                  indicesContainer.Positions.Add(rawIndices.RawIndices[j]);
               }

               if (rawIndices.Offset.Normal != null && j - i == (int)rawIndices.Offset.Normal)
               {
                  //indicesContainer.Positions.Add(rawIndices.RawIndices[j]);
                  //skip this step
               }

               else if (rawIndices.Offset.UV0 != null && j - i == (int)rawIndices.Offset.UV0)
               {
                  indicesContainer.UV0.Add(rawIndices.RawIndices[j]);
               }

               else if (rawIndices.Offset.UV1 != null && j - i == (int)rawIndices.Offset.UV1)
               {
                  indicesContainer.UV1.Add(rawIndices.RawIndices[j]);
               }

               else if (rawIndices.Offset.UV2 != null && j - i == (int)rawIndices.Offset.UV2)
               {
                  indicesContainer.UV2.Add(rawIndices.RawIndices[j]);
               }

               else if (rawIndices.Offset.UV3 != null && j - i == (int)rawIndices.Offset.UV3)
               {
                  indicesContainer.UV3.Add(rawIndices.RawIndices[j]);
               }

               else if (rawIndices.Offset.Color != null && j - i == (int)rawIndices.Offset.Color)
               {
                  indicesContainer.Colors.Add(rawIndices.RawIndices[j]);
               }
            }
         }

         rawIndices.RawIndices.Clear();
         return indicesContainer;
      }

      //Этот метод определяет нужна ли мешу триангуляция
      public bool IsTriangulationRequired(IEnumerable<int> vertexType)
      {
         bool triangulationNeeded = false;
         if (vertexType != null)
         {
            if (vertexType.Any(t => t > 3))
            {
               triangulationNeeded = true;
            }
         }
         return triangulationNeeded;
      }

      //Триангуляция меша
      internal IndicesContainer Triangulate(VertexSemantic semantic, IndicesContainer indicesContainer,
         List<int> vertexType)
      {
         List<int> triangulatedPositionList = new List<int>();
         List<int> triangulatedUV0List = new List<int>();
         List<int> triangulatedUV1List = new List<int>();
         List<int> triangulatedUV2List = new List<int>();
         List<int> triangulatedUV3List = new List<int>();
         List<int> triangulatedColorList = new List<int>();

         /*
          * Переделываем массив индексов для вершин, нормалей и текстурных координат,
          * триангулируя полигоны, которые содержат больше 3 вершин
          * 
          * 1. Проходимся по коллекции, описывающей нетриангулированные данные
          * 2. Внутри цикла стартуем второй цикл, в котором проходимся от нуля до значния ячейки
          * 3. Пока значение в цикле меньше 3, просто прибавляем это число к переменной, 
          * которая накапливает суммарный индекс (то есть её конечное значение должно равняться сумме
          * значений внтури коллекции vertexType и добавляем новый индекс из коллекции нетриангулированных индексов 
          * во временную коллекцию индексов (уже триангулированных)
          * 4. Если значение в цикле больше 2, тогда прибавляем к текущему суммарному индексу 0, j-1 и j-2
          * соответственно, доставая по этим значениям данные из нетриангулированного массива индексов
          * 5. Прибавляем число из коллекции vertexType к накапливаемому значению и переходим на новую итерацию цикла
         */

         int n = 0;
         for (int i = 0; i < vertexType.Count; i++)
         {
            for (int j = 0; j < vertexType[i]; j++)
            {
               if (j < 3)
               {
                  triangulatedPositionList.Add(indicesContainer.Positions[n + j]);
                  if (semantic.HasFlag(VertexSemantic.UV0))
                  {
                     triangulatedUV0List.Add(indicesContainer.UV0[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV1))
                  {
                     triangulatedUV1List.Add(indicesContainer.UV1[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV2))
                  {
                     triangulatedUV2List.Add(indicesContainer.UV2[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV3))
                  {
                     triangulatedUV3List.Add(indicesContainer.UV3[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.Color))
                  {
                     triangulatedColorList.Add(indicesContainer.Colors[n + j]);
                  }
               }
               else
               {
                  triangulatedPositionList.Add(indicesContainer.Positions[n]);
                  triangulatedPositionList.Add(indicesContainer.Positions[n + j - 1]);
                  triangulatedPositionList.Add(indicesContainer.Positions[n + j]);

                  if (semantic.HasFlag(VertexSemantic.UV0))
                  {
                     triangulatedUV0List.Add(indicesContainer.UV0[n]);
                     triangulatedUV0List.Add(indicesContainer.UV0[n + j - 1]);
                     triangulatedUV0List.Add(indicesContainer.UV0[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV1))
                  {
                     triangulatedUV1List.Add(indicesContainer.UV1[n]);
                     triangulatedUV1List.Add(indicesContainer.UV1[n + j - 1]);
                     triangulatedUV1List.Add(indicesContainer.UV1[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV2))
                  {
                     triangulatedUV2List.Add(indicesContainer.UV2[n]);
                     triangulatedUV2List.Add(indicesContainer.UV2[n + j - 1]);
                     triangulatedUV2List.Add(indicesContainer.UV2[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.UV3))
                  {
                     triangulatedUV3List.Add(indicesContainer.UV3[n]);
                     triangulatedUV3List.Add(indicesContainer.UV3[n + j - 1]);
                     triangulatedUV3List.Add(indicesContainer.UV3[n + j]);
                  }

                  if (semantic.HasFlag(VertexSemantic.Color))
                  {
                     triangulatedColorList.Add(indicesContainer.Colors[n]);
                     triangulatedColorList.Add(indicesContainer.Colors[n + j - 1]);
                     triangulatedColorList.Add(indicesContainer.Colors[n + j]);
                  }

               }
            }
            n += vertexType[i];
         }
         indicesContainer.Positions.Clear();
         indicesContainer.Positions = triangulatedPositionList;
         indicesContainer.UV0 = triangulatedUV0List;
         indicesContainer.UV1 = triangulatedUV1List;
         indicesContainer.UV2 = triangulatedUV2List;
         indicesContainer.UV3 = triangulatedUV3List;
         indicesContainer.Colors = triangulatedColorList;
         return indicesContainer;
      }
   }
}
