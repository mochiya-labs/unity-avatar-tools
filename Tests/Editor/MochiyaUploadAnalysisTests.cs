using NUnit.Framework;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class MochiyaUploadAnalysisTests
    {
        [Test] public void UnchangedInputNeverReanalyzesAndInvalidationsAreThrottled()
        {
            var source = new GameObject("source"); int detections = 0, validations = 0;
            using (var cache = new MochiyaUploadAnalysis(
                root => { detections++; return new MochiyaAvatarTarget { Root = root }; },
                (root, vrm, profile) => { validations++; return new MochiyaConversionReport(); }))
            try
            {
                cache.Refresh(source, null, true, false, 0);
                for (int i = 0; i < 1000; i++) cache.Refresh(source, null, true, false, i * .01);
                Assert.That(detections, Is.EqualTo(1)); Assert.That(validations, Is.Zero);
                cache.Refresh(source, null, true, true, 10);
                Assert.That(validations, Is.EqualTo(1));
                for (int i = 0; i < 20; i++) { cache.Invalidate(); cache.Refresh(source, null, true, true, 10 + i * .001); }
                Assert.That(validations, Is.EqualTo(1));
                cache.Refresh(source, null, true, true, 10.3);
                Assert.That(validations, Is.EqualTo(2));
                cache.Refresh(source, null, false, true, 11);
                Assert.That(validations, Is.EqualTo(3));
                cache.Refresh(null, null, false, true, 12);
                Assert.That(cache.Target, Is.Null); Assert.That(cache.Report, Is.Null);
                cache.Dispose(); cache.Invalidate(); cache.Refresh(source, null, true, true, 13);
                Assert.That(validations, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
