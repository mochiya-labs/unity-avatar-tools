using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class MochiyaUploadTests
    {
        [TestCase("https://preview.vercel.app", "https://preview.vercel.app", true)]
        [TestCase(null, "https://preview.vercel.app", false)]
        [TestCase("https://preview.vercel.app", "https://other.vercel.app", false)]
        [TestCase("https://mochiya.org", "https://mochiya.org", false)]
        [TestCase("https://www.mochiya.org", "https://www.mochiya.org", false)]
        [TestCase("http://localhost:3000", "http://localhost:3000", false)]
        [TestCase("https://preview.vercel.app.evil.example", "https://preview.vercel.app.evil.example", false)]
        [TestCase("https://preview.vercel.app?secret=wrong", "https://preview.vercel.app", false)]
        [TestCase("https://preview.vercel.app:444", "https://preview.vercel.app:444", false)]
        public void BypassIsDevelopmentOnlyAndBoundToExplicitPreview(string configuredOrigin, string origin, bool expected)
        {
            var previousOrigin = Environment.GetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL");
            var previousSecret = Environment.GetEnvironmentVariable("MOCHIYA_UPLOAD_VERCEL_BYPASS_TOKEN");
            try
            {
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL", configuredOrigin);
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_VERCEL_BYPASS_TOKEN", "test-bypass");
                using (var client = new MochiyaUploadClient("test-api-key", origin))
                using (var request = client.CreateApiRequest("/api/v1/creator/uploads", "GET", null))
                {
#if !MOCHIYA_DEVELOPMENT
                    expected = false;
                    Assert.AreEqual("https://www.mochiya.org/api/v1/creator/uploads", request.url);
#endif
                    Assert.AreEqual(expected ? "test-bypass" : null, request.GetRequestHeader("x-vercel-protection-bypass"));
                    Assert.AreEqual("Bearer test-api-key", request.GetRequestHeader("Authorization"));
                    Assert.AreEqual(0, request.redirectLimit);
                    StringAssert.DoesNotContain("test-bypass", request.url);
                }
                using (var production = new MochiyaUploadClient("test-api-key"))
                using (var request = production.CreateApiRequest("/api/v1/creator/uploads", "GET", null))
                {
                    Assert.AreEqual("https://www.mochiya.org/api/v1/creator/uploads", request.url);
                    Assert.IsNull(request.GetRequestHeader("x-vercel-protection-bypass"));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL", previousOrigin);
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_VERCEL_BYPASS_TOKEN", previousSecret);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("http://localhost:3000/")]
        [TestCase("https://preview.vercel.app")]
        public void EnvironmentOriginRequiresDevelopmentAndResetsWhenRemoved(string configuredOrigin)
        {
            var previous = Environment.GetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL");
            try
            {
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL", configuredOrigin);
                var expected = MochiyaUploadClient.ProductionOrigin;
#if MOCHIYA_DEVELOPMENT
                if (!string.IsNullOrWhiteSpace(configuredOrigin)) expected = configuredOrigin.Trim().TrimEnd('/');
#endif
                Assert.AreEqual(expected, MochiyaUploadClient.ConfiguredOrigin);
                using (var client = new MochiyaUploadClient("test-api-key", MochiyaUploadClient.ConfiguredOrigin))
                using (var request = client.CreateApiRequest("/api/v1/creator/upload-capabilities", "GET", null))
                    Assert.AreEqual(expected + "/api/v1/creator/upload-capabilities", request.url);
                Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL", null);
                Assert.AreEqual(MochiyaUploadClient.ProductionOrigin, MochiyaUploadClient.ConfiguredOrigin);
            }
            finally { Environment.SetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL", previous); }
        }

        [Test]
        public void PrivateRequestUsesJsonNullPriceAndHasNoCredentials()
        {
            var request = new MochiyaUploadRequest { idempotencyKey = Guid.NewGuid().ToString(), item = new MochiyaItemInput { title = "日本語のアバター", status = "private" }, files = new[] { new MochiyaUploadFile { id = "model", role = "model", name = "avatar.vrm", size = 524288000, contentType = "application/octet-stream" } } };
            var json = request.ToJson();
            StringAssert.Contains("\"priceCents\":null", json);
            StringAssert.Contains("日本語のアバター", json);
            StringAssert.DoesNotContain("token", json);
            StringAssert.DoesNotContain("storagePath", json);
            request.item.status = "public"; request.item.priceCents = 123;
            StringAssert.Contains("\"priceCents\":123", request.ToJson());
        }
        [Test]
        public void ContractLoadsAllLocalesAndLargeFileBudgetsWithoutIntegerOverflow()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/org.mochiya.avatar-tools/Editor/UploadContract.json");
            Assert.NotNull(asset);
            var contract = JsonUtility.FromJson<MochiyaUploadContract>(asset.text);
            Assert.AreEqual(2147483648L, contract.limits.totalBytes);
            Assert.AreEqual(4, contract.locales.Length);
            Assert.Contains("base_avatar", contract.categories);
            foreach (var locale in contract.locales) Assert.That(locale.labels.Length, Is.GreaterThan(30));
        }
    }
}
