using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor.Tests
{
    public sealed class MochiyaLilToonExportWindowTests
    {
        [Test]
        public void VrmExportSettingsAreEditable()
        {
            var window = ScriptableObject.CreateInstance<MochiyaLilToonExportWindow>();
            try
            {
                var settingsField = typeof(MochiyaLilToonExportWindow).GetField(
                    "_vrmExportSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var settings = settingsField?.GetValue(window) as Object;

                Assert.That(settings, Is.Not.Null);
                Assert.That(settings.hideFlags & HideFlags.NotEditable, Is.EqualTo(HideFlags.None));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
