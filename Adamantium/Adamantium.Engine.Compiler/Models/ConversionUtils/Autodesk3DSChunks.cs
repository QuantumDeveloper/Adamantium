namespace Adamantium.Engine.Compiler.Converter.ConversionUtils
{
   /// <summary>
   /// Desrbibes chunk code for parsing 3DS file format
   /// </summary>
   public enum Autodesk3DSChunks : ushort
   {
      None = 0x0,
      /// <summary>
      /// Main chunk
      /// </summary>
      Main3DS = 0x4D4D,

      /// <summary>
      /// Contains version of 3ds file
      /// </summary>
      Version = 0x0002,

      /// <summary>
      /// Describes metrics or how many units are inside 1 world unit
      /// </summary>
      OneUnit = 0x0100,

      /// <summary>
      /// Start chunk of the editor config
      /// </summary>
      EditorConfig3DS = 0x3D3D, // 
      /// <summary>
      /// Start chunk of the keyframer config
      /// </summary>
      KeyframeConfig3DS = 0xB000,

      /// <summary>
      /// Main material block
      /// </summary>
      MaterialBlock = 0xAFFF,
      /// <summary>
      /// Material name
      /// </summary>
      MaterialName = 0xA000,
      /// <summary>
      /// Ambient color value
      /// </summary>
      AmbientColor = 0xA010,
      /// <summary>
      /// Diffuse color value
      /// </summary>
      DiffuseColor = 0xA020,
      /// <summary>
      /// Specular color value
      /// </summary>
      SpecularColor = 0xA030,
      
      /// <summary>
      /// RGB color in float format
      /// </summary>
      RgbColorFloat = 0x0010,

      /// <summary>
      /// RGB color in byte format
      /// </summary>
      RgbColorByte = 0x0011,

      /// <summary>
      /// RGB color gamma corrected in float format
      /// </summary>
      RgbColorGammaFloat = 0x0012,

      /// <summary>
      /// RGB color gamma corrected in byte format
      /// </summary>
      RgbColorGammaByte = 0x0013,
      
      /// <summary>
      /// Mesh name
      /// </summary>
      ObjectDefinition = 0x4000,

      /// <summary>
      /// Mesh block
      /// </summary>
      Mesh = 0x4100,
      /// <summary>
      /// Light block
      /// </summary>
      Light = 0x4600,

      /// <summary>
      /// Camera block
      /// </summary>
      Camera = 0x4700,

      LightOff = 0x4620,

      SpotLight = 0x4610,
      
      //------ sub defines of PolygonList
      /// <summary>
      /// Contains list of mesh positions
      /// </summary>
      VertexList = 0x4110,
      /// <summary>
      /// Contains indexes in correct order to construct mesh from vertices
      /// </summary>
      FaceDescription = 0x4120,
      /// <summary>
      /// //Contains material name and indexes of each POLYGON (triangle), described in <see cref="Autodesk3DSChunks.FaceDescription"/> block
      /// </summary>
      FaceMaterial = 0x4130,
      /// <summary>
      /// Contains list of uv coordinates
      /// </summary>
      UVCoordinates = 0x4140,

      FaceSmoothingGroup = 0x4150,
      /// <summary>
      /// Mesh world matrix contains 3 vectors, describes its orientation and 4th vector describes its origin
      /// </summary>
      WorldMatrix = 0x4160,
      /// <summary>
      /// Visibible/invisible object
      /// </summary>
      ObjectVisible = 0x4165,
      StandardMapping = 0x4170,

      /// <summary>
      /// Describes presence of bump texture
      /// </summary>
      BumpMap = 0xA230,
      /// <summary>
      /// Describes presence of usual texture
      /// </summary>
      TextureMap1 = 0xA200,
      /// <summary>
      /// Describes presence of reflection texture
      /// </summary>
      ReflectionMap = 0xA220,
      /// <summary>
      /// Contains path to texture on file system
      /// </summary>
      MappingFileName = 0xA300,

      MappingParameters = 0xA351,

      /// <summary>
      /// Start and end frames
      /// </summary>
      StartEndFrames = 0xB008,
      /// <summary>
      /// Object name
      /// </summary>
      KFObjectName = 0xB010,
      /// <summary>
      /// Object base point for current key frame
      /// </summary>
      KFObjectPivotPoint = 0xB013,
      /// <summary>
      /// Object position for current key frame
      /// </summary>
      KFPositionTrack = 0xB020,
      /// <summary>
      /// Object rotation for current key frame
      /// </summary>
      KFRotationTrack = 0xB021,
      /// <summary>
      /// Object scale for current key frame
      /// </summary>
      KFScaleTrack = 0xB022,
      /// <summary>
      /// Hierarchy position
      /// </summary>
      KFHierarchyPosition = 0xB030,


   }
}
