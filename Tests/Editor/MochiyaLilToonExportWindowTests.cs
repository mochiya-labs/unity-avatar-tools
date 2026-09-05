using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor.Tests
{
    public sealed class MochiyaLilToonExportWindowTests
    {
        [Test]
        public void ClosingEitherWindowDoesNotDestroyItsSharedExportProfile()
        {
            var profile = ScriptableObject.CreateInstance<MochiyaExportProfile>();
            var window = ScriptableObject.CreateInstance<MochiyaLilToonExportWindow>();
            var avatarWindow = ScriptableObject.CreateInstance<MochiyaAvatarToolsWindow>();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var exportField = typeof(MochiyaLilToonExportWindow).GetField("_profile", flags);
                var avatarField = typeof(MochiyaAvatarToolsWindow).GetField("profile", flags);
                Assert.That(exportField.GetValue(window), Is.SameAs(MochiyaExportProfile.Default));
                Assert.That(avatarField.GetValue(avatarWindow), Is.SameAs(MochiyaExportProfile.Default));
                exportField.SetValue(window, profile); avatarField.SetValue(avatarWindow, profile);
                Object.DestroyImmediate(window); Object.DestroyImmediate(avatarWindow);
                Assert.That(profile != null, Is.True);
                Assert.That(profile.hideFlags & HideFlags.NotEditable, Is.EqualTo(HideFlags.None));
            }
            finally
            {
                if (window != null) Object.DestroyImmediate(window);
                if (avatarWindow != null) Object.DestroyImmediate(avatarWindow);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
