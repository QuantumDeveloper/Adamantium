using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Adamantium.Core.Exceptions;

namespace Adamantium.Engine.Graphics
{
    public static class VertexUtils
    {
        private const string positionElement = "SV_POSITION";
        private const string normalElement = "NORMAL";
        private const string colorElement = "COLOR";
        private const string texCoordElement = "TEXCOORD";
        private const string tangentElement = "TANGENT";
        private const string binormalElement = "BINORMAL";

        private static Dictionary<Type, VertexInputBindingDescription> vertexInputDescriptions;
        private static Dictionary<Type, VertexInputAttributeDescription[]> vertexInputAttributeDescriptions;

        static VertexUtils()
        {
            vertexInputDescriptions = new Dictionary<Type, VertexInputBindingDescription>();
            vertexInputAttributeDescriptions = new Dictionary<Type, VertexInputAttributeDescription[]>();
        }
        
        public static VertexInputBindingDescription GetBindingDescription(Type vertexType)
        {
            if (vertexInputDescriptions.ContainsKey(vertexType))
            {
                return vertexInputDescriptions[vertexType];
            }
            
            var desc = new VertexInputBindingDescription
            {
                Binding = 0, 
                Stride = (uint) Marshal.SizeOf(vertexType), 
                InputRate = VertexInputRate.Vertex
            };

            vertexInputDescriptions.Add(vertexType, desc);
            return desc;
        }

        public static VertexInputBindingDescription GetBindingDescription<T>() where T : struct
        {
            return GetBindingDescription(typeof(T));
        }

        public static VertexInputAttributeDescription[] GetVertexAttributeDescription(Type vertexType)
        {
            if (vertexInputAttributeDescriptions.ContainsKey(vertexType))
            {
                return vertexInputAttributeDescriptions[vertexType];
            }
            
            var fields = vertexType.GetFields();

            var attributes = new List<VertexInputAttributeDescription>();
            uint location = 0;

            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue;

                var desc = new VertexInputAttributeDescription();
                desc.Binding = 0;
                desc.Location = location;
                desc.Format = GetFormat(field);
                desc.Offset = (uint)Marshal.OffsetOf(vertexType, field.Name).ToInt32();
                location++;
                attributes.Add(desc);
            }

            return attributes.ToArray();
        }

        public static VertexInputAttributeDescription[] GetVertexAttributeDescription<T>() where T : struct
        {
            return GetVertexAttributeDescription(typeof(T));
        }
        
        private static Format GetFormat(FieldInfo field)
        {
            var attr = field.GetCustomAttribute<VertexInputElementAttribute>();
            if (attr == null)
            {
                throw new AttributeNotFoundException($"Attribute {nameof(VertexInputElementAttribute)} not found");
            }

            if (attr.Format != Format.UNDEFINED)
            {
                return attr.Format;
            }

            return VertexInputElement.GetFormatFromType(field.FieldType);

            switch (attr.SemanticName)
            {
                case positionElement:
                    return Format.R32G32B32A32_SFLOAT;
                case normalElement:
                    return Format.R32G32B32_SFLOAT;
                case colorElement:
                    return Format.R8G8B8A8_UNORM;
                case texCoordElement:
                    return Format.R32G32_SFLOAT;
                case tangentElement:
                    return Format.R32G32B32A32_SFLOAT;
                case binormalElement:
                    return Format.R32G32B32_SFLOAT;
                default:
                    throw new ArgumentOutOfRangeException($"Semantic name: {attr.SemanticName} could not be converted to Vulkan Format");
            }
        }
        
    }
}
