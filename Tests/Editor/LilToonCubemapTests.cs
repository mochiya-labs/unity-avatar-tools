using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UniGLTF;
using UniJSON;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public class LilToonCubemapTests
    {
        [TestCase(false, false, true)]
        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        public void PreservesHdrFacesAndMipsWithoutReadableSource(bool vrm, bool inverseX, bool enabled)
        {
            var shader = Shader.Find("lilToon");
            Assert.That(shader, Is.Not.Null, "The cubemap integration suite requires lilToon.");
            var root = AvatarConversionTests.CreateAvatar();
            var material = new Material(shader) { name = "Cubemap fixture" };
            var cube = new Cubemap(4, TextureFormat.RGBAHalf, true) { name = "HDR six faces" };
            for (var mip = 0; mip < cube.mipmapCount; mip++)
            {
                var size = Math.Max(1, cube.width >> mip);
                for (var face = 0; face < 6; face++)
                {
                    // Distinct faces/mips and asymmetric corners reveal orientation errors.
                    var pixels = Enumerable.Range(0, size * size)
                        .Select(i => new Color(face + 1, mip + 0.25f, i / (float)(size * size), 1)).ToArray();
                    cube.SetPixels(pixels, (CubemapFace)face, mip);
                }
            }
            cube.Apply(false, true);
            material.SetTexture("_ReflectionCubeTex", cube);
            material.SetFloat("_UseReflection", enabled ? 1 : 0);
            material.SetFloat("_ApplyReflection", 1);
            material.SetFloat("_ReflectionCubeOverride", 1);
            material.SetColor("_ReflectionCubeColor", Color.white);
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var oldMaterial = renderer.sharedMaterial;
            renderer.sharedMaterial = material;
            var second = Object.Instantiate(material); second.name = "Shared cube";
            var extra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            extra.transform.SetParent(root.transform, false);
            extra.GetComponent<Renderer>().sharedMaterial = second;
            var before = EditorJsonUtility.ToJson(material);
            var path = Path.GetFullPath("MochiyaTests/cubemap-" + (vrm ? "vrm" : inverseX ? "x" : "z") + (enabled ? "" : "-disabled") + (vrm ? ".vrm" : ".glb"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                Assert.That(MochiyaExportValidation.Validate(root, false).Any(x => x.Severity == MochiyaExportIssueSeverity.Error), Is.False);
                if (vrm) MochiyaLilToonExporter.ExportVrm(root, path);
                else MochiyaLilToonExporter.ExportGlb(root, path, new GltfExportSettings { InverseAxis = inverseX ? Axes.X : Axes.Z });
                var bytes = File.ReadAllBytes(path);
                var jsonLength = BitConverter.ToInt32(bytes, 12);
                var json = System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength).ParseAsJson();
                var cubes = json["materials"].ArrayItems()
                    .Where(x => x["extensions"].ContainsKey("MOCHIYA_materials_liltoon"))
                    .Select(x => x["extensions"]["MOCHIYA_materials_liltoon"]["cubeTextures"]["_ReflectionCubeTex"]).ToArray();
                Assert.That(cubes.Length, Is.EqualTo(2));
                Assert.That(cubes[0].ToString(), Is.EqualTo(cubes[1].ToString()), "Shared cubes reuse the same binary data.");
                var levels = cubes[0]["mipmaps"].ArrayItems().Select(x => x.GetInt32()).ToArray();
                Assert.That(levels.Length, Is.EqualTo(3));
                var binStart = 20 + jsonLength + 8;
                for (var mip = 0; mip < levels.Length; mip++)
                {
                    var view = json["bufferViews"][levels[mip]];
                    var size = 4 >> mip;
                    var offset = binStart + view["byteOffset"].GetInt32();
                    Assert.That(view["byteLength"].GetInt32(), Is.EqualTo(size * size * 8 * 6));
                    for (var face = 0; face < 6; face++)
                    {
                        var sourceFace = face;
                        if ((vrm || inverseX) && face < 2) sourceFace ^= 1;
                        if (!vrm && !inverseX && face >= 4) sourceFace ^= 1;
                        var actual = Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, offset + face * size * size * 8));
                        var expected = (float)sourceFace + 1;
                        if (QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Gamma) expected = Mathf.GammaToLinearSpace(expected);
                        Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(0.02, expected * 0.005)), $"mip {mip}, face {face}");
                    }
                }
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
                Assert.That(cube.isReadable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(renderer.sharedMesh);
                Object.DestroyImmediate(root.GetComponent<Animator>().avatar);
                Object.DestroyImmediate(root); Object.DestroyImmediate(oldMaterial);
                Object.DestroyImmediate(second); Object.DestroyImmediate(material); Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void SupportsBaseOnlyCubemapButRejectsOtherSlotsAndRenderTextures()
        {
            var cube = new Cubemap(2, TextureFormat.RGBA32, false);
            var live = new RenderTexture(2, 2, 0) { dimension = UnityEngine.Rendering.TextureDimension.Cube };
            try
            {
                Assert.That(LilToonCubemapExporter.IsSupported("_ReflectionCubeTex", cube), Is.True);
                Assert.That(LilToonCubemapExporter.IsSupported("_MainTex", cube), Is.False);
                Assert.That(LilToonCubemapExporter.IsSupported("_ReflectionCubeTex", live), Is.False);
            }
            finally { Object.DestroyImmediate(cube); Object.DestroyImmediate(live); }
        }
    }
}
