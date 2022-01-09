using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Compiler.Converter.Parsers
{
    enum ParsedStep
    {
        None,
        Position,
        UV,
        Normal,
        Faces,
        Material
    }

    public class ObjFileParser : ModelFileParser
    {
        private List<string> fileContent;

        private String FileName;
        private String materialLibPath;

        private const String MTL = "mtllib ";
        private const String NEW_MTL = "newmtl ";
        private const String USEMTL = "usemtl ";
        private const string POSITION = "v ";
        private const string NORMAL = "vn ";
        private const string UV = "vt ";
        private const string FACES = "f ";

        private ParsedStep _lastParsedStep;

        private readonly ObjDataContainer dataContainer;
        public ObjFileParser(String filePath) : base(filePath)
        {
            dataContainer = new ObjDataContainer(filePath);
            FileName = Path.GetFileNameWithoutExtension(filePath);
        }

        protected override DataContainer ParseData(ConversionConfig config)
        {
            if (!config.GeometryEnabled && !config.MaterialsEnabled)
            {
                dataContainer.IsFileValid = false;
                return dataContainer;
            }

            fileContent = new List<string>(File.ReadAllLines(FilePath));
            ParseGeometry();

            if (!config.GeometryEnabled)
            {
                dataContainer.GeometryData = null;
            }

            if (config.MaterialsEnabled)
            {
                ParseMaterials();
            }
            dataContainer.IsFileValid = true;
            return dataContainer;
        }

        private void ParseGeometry()
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3F>();
            var uvs = new List<Vector2F>();
            positions.Add(Vector3.Zero);
            normals.Add(Vector3F.Zero);
            uvs.Add(Vector2F.Zero);
            ObjMeshData geometryData = null;
            RawIndicesSemanticData semanticData = null;
            Offset offset = new Offset();
            ulong offsetIndex = 0;
            int repeatingIndex = 0;

            for (int i = 0; i < fileContent.Count; i++)
            {
                var line = fileContent[i];
                if (line.StartsWith(MTL))
                {
                    materialLibPath = line.Substring(MTL.Length);
                }

                else if (line.StartsWith("o") || line.StartsWith("g"))
                {
                    var meshName = line.Substring(2);
                    ObjectType type = ObjectType.Object;
                    if (line.StartsWith("g"))
                    {
                        type = ObjectType.Group;
                    }
                    geometryData = new ObjMeshData(type);
                    geometryData.Name = meshName;
                    if (dataContainer.Meshes.ContainsKey(meshName))
                    {
                        meshName += "_" + repeatingIndex;
                        repeatingIndex++;
                        geometryData.Name = meshName;
                    }
                    dataContainer.Meshes.Add(meshName, geometryData);

                    if (String.IsNullOrEmpty(materialLibPath))
                    {
                        semanticData = new RawIndicesSemanticData();
                        geometryData.GeometrySemantic.Add(semanticData);
                        semanticData.Offset = offset;
                    }
                }

                else if (line.StartsWith(POSITION))
                {
                    if (offset.Position == null)
                    {
                        offset.Position = offsetIndex;
                        offsetIndex++;
                    }
                    String values = line.Substring(POSITION.Length).Trim(' ');
                    var v = ParseNumericString(values);
                    positions.Add(new Vector3(v));
                    _lastParsedStep = ParsedStep.Position;
                }

                else if (line.StartsWith(NORMAL))
                {
                    if (offset.Normal == null)
                    {
                        offset.Normal = offsetIndex;
                        offsetIndex++;
                    }
                    String values = line.Substring(NORMAL.Length);
                    var n = ParseNumericStringFloat(values);
                    normals.Add(new Vector3F(n));
                    _lastParsedStep = ParsedStep.Normal;
                }

                else if (line.StartsWith(UV))
                {
                    if (offset.UV0 == null)
                    {
                        offset.UV0 = offsetIndex;
                        offsetIndex++;
                    }
                    String values = line.Substring(UV.Length);
                    var uv = ParseNumericStringFloat(values);
                    uvs.Add(new Vector2F(uv[0], uv[1]));
                    _lastParsedStep = ParsedStep.UV;
                }

                else if (line.StartsWith(USEMTL))
                {
                    if (semanticData == null || semanticData.Offset.CommonOffset >= 0)
                    {
                        semanticData = new RawIndicesSemanticData();
                    }
                    semanticData.MaterialId = line.Substring(USEMTL.Length);
                    semanticData.Offset = offset;
                    if (geometryData != null && !geometryData.GeometrySemantic.Contains(semanticData))
                    {
                        geometryData?.GeometrySemantic.Add(semanticData);
                    }
                    _lastParsedStep = ParsedStep.Material;
                }

                else if (line.StartsWith(FACES))
                {
                    int indicesCount;
                    var indices = ParseFace(line.Substring(FACES.Length), out indicesCount);
                    if (semanticData == null)
                    {
                        continue;
                    }

                    semanticData.RawIndices.AddRange(indices);
                    semanticData.VertexType.Add(indicesCount);
                    if (semanticData.MeshTopology == PrimitiveType.Undefined)
                    {
                        var topology = indicesCount >= 3 ? PrimitiveType.TriangleList : PrimitiveType.LineList;
                        semanticData.MeshTopology = topology;
                        if (offset.UV0 != null)
                        {
                            offset.UV0 = 1;
                        }
                        if (offset.Normal != null)
                        {
                            offset.Normal = 2;
                        }
                        semanticData.Semantic = GetSemantic(offset);
                        offset = new Offset();
                        offsetIndex = 0;
                    }

                    _lastParsedStep = ParsedStep.Faces;
                }
            }

            if (_lastParsedStep == ParsedStep.Faces && dataContainer.Meshes.Count == 0)
            {
                geometryData = new ObjMeshData(ObjectType.Object);
                geometryData.Name = FileName;
                geometryData.GeometrySemantic.Add(semanticData);
                dataContainer.Meshes.Add(geometryData.Name, geometryData);
            }

            dataContainer.GeometryData.Positions = positions;
            dataContainer.GeometryData.Normals = normals;
            dataContainer.GeometryData.UV = uvs;
            dataContainer.Modules |= Modules.Geometry;
        }

        private double[] ParseNumericString(string values)
        {
            return values.Split(' ').Where(s => !string.IsNullOrEmpty(s))
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture.NumberFormat)).ToArray();
        }
        
        private float[] ParseNumericStringFloat(string values)
        {
            return values.Split(' ').Where(s => !string.IsNullOrEmpty(s))
                .Select(s => float.Parse(s, CultureInfo.InvariantCulture.NumberFormat)).ToArray();
        }

        private VertexSemantic GetSemantic(Offset offset)
        {
            VertexSemantic semantic = VertexSemantic.None;
            if (offset.Position.HasValue)
            {
                semantic |= VertexSemantic.Position;
            }
            if (offset.Normal.HasValue)
            {
                semantic |= VertexSemantic.Normal;
            }
            if (offset.UV0.HasValue)
            {
                semantic |= VertexSemantic.UV0;
            }
            return semantic;
        }

        private List<int> ParseFace(string face, out int indicesCount)
        {
            List<int> indicesList = new List<int>();

            var values = face.Split(' ');
            indicesCount = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Contains("/"))
                {
                    var indices = values[i].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => Int32.Parse(s, CultureInfo.InvariantCulture.NumberFormat)).ToArray();
                    indicesList.AddRange(indices);
                    indicesCount++;
                }
                else if (!values[i].Contains("/") && !string.IsNullOrEmpty(values[i]))
                {
                    var index = Int32.Parse(values[i], CultureInfo.InvariantCulture.NumberFormat);
                    indicesList.Add(index);
                    indicesCount++;
                }

            }
            return indicesList;
        }

        private void ParseMaterials()
        {
            if (String.IsNullOrEmpty(materialLibPath))
            {
                return; // no materials file present for current .obj file
            }

            String path = Path.Combine(Path.GetDirectoryName(FilePath), materialLibPath);
            if (!File.Exists(path))
                return; //defined material file does not exists

            String[] materials = File.ReadAllLines(path);
            ObjMaterialData objMaterial = null;
            for (int i = 0; i < materials.Length; i++)
            {
                var line = materials[i];
                if (line.StartsWith(NEW_MTL))
                {
                    var materialName = line.Substring(NEW_MTL.Length);
                    objMaterial = new ObjMaterialData();
                    objMaterial.Name = materialName;
                    dataContainer.Materials.Add(materialName, objMaterial);
                }
                else if (!String.IsNullOrEmpty(line))
                {
                    objMaterial?.Data.Add(line);
                }
            }

            dataContainer.Modules |= Modules.Materials;

        }
    }
}
