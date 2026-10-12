using System;
using System.Collections.Generic;
using UniGLTF;
using UniJSON;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    // Cubes are extension-owned binary data, never ordinary glTF 2D textures.
    internal sealed class LilToonCubemapExporter
    {
        internal const string Property = "_ReflectionCubeTex";
        private readonly ExportingGltfData storage;
        private readonly Axes inverseAxis;
        private readonly Dictionary<Cubemap, int[]> exported = new Dictionary<Cubemap, int[]>();

        public LilToonCubemapExporter(ExportingGltfData storage, Axes inverseAxis)
        { this.storage = storage; this.inverseAxis = inverseAxis; }

        internal static bool IsSupported(string property, Texture texture)
        {
            return property == Property && texture is Cubemap cube &&
                Mathf.IsPowerOfTwo(cube.width) &&
                (cube.mipmapCount == 1 || cube.mipmapCount == 1 + Mathf.RoundToInt(Mathf.Log(cube.width, 2)));
        }

        public void Write(JsonFormatter formatter, Material source)
        {
            formatter.BeginMap();
            if (source.HasProperty(Property) && source.GetTexture(Property) != null)
            {
                var texture = source.GetTexture(Property);
                if (!IsSupported(Property, texture))
                    throw new NotSupportedException($"Material '{source.name}': {Property} requires a power-of-two Cubemap asset with one level or a complete mip chain. Cube RenderTextures are not supported.");
                var cube = (Cubemap)texture;
                if (!exported.TryGetValue(cube, out var levels))
                {
                    levels = Export(cube);
                    exported.Add(cube, levels);
                }
                formatter.Key(Property); formatter.BeginMap();
                formatter.Key("encoding"); formatter.Value("rgba16f-linear");
                formatter.Key("size"); formatter.Value(cube.width);
                formatter.Key("mipmaps"); formatter.BeginList();
                foreach (var index in levels) formatter.Value(index);
                formatter.EndList(); formatter.EndMap();
            }
            formatter.EndMap();
        }

        private int[] Export(Cubemap cube)
        {
            var shader = Shader.Find("Hidden/Mochiya/CubemapExport");
            if (shader == null || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                throw new NotSupportedException($"Cubemap '{cube.name}' requires a graphics-enabled Unity Editor with half-float render textures.");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var previousTarget = RenderTexture.active;
            var previousSrgb = GL.sRGBWrite;
            try
            {
                material.SetTexture("_Cube", cube);
                material.SetVector("_Axis", inverseAxis == Axes.X ? new Vector4(-1, 1, 1, 0) : new Vector4(1, 1, -1, 0));
                var levels = new int[cube.mipmapCount];
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    var size = Math.Max(1, cube.width >> mip);
                    var pixels = new byte[checked(size * size * 8 * 6)];
                    var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    Texture2D readback = null;
                    try
                    {
                        readback = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true);
                        material.SetFloat("_Mip", mip);
                        GL.sRGBWrite = false;
                        for (var face = 0; face < 6; face++)
                        {
                            material.SetFloat("_Face", face);
                            Graphics.Blit(null, target, material);
                            RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                            var bytes = readback.GetRawTextureData<byte>().ToArray();
                            if (!BitConverter.IsLittleEndian)
                                for (var i = 0; i < bytes.Length; i += 2) { var b = bytes[i]; bytes[i] = bytes[i + 1]; bytes[i + 1] = b; }
                            for (var i = 0; i < bytes.Length; i += 2)
                                if (((bytes[i] | bytes[i + 1] << 8) & 0x7c00) == 0x7c00)
                                    throw new InvalidOperationException($"Cubemap '{cube.name}' contains non-finite or out-of-range HDR values at mip {mip}.");
                            Buffer.BlockCopy(bytes, 0, pixels, face * bytes.Length, bytes.Length);
                        }
                        levels[mip] = storage.ExtendBufferAndGetViewIndex(pixels);
                    }
                    finally
                    {
                        RenderTexture.active = previousTarget;
                        if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                        RenderTexture.ReleaseTemporary(target);
                    }
                }
                return levels;
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
