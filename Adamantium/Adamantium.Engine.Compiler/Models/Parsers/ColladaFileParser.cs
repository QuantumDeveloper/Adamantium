﻿using System;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Compiler.Converter.AutoGenerated;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Compiler.Converter.ConversionUtils;
using Adamantium.Engine.Core;
using static Adamantium.Engine.Core.Models.SceneData;

namespace Adamantium.Engine.Compiler.Converter.Parsers
{
   public class ColladaFileParser: ModelFileParser
   {
      private COLLADA model;
      private ColladaDataContainer dataContainer;
      private assetContributor[] contributor;
      private asset _asset;
      private String Separator = ":";
      private Modules modules;
      private ConversionConfig config;

      //Конструктор класса
      public ColladaFileParser(String filePath) : base(filePath)
      {
         dataContainer = new ColladaDataContainer(filePath) {ConverterToUse = ConverterVariant.Collada};
      }

      protected override DataContainer ParseData(ConversionConfig config)
      {
         this.config = config;
         //Проверяем действительно ли файл содержит корректную информацию
         //Если нет, процесс разбора файла прекращается
         if (LoadFile(dataContainer.FilePath))
         {
            ParseFile();
            GetMetadata();
         }

         if (IsEmpty())
         {
            dataContainer.IsFileValid = false;
         }

         return dataContainer;
      }

      //Получаем базовую необходимую информацию о файле
      protected Boolean LoadFile(string filePath)
      {
         try
         {
            model = COLLADA.Load(filePath);
            _asset = model.asset;
            contributor = _asset.contributor;
            dataContainer.IsFileValid = true;
         }
         catch (InvalidOperationException)
         {
            //Если файл не открылся, значит он не является корректным
            //Отмечаем это и возвращаем false
            dataContainer.IsFileValid = false;
            return false;
         }
         return true;
      }

      //Парсим файл
      private void ParseFile()
      {
         foreach (var item in model.Items)
         {
            try
            {
               //Находим библиотеку геометрии
               var geometries = item as library_geometries;
               if (geometries != null && config.GeometryEnabled)
               {
                  foreach (var geometry in geometries.geometry)
                  {
                     dataContainer.Geometries.Add(geometry);
                  }
                  modules |= Modules.Geometry;
               }

               //Находим библиотеку камер
               var cameras = item as library_cameras;
               if (cameras != null && config.CamerasEnabled)
               {
                  foreach (var camera in cameras.camera)
                  {
                     dataContainer.Cameras.Add(camera);
                  }
                  modules |= Modules.Cameras;
               }

               //Находим библиотеку визуальныъх сцен
               var visualScenes = item as library_visual_scenes;
               if (visualScenes != null)
               {
                  foreach (var visualScene in visualScenes.visual_scene)
                  {
                     dataContainer.VisualScenes.Add(visualScene);
                  }
                  modules |= Modules.VisualScenes;
               }

               //Находим библиотеку анимации
               var animations = item as library_animations;
               if (animations != null && config.AnimationEnabled)
               {
                  foreach (var animation in animations.animation)
                  {
                     dataContainer.Animations.Add(animation);
                  }
                  modules |= Modules.Animation;
               }

               //Находим библиотеку контроллеров
               var controllers = item as library_controllers;
               if (controllers?.controller != null && config.ControllersEnabled)
               {
                  foreach (var controller in controllers.controller)
                  {
                     dataContainer.Controllers.Add(controller);
                  }
                  modules |= Modules.Controllers;
               }

               //Находим библиотеку света
               var lights = item as library_lights;
               if (lights != null && config.LightsEnabled)
               {
                  foreach (var light in lights.light)
                  {
                     dataContainer.Lights.Add(light);
                  }
                  modules |= Modules.Light;
               }

               //Находим библиотеку текстур
               var images = item as library_images;
               if (images?.image != null && config.ImagesEnabled)
               {
                  foreach (var image in images.image)
                  {
                     dataContainer.Images.Add(image);
                  }
                  modules |= Modules.Images;
               }

               //Находим библиотеку материалов
               var materials = item as library_materials;
               if (materials != null && config.MaterialsEnabled)
               {
                  foreach (var material in materials.material)
                  {
                     dataContainer.Materials.Add(material);
                  }
                  modules |= Modules.Materials;
               }

               //Находим библиотеку эффектов
               var effects = item as library_effects;
               if (effects != null && config.MaterialsEnabled)
               {
                  foreach (var effect in effects.effect)
                  {
                     dataContainer.Effects.Add(effect);
                  }
               }
            }
            //Добавлен именно этот эксепшн потому что в файлах Блендера есть 
            //вариант, когда присутствует элемент в таком виде <library_images/>
            //Это приводит к тому, что проверка на нулл не даёт положительного результата
            //Тогда это самый просто способ избежать падения программы
            catch (NullReferenceException) {}
         }
         dataContainer.SortedModules = modules.Sort<Modules>();
      }


      #region Metadata

      private void GetMetadata()
      {
         //Получает версию файла
         FileMetadata metadata = new FileMetadata();
         string version = null;
         if (model.version == VersionType.Item141)
         {
            version = "1.4.1";
         }
         else if (model.version == VersionType.Item140)
         {
            version = "1.4.0";
         }
         else
         {
            version = model.version.ToString();
         }
         metadata.Version = version;
         metadata.AuthoringTool = contributor[0].authoring_tool;
         metadata.Author = contributor[0].author;
         metadata.CreationDate = _asset.created.Date;
         metadata.ModificationDate = _asset.modified.Date;

         UnitType result;
         if (Enum.TryParse(_asset.unit.name, true, out result))
         {
            dataContainer.Units = new Unit(result, (Single) _asset.unit.meter);
         }

         dataContainer.Axis = (UpAxis) _asset.up_axis;

         dataContainer.Metadata = metadata;
      }

      #endregion

      private bool IsEmpty()
      {
         if (dataContainer.Geometries.Count == 0 &&
             dataContainer.Animations.Count == 0 &&
             dataContainer.Controllers.Count == 0 &&
             dataContainer.Cameras.Count == 0 &&
             dataContainer.Lights.Count == 0 &&
             dataContainer.Materials.Count == 0 &&
             dataContainer.Effects.Count == 0 &&
             dataContainer.Images.Count == 0 &&
             dataContainer.VisualScenes.Count == 0)
         {
            return true;
         }
         return false;
      }
   }
}