using NUnit.Framework;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class LilToonMaterialSupportTests
    {
        [TestCase("lilToon")]
        [TestCase("Hidden/lilToonOutline")]
        [TestCase("Hidden/lilToonCutout")]
        [TestCase("Hidden/lilToonCutoutOutline")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        [TestCase("Hidden/lilToonRefraction")]
        [TestCase("Hidden/lilToonFur")]
        [TestCase("Hidden/lilToonGem")]
        [TestCase("Hidden/lilToonRefractionBlur")]
        [TestCase("Hidden/lilToonFurCutout")]
        [TestCase("Hidden/lilToonFurTwoPass")]
        [TestCase("Hidden/lilToonOnePassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonOnePassTransparentOutline")]
        [TestCase("Hidden/lilToonTwoPassTransparentOutline")]
        public void StandardVariantsAreSupported(string shaderName)
        {
            Assert.That(LilToonMaterialSupport.IsSupportedShaderName(shaderName), Is.True);
        }

        [TestCase("Hidden/lilToonLite")]
        [TestCase("_lil/lilToonMulti")]
        [TestCase("Hidden/lilToonFurOnly")]
        [TestCase("Hidden/lilToonTessellation")]
        public void SpecializedVariantsAreRejected(string shaderName)
        {
            Assert.That(LilToonMaterialSupport.IsLilToonShaderName(shaderName), Is.True);
            Assert.That(LilToonMaterialSupport.IsSupportedShaderName(shaderName), Is.False);
        }

        [TestCase("Hidden/lilToonRefraction", "refraction")]
        [TestCase("Hidden/lilToonFur", "fur")]
        [TestCase("Hidden/lilToonGem", "gem")]
        [TestCase("Hidden/lilToonRefractionBlur", "refraction-blur")]
        [TestCase("Hidden/lilToonFurCutout", "fur-cutout")]
        [TestCase("Hidden/lilToonFurTwoPass", "fur-two-pass")]
        [TestCase("Hidden/lilToonTransparent", "transparent", "normal")]
        [TestCase("Hidden/lilToonOnePassTransparent", "transparent", "one-pass")]
        [TestCase("Hidden/lilToonTwoPassTransparent", "transparent", "two-pass")]
        [TestCase("Hidden/lilToonOnePassTransparentOutline", "transparent", "one-pass")]
        [TestCase("Hidden/lilToonTwoPassTransparentOutline", "transparent", "two-pass")]
        public void SpecialModesKeepTheirIdentity(string shaderName, string mode, string transparency = "normal")
        {
            var shader = UnityEngine.Shader.Find(shaderName);
            if (shader == null) Assert.Ignore("Install lilToon to verify material export.");
            var material = new UnityEngine.Material(shader);
            try
            {
                Assert.That(LilToonMaterialSupport.IsSupported(material, out var reason), Is.True, reason);
                Assert.That(LilToonMaterialSupport.GetRenderMode(material), Is.EqualTo(mode));
                var root = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
                var path = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "liltoon-mode-" + System.Guid.NewGuid() + ".glb");
                root.GetComponent<UnityEngine.Renderer>().sharedMaterial = material;
                var before = UnityEditor.EditorJsonUtility.ToJson(material);
                try
                {
                    MochiyaLilToonExporter.ExportGlb(root, path);
                    var bytes = System.IO.File.ReadAllBytes(path);
                    var json = System.Text.Encoding.UTF8.GetString(bytes, 20, System.BitConverter.ToInt32(bytes, 12));
                    StringAssert.Contains("\"renderMode\":\"" + mode + "\"", json);
                    StringAssert.Contains("\"specVersion\":\"1.2\"", json);
                    if (mode == "transparent") StringAssert.Contains("\"transparencyMode\":\"" + transparency + "\"", json);
                    if (mode == "fur-cutout") StringAssert.Contains("\"alphaMode\":\"MASK\"", json);
                    Assert.That(UnityEditor.EditorJsonUtility.ToJson(material), Is.EqualTo(before));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
