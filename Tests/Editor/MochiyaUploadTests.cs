using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class MochiyaUploadTests
    {
        [Test]
        public void PrivateRequestUsesJsonNullPriceAndHasNoCredentials()
        {
            var request = new MochiyaUploadRequest { idempotencyKey = Guid.NewGuid().ToString(), item = new MochiyaItemInput { title = "日本語のアバター", purchaseMode = "private" }, files = new[] { new MochiyaUploadFile { id = "model", role = "model", name = "avatar.vrm", size = 524288000, contentType = "application/octet-stream" } } };
            var json = request.ToJson();
            StringAssert.Contains("\"priceCents\":null", json);
            StringAssert.Contains("日本語のアバター", json);
            StringAssert.DoesNotContain("token", json);
            StringAssert.DoesNotContain("storagePath", json);
            request.item.purchaseMode = "mochiya"; request.item.priceCents = 123;
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
