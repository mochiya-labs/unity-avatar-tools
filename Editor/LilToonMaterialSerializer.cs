using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UniGLTF;
using UniJSON;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mochiya.LilToon.Exporter.Editor
{
    internal static class LilToonMaterialSerializer
    {
        public const string ExtensionName = "MOCHIYA_materials_liltoon";
        public const string SpecVersion = "1.0";

        private static readonly Regex DataTextureName = new Regex(
            "(_Mask|Mask$|Normal|Bump|Dither|Parallax|Noise|UDIM|AudioLink)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void Attach(glTFMaterial destination, Material source, ITextureExporter textureExporter)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (textureExporter == null) throw new ArgumentNullException(nameof(textureExporter));

            var formatter = new JsonFormatter();
            formatter.BeginMap();

            formatter.Key("specVersion");
            formatter.Value(SpecVersion);
            formatter.Key("lilToonVersion");
            formatter.Value(GetLilToonVersion(source.shader));
            formatter.Key("shaderVariant");
            formatter.Value(source.shader.name);
            formatter.Key("renderMode");
            formatter.Value(LilToonMaterialSupport.GetRenderMode(source));

            formatter.Key("properties");
            WriteProperties(formatter, source);
            formatter.Key("textures");
            WriteTextures(formatter, source, textureExporter);

            formatter.EndMap();
            glTFExtensionExport.GetOrCreate(ref destination.extensions)
                .Add(ExtensionName, formatter.GetStore().Bytes);
        }

        private static string GetLilToonVersion(Shader shader)
        {
            var assetPath = shader != null ? AssetDatabase.GetAssetPath(shader) : null;
            if (!string.IsNullOrEmpty(assetPath))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (package != null && !string.IsNullOrEmpty(package.version)) return package.version;
            }
            return "unknown";
        }

        private static void WriteProperties(JsonFormatter formatter, Material material)
        {
            formatter.BeginMap();
            var shader = material.shader;
            var serializedNames = new HashSet<string>(StringComparer.Ordinal);
            var count = shader.GetPropertyCount();

            for (var index = 0; index < count; ++index)
            {
                var name = shader.GetPropertyName(index);
                var type = shader.GetPropertyType(index);
                if (type == ShaderPropertyType.Texture) continue;

                formatter.Key(name);
                serializedNames.Add(name);
                switch (type)
                {
                    case ShaderPropertyType.Color:
                    {
                        // glTF color factors and Three.js shader uniforms are linear values.
                        var value = material.GetColor(name).linear;
                        WriteVector4(formatter, value.r, value.g, value.b, value.a);
                        break;
                    }
                    case ShaderPropertyType.Vector:
                    {
                        var value = material.GetVector(name);
                        WriteVector4(formatter, value.x, value.y, value.z, value.w);
                        break;
                    }
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                    case ShaderPropertyType.Int:
                        formatter.Value(material.GetFloat(name));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported shader property type {type} for {name}.");
                }
            }

            for (var index = 0; index < count; ++index)
            {
                if (shader.GetPropertyType(index) != ShaderPropertyType.Texture) continue;
                var textureName = shader.GetPropertyName(index);
                var transformName = textureName + "_ST";
                if (serializedNames.Contains(transformName) || material.GetTexture(textureName) == null) continue;

                var scale = material.GetTextureScale(textureName);
                var offset = material.GetTextureOffset(textureName);
                (scale, offset) = TextureTransform.VerticalFlipScaleOffset(scale, offset);
                formatter.Key(transformName);
                WriteVector4(formatter, scale.x, scale.y, offset.x, offset.y);
            }

            formatter.EndMap();
        }

        private static void WriteTextures(JsonFormatter formatter, Material material, ITextureExporter textureExporter)
        {
            formatter.BeginMap();
            var shader = material.shader;
            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; ++index)
            {
                if (shader.GetPropertyType(index) != ShaderPropertyType.Texture) continue;
                var name = shader.GetPropertyName(index);
                var texture = material.GetTexture(name);
                if (texture == null) continue;

                var attributes = shader.GetPropertyAttributes(index);
                var isNormal = HasNormalAttribute(attributes);
                var textureIndex = isNormal
                    ? textureExporter.RegisterExportingAsNormal(texture)
                    : DataTextureName.IsMatch(name)
                        ? textureExporter.RegisterExportingAsLinear(texture, true)
                        : textureExporter.RegisterExportingAsSRgb(texture, true);

                formatter.Key(name);
                formatter.BeginMap();
                formatter.Key("index");
                formatter.Value(textureIndex);
                formatter.Key("texCoord");
                formatter.Value(0);
                formatter.EndMap();
            }
            formatter.EndMap();
        }

        private static bool HasNormalAttribute(string[] attributes)
        {
            if (attributes == null) return false;
            foreach (var attribute in attributes)
            {
                if (string.Equals(attribute, "Normal", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void WriteVector4(JsonFormatter formatter, float x, float y, float z, float w)
        {
            formatter.BeginList();
            formatter.Value(x);
            formatter.Value(y);
            formatter.Value(z);
            formatter.Value(w);
            formatter.EndList();
        }
    }
}
