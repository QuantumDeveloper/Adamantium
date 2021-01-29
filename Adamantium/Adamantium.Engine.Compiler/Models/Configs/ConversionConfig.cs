using System;
using Adamantium.Core;

namespace Adamantium.Engine.Compiler.Converter.Configs
{
   public class ConversionConfig : PropertyChangedBase
   {

      public ConversionConfig(Boolean enableAll)
      {
         if (enableAll)
         {
            EnableAll();
         }
         else
         {
            SetDefaults();
         }
      }

      #region Объявление переменных

      //устанавливает значение будет ли модель записана в бинарный файл или в текстовый, true - бинарный
      private bool individualSettingsEnabled;
      private bool geometryEnabled;
      private bool animationEnabled;
      private bool controllersEnabled;
      private bool lightsEnabled;
      private bool materialsEnabled;
      private bool imagesEnabled;
      private bool camerasEnabled;
      private bool transposeMatrixNeeded;
      private bool optimizeMeshes;
      private bool convertToLhDX;
      private bool convertToRhDX;
      private bool convertToOGL;
      private bool convertToVulkan;
      private bool configChanged;
      private bool calculateNormalsIfNotPresent;
      private bool calculateTangentsBitangentsIfNotPresent;
      private bool getMeshMatrices;
      private bool getLightsMatrices;
      private bool getCamerasMatrices;
      private bool getJoints;

      private bool isClockWise;
      private static string fileName = "ConvertationConfig.aecc"; //adamantium engine covertation config

      #endregion //Объявление переменных

      #region Свойства

      public static String FileName
      {
         get { return fileName; }
      }

      public bool GetJoints
      {
         get { return getJoints; }
         set
         {
            getJoints = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool GetCamerasMatrices
      {
         get { return getCamerasMatrices; }
         set
         {
            getCamerasMatrices = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool GetLightsMatrices
      {
         get { return getLightsMatrices; }
         set
         {
            getLightsMatrices = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool GetMeshMatrices
      {
         get { return getMeshMatrices; }
         set
         {
            getMeshMatrices = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool CalculateNormalsIfNotPresent
      {
         get { return calculateNormalsIfNotPresent; }
         set
         {
            calculateNormalsIfNotPresent = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool CalculateTangentsBitangentsIfNotPresent
      {
         get { return calculateTangentsBitangentsIfNotPresent; }
         set
         {
            calculateTangentsBitangentsIfNotPresent = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool OptimizeMeshes
      {
         get { return optimizeMeshes; }
         set
         {
            optimizeMeshes = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ConvertToLHDirectX
      {
         get { return convertToLhDX; }
         set
         {
            convertToLhDX = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ConvertToRHDirectX
      {
         get { return convertToRhDX; }
         set
         {
            convertToRhDX = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ConvertToOGL
      {
         get { return convertToOGL; }
         set
         {
            convertToOGL = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ConvertToVulkan
      {
         get => convertToVulkan;
         set => convertToVulkan = value;
      }

      public bool IsClockWise
      {
         get { return isClockWise; }
         set
         {
            isClockWise = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool IndividualSettingsEnabled
      {
         get { return individualSettingsEnabled; }
         set
         {
            individualSettingsEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool GeometryEnabled
      {
         get { return geometryEnabled; }
         set
         {
            geometryEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool AnimationEnabled
      {
         get { return animationEnabled; }
         set
         {
            animationEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ControllersEnabled
      {
         get { return controllersEnabled; }
         set
         {
            controllersEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool LightsEnabled
      {
         get { return lightsEnabled; }
         set
         {
            lightsEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool MaterialsEnabled
      {
         get { return materialsEnabled; }
         set
         {
            materialsEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ImagesEnabled
      {
         get { return imagesEnabled; }
         set
         {
            imagesEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool MatrixTransposeNeeded
      {
         get { return transposeMatrixNeeded; }
         set
         {
            transposeMatrixNeeded = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool CamerasEnabled
      {
         get { return camerasEnabled; }
         set
         {
            camerasEnabled = value;
            RaisePropertyChanged();
            ConfigChanged = true;
         }
      }

      public bool ConfigChanged
      {
         get { return configChanged; }
         set
         {
            configChanged = value;
            RaisePropertyChanged();
         }
      }


      #endregion //Свойства

      public void SetDefaults()
      {
         individualSettingsEnabled = true;
         geometryEnabled = false;
         animationEnabled = false;
         controllersEnabled = false;
         lightsEnabled = false;
         imagesEnabled = false;
         materialsEnabled = false;
         transposeMatrixNeeded = true;
         convertToLhDX = true;
         convertToRhDX = false;
         convertToOGL = false;
         isClockWise = true;
         optimizeMeshes = true;
         getJoints = true;
      }

      public void EnableAll()
      {
         individualSettingsEnabled = true;
         geometryEnabled = true;
         animationEnabled = true;
         controllersEnabled = true;
         lightsEnabled = true;
         imagesEnabled = true;
         materialsEnabled = true;
         transposeMatrixNeeded = true;
         convertToLhDX = true;
         convertToRhDX = false;
         convertToOGL = false;
         isClockWise = true;
         optimizeMeshes = true;
         getMeshMatrices = true;
         getLightsMatrices = true;
         getCamerasMatrices = true;
         getJoints = true;
         calculateNormalsIfNotPresent = true;
         calculateTangentsBitangentsIfNotPresent = true;
      }
   }
}
