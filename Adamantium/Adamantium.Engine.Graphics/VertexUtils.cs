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
        private static Dictionary<Type, VertexInputBindingDescription> vertexInputDescriptions;
        private static Dictionary<Type, VertexInputAttributeDescription[]> vertexInputAttributeDescriptions;
        
        private static Dictionary<Type, VertexInputBindingDescription2EXT> vertexInputDescriptions2;
        private static Dictionary<Type, VertexInputAttributeDescription2EXT[]> vertexInputAttributeDescriptions2;

        static VertexUtils()
        {
            vertexInputDescriptions = new Dictionary<Type, VertexInputBindingDescription>();
            vertexInputAttributeDescriptions = new Dictionary<Type, VertexInputAttributeDescription[]>();

            vertexInputDescriptions2 = new Dictionary<Type, VertexInputBindingDescription2EXT>();
            vertexInputAttributeDescriptions2 = new Dictionary<Type, VertexInputAttributeDescription2EXT[]>();
        }
        
        public static VertexInputBindingDescription GetBindingDescription(Type vertexType)
        {
            if (vertexInputDescriptions.TryGetValue(vertexType, out var description))
            {
                return description;
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
            if (vertexInputAttributeDescriptions.TryGetValue(vertexType, out var description))
            {
                return description;
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

            vertexInputAttributeDescriptions[vertexType] = attributes.ToArray();

            return attributes.ToArray();
        }
        
        public static VertexInputAttributeDescription[] GetVertexAttributeDescription<T>() where T : struct
        {
            return GetVertexAttributeDescription(typeof(T));
        }
        
        public static VertexInputBindingDescription2EXT GetBindingDescription2(Type vertexType)
        {
            if (vertexInputDescriptions2.TryGetValue(vertexType, out var description))
            {
                return description;
            }
            
            var desc = new VertexInputBindingDescription2EXT
            {
                Binding = 0, 
                Stride = (uint) Marshal.SizeOf(vertexType), 
                InputRate = VertexInputRate.Vertex,
                Divisor = 1
            };

            vertexInputDescriptions2.Add(vertexType, desc);
            return desc;
        }
        
        public static VertexInputBindingDescription2EXT GetBindingDescription2<T>() where T : struct
        {
            return GetBindingDescription2(typeof(T));
        }
        
        public static VertexInputAttributeDescription2EXT[] GetVertexAttributeDescription2(Type vertexType)
        {
            if (vertexInputAttributeDescriptions2.TryGetValue(vertexType, out var description2))
            {
                return description2;
            }
            
            var fields = vertexType.GetFields();

            var attributes = new List<VertexInputAttributeDescription2EXT>();
            uint location = 0;

            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue;

                var desc = new VertexInputAttributeDescription2EXT();
                desc.Binding = 0;
                desc.Location = location;
                desc.Format = GetFormat(field);
                desc.Offset = (uint)Marshal.OffsetOf(vertexType, field.Name).ToInt32();
                location++;
                attributes.Add(desc);
            }
            
            vertexInputAttributeDescriptions2[vertexType] = attributes.ToArray();

            return attributes.ToArray();
        }
        
        private static Format GetFormat(FieldInfo field)
        {
            var attr = field.GetCustomAttribute<VertexInputElementAttribute>();
            if (attr == null)
            {
                throw new AttributeNotFoundException($"Attribute {nameof(VertexInputElementAttribute)} not found");
            }

            return attr.Format != Format.UNDEFINED ? attr.Format : VertexInputElement.GetFormatFromType(field.FieldType);
        }
    }
}
