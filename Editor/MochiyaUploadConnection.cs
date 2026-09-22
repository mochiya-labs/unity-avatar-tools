using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    internal interface IMochiyaCredentialStore
    {
        string Read();
        void Write(string value);
        void Delete();
    }

    /// <summary>A project-local sign-in, revalidated before reuse and bound to one server.</summary>
    internal sealed class MochiyaUploadConnection
    {
        [Serializable] private sealed class Record { public int version = 1; public string origin, token; }
        private readonly IMochiyaCredentialStore store;
        private int generation;
        internal string Token { get; private set; }
        internal string Origin { get; private set; }

        internal MochiyaUploadConnection(IMochiyaCredentialStore store) { this.store = store; }

        internal bool Restore(string origin)
        {
            var json = store.Read();
            if (string.IsNullOrEmpty(json)) return false;
            Record record = null;
            try { record = JsonUtility.FromJson<Record>(json); } catch (ArgumentException) { }
            if (record == null || record.version != 1 || string.IsNullOrWhiteSpace(record.token) || record.origin != origin)
            { SignOut(); return false; }
            Token = record.token; Origin = record.origin;
            return true;
        }

        internal async Task<MochiyaUploadCapabilities> Authenticate(string token, string origin,
            Func<Task<MochiyaUploadCapabilities>> validate, CancellationToken cancellation)
        {
            var attempt = ++generation;
            try
            {
                var capabilities = await validate();
                cancellation.ThrowIfCancellationRequested();
                if (attempt != generation) throw new OperationCanceledException();
                store.Write(JsonUtility.ToJson(new Record { origin = origin, token = token }));
                Token = token; Origin = origin;
                return capabilities;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (attempt == generation) SignOut();
                throw;
            }
        }

        internal void SignOut()
        {
            generation++;
            Token = null; Origin = null;
            store.Delete();
        }
    }
}
