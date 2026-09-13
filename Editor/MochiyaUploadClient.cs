using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Mochiya.AvatarTools.Editor
{
    [Serializable] public sealed class MochiyaUploadPair { public string name, value; }
    [Serializable] public sealed class MochiyaUploadLimits
    {
        public int version, title, tags, tag, gallery, additionalFiles, specifications, requirements, credits, licenseText, maxPriceCents;
        public long imageBytes, fileBytes, totalBytes;
    }
    [Serializable] public sealed class MochiyaUploadPricing { public int processingBasisPoints, processingFixedCents, commissionBasisPoints; }
    [Serializable] public sealed class MochiyaUploadCapabilities
    {
        public MochiyaUploadLimits limits; public string[] categories; public string displayName;
        public MochiyaUploadPair[] labels; public MochiyaUploadPricing pricing;
    }
    [Serializable] public sealed class MochiyaUploadLocale { public string name; public MochiyaUploadPair[] labels; }
    [Serializable] public sealed class MochiyaUploadContract { public MochiyaUploadLimits limits; public string[] categories; public MochiyaUploadLocale[] locales; }
    [Serializable] public sealed class MochiyaItemInput
    {
        public string title = "", category = "base_avatar", status = "private";
        public string[] tags = Array.Empty<string>(); public int priceCents;
        public string specifications = "", requirements = "", credits = "", licenseText = "";
    }
    [Serializable] public sealed class MochiyaUploadFile
    {
        public string id, role, name, contentType; public long size;
    }
    [Serializable] public sealed class MochiyaUploadRequest
    {
        public int version = 2; public string idempotencyKey; public MochiyaItemInput item; public MochiyaUploadFile[] files;
        public string ToJson()
        {
            var json = JsonUtility.ToJson(this);
            return item.status == "private" ? json.Replace("\"priceCents\":" + item.priceCents, "\"priceCents\":null") : json;
        }
    }
    [Serializable] public sealed class MochiyaUploadTarget { public string id, url; public MochiyaUploadPair[] headers; }
    [Serializable] public sealed class MochiyaUploadSession { public string id, productId; public bool complete; public MochiyaUploadTarget[] targets; }
    [Serializable] internal sealed class MochiyaUploadError { public string code, message; }
    [Serializable] internal sealed class MochiyaUploadErrorResponse { public MochiyaUploadError error; }

    internal sealed class MochiyaUploadClient : IDisposable
    {
        internal const string ProductionOrigin = "http://localhost:3000";
        private readonly string origin, token;
        private UnityWebRequest active;
        public MochiyaUploadClient(string token, string origin = ProductionOrigin)
        {
            var uri = new Uri(origin);
            if (uri.Scheme != "https" && !(uri.IsLoopback && uri.Scheme == "http")) throw new ArgumentException("Use HTTPS for Mochiya.");
            this.origin = uri.GetLeftPart(UriPartial.Authority); this.token = token;
        }
        public async Task<T> Api<T>(string path, string method, string json, CancellationToken cancel)
        {
            using (var request = new UnityWebRequest(origin + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.redirectLimit = 0; request.timeout = 120;
                request.SetRequestHeader("Authorization", "Bearer " + token);
                if (json != null) { request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)); request.SetRequestHeader("Content-Type", "application/json"); }
                await Send(request, cancel);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    MochiyaUploadErrorResponse error = null;
                    try { error = JsonUtility.FromJson<MochiyaUploadErrorResponse>(request.downloadHandler.text); } catch { }
                    throw new InvalidOperationException(error?.error?.message ?? ("Mochiya request failed (" + request.responseCode + ")."));
                }
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
        }
        public async Task Upload(MochiyaUploadTarget target, string path, long expectedSize, Action<float> progress, CancellationToken cancel)
        {
            var uri = new Uri(target.url);
            if (uri.Scheme != "https" || !(uri.Host == "storage.googleapis.com" || uri.Host.EndsWith(".storage.googleapis.com", StringComparison.Ordinal))) throw new InvalidOperationException("Unexpected upload destination.");
            if (new FileInfo(path).Length != expectedSize) throw new InvalidOperationException("The selected file changed. Start a new upload.");
            using (var request = new UnityWebRequest(target.url, "PUT"))
            {
                request.uploadHandler = new UploadHandlerFile(path); request.downloadHandler = new DownloadHandlerBuffer();
                request.redirectLimit = 0; request.timeout = 1800;
                foreach (var header in target.headers) request.SetRequestHeader(header.name, header.value);
                await Send(request, cancel, progress);
                // A previous successful PUT may have lost its response. Finalization verifies the stored object.
                if (request.result != UnityWebRequest.Result.Success && request.responseCode != 412)
                    throw new IOException("File upload failed (" + request.responseCode + "). Retry to continue.");
            }
        }
        private async Task Send(UnityWebRequest request, CancellationToken cancel, Action<float> progress = null)
        {
            active = request;
            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone) { cancel.ThrowIfCancellationRequested(); progress?.Invoke(request.uploadProgress); await Task.Delay(50, cancel); }
                cancel.ThrowIfCancellationRequested();
            }
            finally { if (cancel.IsCancellationRequested) request.Abort(); active = null; }
        }
        public void Dispose() { active?.Abort(); active = null; }
    }
}
