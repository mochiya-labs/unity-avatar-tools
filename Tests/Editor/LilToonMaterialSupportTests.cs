using UniJSON;
using NUnit.Framework;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class LilToonMaterialSupportTests
    {
        [TestCase(false, 2450, 3000)]
        [TestCase(true, 2450, 3000)]
        [TestCase(false, -1, -1)]
        public void ExportPreservesEffectiveQueuesWithoutChangingSource(bool vrm, int innerQueue, int outerQueue)
        {
            var shader = UnityEngine.Shader.Find("Hidden/lilToonTransparent");
            Assert.That(shader, Is.Not.Null, "Queue round-trip requires lilToon.");
            var root = AvatarConversionTests.CreateAvatar();
            var inner = new UnityEngine.Material(shader) { name = "QueueInner", renderQueue = innerQueue };
            var outer = new UnityEngine.Material(shader) { name = "QueueOuter", renderQueue = outerQueue };
            inner.SetFloat("_ZWrite", 1); outer.SetFloat("_ZWrite", 1);
            root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMaterial = inner;
            var shell = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            shell.transform.SetParent(root.transform, false);
            shell.GetComponent<UnityEngine.Renderer>().sharedMaterial = outer;
            var innerBefore = UnityEditor.EditorJsonUtility.ToJson(inner);
            var outerBefore = UnityEditor.EditorJsonUtility.ToJson(outer);
            var path = System.IO.Path.GetFullPath("MochiyaTests/queues-" + (vrm ? "vrm" : innerQueue < 0 ? "default" : "glb") + (vrm ? ".vrm" : ".glb"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            try
            {
                if (vrm) MochiyaLilToonExporter.ExportVrm(root, path);
                else MochiyaLilToonExporter.ExportGlb(root, path);
                var bytes = System.IO.File.ReadAllBytes(path);
                var json = System.Text.Encoding.UTF8.GetString(bytes, 20, System.BitConverter.ToInt32(bytes, 12)).ParseAsJson();
                var found = 0;
                foreach (var material in json["materials"].ArrayItems())
                {
                    var name = material["name"].GetString();
                    if (name != "QueueInner" && name != "QueueOuter") continue;
                    var extension = material["extensions"]["MOCHIYA_materials_liltoon"];
                    Assert.That(extension["renderQueue"].GetInt32(), Is.EqualTo(name == "QueueInner" ? inner.renderQueue : outer.renderQueue));
                    Assert.That(extension["renderMode"].GetString(), Is.EqualTo("transparent"));
                    Assert.That(extension["properties"]["_ZWrite"].GetSingle(), Is.EqualTo(1));
                    found++;
                }
                Assert.That(found, Is.EqualTo(2));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(inner), Is.EqualTo(innerBefore));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(outer), Is.EqualTo(outerBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(inner);
                UnityEngine.Object.DestroyImmediate(outer);
            }
        }

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
                    StringAssert.Contains("\"specVersion\":\"1.3\"", json);
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
