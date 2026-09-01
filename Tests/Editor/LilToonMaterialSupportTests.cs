using NUnit.Framework;

namespace Mochiya.LilToon.Exporter.Editor.Tests
{
    public sealed class LilToonMaterialSupportTests
    {
        [TestCase("lilToon")]
        [TestCase("Hidden/lilToonOutline")]
        [TestCase("Hidden/lilToonCutout")]
        [TestCase("Hidden/lilToonCutoutOutline")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        public void StandardVariantsAreSupported(string shaderName)
        {
            Assert.That(LilToonMaterialSupport.IsSupportedShaderName(shaderName), Is.True);
        }

        [TestCase("Hidden/lilToonLite")]
        [TestCase("_lil/lilToonMulti")]
        [TestCase("Hidden/lilToonFur")]
        [TestCase("Hidden/lilToonRefraction")]
        [TestCase("Hidden/lilToonGem")]
        [TestCase("Hidden/lilToonTessellation")]
        public void SpecializedVariantsAreRejected(string shaderName)
        {
            Assert.That(LilToonMaterialSupport.IsLilToonShaderName(shaderName), Is.True);
            Assert.That(LilToonMaterialSupport.IsSupportedShaderName(shaderName), Is.False);
        }
    }
}
