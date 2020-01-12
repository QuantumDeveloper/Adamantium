using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics
{
    public static class VertexUtils
    {
        public static VertexInputBindingDescription GetBindingDescription(Type vertexType)
        {
            var decr = new VertexInputBindingDescription();
            decr.Binding = 0;
            decr.Stride = (uint)Marshal.SizeOf(vertexType);
            decr.InputRate = VertexInputRate.Vertex;

            return decr;
        }

        public static VertexInputBindingDescription GetBindingDescription<T>() where T : struct
        {
            return GetBindingDescription(typeof(T));
        }

        public static VertexInputAttributeDescription[] GetVertexAttributeDescription(Type vertexType)
        {
            var fields = vertexType.GetFields();

            var attributes = new List<VertexInputAttributeDescription>();
            uint location = 0;

            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue;

                var desc = new VertexInputAttributeDescription();
                desc.Binding = 0;
                desc.Location = location;
                desc.Format = GetFormat(Marshal.SizeOf(field.FieldType));
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

        private static Format GetFormat(int size)
        {
            switch (size)
            {
                case 4:
                    return Format.R32_SFLOAT;
                case 8:
                    return Format.R32G32_SFLOAT;
                case 12:
                    return Format.R32G32B32_SFLOAT;
                case 16:
                    return Format.R32G32B32A32_SFLOAT;

                default:
                    throw new Exception($"size {size} is not supported");
            }
        }
    }
}
