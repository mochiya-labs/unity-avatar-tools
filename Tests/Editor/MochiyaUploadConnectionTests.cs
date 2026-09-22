using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class MochiyaUploadConnectionTests
    {
        private const string Origin = "https://mochiya.org";
        private sealed class Store : IMochiyaCredentialStore
        {
            internal string Value;
            public string Read() => Value;
            public void Write(string value) { Value = value; }
            public void Delete() { Value = null; }
        }
        private static Task<MochiyaUploadCapabilities> Valid() => Task.FromResult(new MochiyaUploadCapabilities());

        [Test]
        public async Task SuccessfulAuthenticationSurvivesNewConnectionAndManualSignOutRemovesIt()
        {
            var store = new Store();
            var connection = new MochiyaUploadConnection(store);
            Assert.IsFalse(connection.Restore(Origin));
            await connection.Authenticate("test-key", Origin, Valid, CancellationToken.None);
            var reopened = new MochiyaUploadConnection(store);
            Assert.IsTrue(reopened.Restore(Origin));
            Assert.AreEqual("test-key", reopened.Token);
            Assert.AreEqual(Origin, reopened.Origin);
            var validated = false;
            await reopened.Authenticate(reopened.Token, Origin, () => { validated = true; return Valid(); }, CancellationToken.None);
            Assert.IsTrue(validated);
            reopened.SignOut();
            Assert.IsNull(reopened.Token);
            Assert.IsNull(store.Value);
            Assert.IsFalse(new MochiyaUploadConnection(store).Restore(Origin));
        }

        [TestCase("http://localhost:3000")]
        [TestCase("https://preview.vercel.app")]
        public async Task ServerChangeDeletesSavedKeyBeforeItCanBeReused(string otherOrigin)
        {
            var store = new Store();
            await new MochiyaUploadConnection(store).Authenticate("test-key", Origin, Valid, CancellationToken.None);
            var reopened = new MochiyaUploadConnection(store);
            Assert.IsFalse(reopened.Restore(otherOrigin));
            Assert.IsNull(reopened.Token);
            Assert.IsNull(store.Value);
            Assert.IsFalse(reopened.Restore(Origin));
        }

        [TestCase(401)]
        [TestCase(500)]
        [TestCase(0)]
        public async Task FailedReauthenticationClearsSavedKey(int status)
        {
            var store = new Store();
            var connection = new MochiyaUploadConnection(store);
            await connection.Authenticate("test-key", Origin, Valid, CancellationToken.None);
            var reopened = new MochiyaUploadConnection(store);
            Assert.IsTrue(reopened.Restore(Origin));
            Assert.ThrowsAsync<MochiyaUploadApiException>(async () => await reopened.Authenticate(reopened.Token, Origin,
                () => Task.FromException<MochiyaUploadCapabilities>(new MochiyaUploadApiException(status, "Failed")), CancellationToken.None));
            Assert.IsNull(store.Value);
            Assert.IsNull(reopened.Token);
        }

        [Test]
        public void FailedFirstAuthenticationDoesNotSaveKey()
        {
            var store = new Store();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await new MochiyaUploadConnection(store).Authenticate("wrong-key", Origin,
                () => Task.FromException<MochiyaUploadCapabilities>(new InvalidOperationException()), CancellationToken.None));
            Assert.IsNull(store.Value);
        }

        [Test]
        public async Task ClosingDuringReauthenticationPreservesSavedKeyButCannotSaveLateResult()
        {
            var store = new Store();
            var connection = new MochiyaUploadConnection(store);
            await connection.Authenticate("original-key", Origin, Valid, CancellationToken.None);
            var pending = new TaskCompletionSource<MochiyaUploadCapabilities>();
            using (var cancellation = new CancellationTokenSource())
            {
                var task = connection.Authenticate("replacement-key", Origin, () => pending.Task, cancellation.Token);
                cancellation.Cancel(); pending.SetResult(new MochiyaUploadCapabilities());
                Assert.CatchAsync<OperationCanceledException>(async () => await task);
            }
            var reopened = new MochiyaUploadConnection(store);
            Assert.IsTrue(reopened.Restore(Origin));
            Assert.AreEqual("original-key", reopened.Token);
        }

        [Test]
        public void SignOutDuringAuthenticationCannotBeUndoneByLateSuccess()
        {
            var store = new Store();
            var connection = new MochiyaUploadConnection(store);
            var pending = new TaskCompletionSource<MochiyaUploadCapabilities>();
            var task = connection.Authenticate("test-key", Origin, () => pending.Task, CancellationToken.None);
            connection.SignOut(); pending.SetResult(new MochiyaUploadCapabilities());
            Assert.CatchAsync<OperationCanceledException>(async () => await task);
            Assert.IsNull(store.Value);
        }

        [TestCase("not-json")]
        [TestCase("{\"version\":99,\"origin\":\"https://mochiya.org\",\"token\":\"test-key\"}")]
        [TestCase("{\"version\":1,\"origin\":\"https://mochiya.org\",\"token\":\"\"}")]
        public void InvalidSavedRecordIsRemoved(string value)
        {
            var store = new Store { Value = value };
            Assert.IsFalse(new MochiyaUploadConnection(store).Restore(Origin));
            Assert.IsNull(store.Value);
        }

        [TestCase(0, true)]
        [TestCase(401, true)]
        [TestCase(403, true)]
        [TestCase(500, true)]
        [TestCase(503, true)]
        [TestCase(400, false)]
        [TestCase(409, false)]
        [TestCase(429, false)]
        public void OnlyAuthenticationAndServerFailuresInvalidateUploadConnection(int status, bool expected)
        {
            Assert.AreEqual(expected, new MochiyaUploadApiException(status, "Failed").InvalidatesConnection);
        }

        [Test]
        public void ProjectKeysAreStableAndDifferentProjectsAreIsolated()
        {
            var root = Path.Combine(Path.GetTempPath(), "mochiya-credential-test");
            Assert.AreEqual(MochiyaCredentialStore.ProjectKey(root), MochiyaCredentialStore.ProjectKey(root + Path.DirectorySeparatorChar));
            Assert.AreNotEqual(MochiyaCredentialStore.ProjectKey(root), MochiyaCredentialStore.ProjectKey(root + "-other"));
            StringAssert.DoesNotContain(root, MochiyaCredentialStore.ProjectKey(root));
        }

        [Test]
        public async Task ClosingWindowPreservesCredentialButSignOutClearsRecoveryAndSecretFields()
        {
            var store = new Store();
            var connection = new MochiyaUploadConnection(store);
            await connection.Authenticate("fictitious-key", Origin, Valid, CancellationToken.None);
            var window = UnityEngine.ScriptableObject.CreateInstance<MochiyaUploadWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(MochiyaUploadWindow);
            try
            {
                type.GetField("connection", flags).SetValue(window, connection);
                type.GetField("token", flags).SetValue(window, "fictitious-key");
                type.GetField("recoveryId", flags).SetValue(window, "old-upload");
                type.GetField("productId", flags).SetValue(window, "old-product");
                Assert.IsTrue(type.GetField("token", flags).IsNotSerialized);
                type.GetMethod("OnDisable", flags).Invoke(window, null);
                Assert.IsNotNull(store.Value);
                Assert.AreEqual("", type.GetField("token", flags).GetValue(window));
                Assert.AreEqual("old-upload", type.GetField("recoveryId", flags).GetValue(window));
                type.GetMethod("SignOut", flags).Invoke(window, null);
                Assert.IsNull(store.Value);
                Assert.AreEqual("", type.GetField("recoveryId", flags).GetValue(window));
                Assert.AreEqual("", type.GetField("productId", flags).GetValue(window));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void NativeCredentialStoreRoundTripAndProjectIsolation()
        {
            var path = Path.Combine(Path.GetTempPath(), "MochiyaCredentialTest-" + Guid.NewGuid().ToString("N"));
            var store = new MochiyaCredentialStore(path);
            var other = new MochiyaCredentialStore(path + "-other");
            try
            {
                Assert.IsNull(store.Read());
                store.Write("fictitious-key");
                Assert.AreEqual("fictitious-key", new MochiyaCredentialStore(path).Read());
                Assert.IsNull(other.Read());
                store.Write("replacement-fictitious-key");
                Assert.AreEqual("replacement-fictitious-key", store.Read());
                store.Delete();
                Assert.IsNull(store.Read());
                store.Delete();
            }
            finally { store.Delete(); }
        }
    }
}
