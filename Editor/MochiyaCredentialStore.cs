using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>Credentials belong to the current OS user and project path, never the project files.</summary>
    internal sealed class MochiyaCredentialStore : IMochiyaCredentialStore
    {
        private readonly string target;
        internal MochiyaCredentialStore(string projectPath) { target = ProjectKey(projectPath); }

        internal static string ProjectKey(string projectPath)
        {
            var path = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
#if UNITY_EDITOR_WIN
            path = path.ToUpperInvariant();
#endif
            using (var hash = SHA256.Create())
                return "Mochiya.AvatarTools/" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static Exception Unavailable() => new InvalidOperationException("Cannot access the OS credential store. Unlock it and try connecting again.");

#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint flags, type;
            public string targetName, comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
            public uint blobSize;
            public IntPtr blob;
            public uint persist, attributeCount;
            public IntPtr attributes;
            public string targetAlias, userName;
        }
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadCredential(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WriteCredential(ref Credential credential, uint flags);
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteCredential(string target, uint type, uint flags);
        [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr credential);

        public string Read()
        {
            if (!ReadCredential(target, 1, 0, out var pointer))
            {
                if (Marshal.GetLastWin32Error() == 1168) return null;
                throw Unavailable();
            }
            try
            {
                var credential = Marshal.PtrToStructure<Credential>(pointer);
                var bytes = new byte[credential.blobSize];
                try { Marshal.Copy(credential.blob, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            finally { CredFree(pointer); }
        }
        public void Write(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                var credential = new Credential { type = 1, targetName = target, userName = "Mochiya", blob = pointer, blobSize = (uint)bytes.Length, persist = 2 };
                if (!WriteCredential(ref credential, 0)) throw Unavailable();
            }
            finally { Array.Clear(bytes, 0, bytes.Length); Marshal.Copy(bytes, 0, pointer, bytes.Length); Marshal.FreeHGlobal(pointer); }
        }
        public void Delete()
        {
            if (!DeleteCredential(target, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw Unavailable();
        }
#elif UNITY_EDITOR_OSX
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string Core = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [DllImport(Security)] private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        [DllImport(Security)] private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        [DllImport(Security)] private static extern int SecItemUpdate(IntPtr query, IntPtr attributes);
        [DllImport(Security)] private static extern int SecItemDelete(IntPtr query);
        [DllImport(Core)] private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, IntPtr capacity, IntPtr keys, IntPtr values);
        [DllImport(Core)] private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);
        [DllImport(Core)] private static extern IntPtr CFStringCreateWithBytes(IntPtr allocator, byte[] bytes, IntPtr length, uint encoding, [MarshalAs(UnmanagedType.I1)] bool external);
        [DllImport(Core)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, IntPtr length);
        [DllImport(Core)] private static extern IntPtr CFDataGetLength(IntPtr data);
        [DllImport(Core)] private static extern IntPtr CFDataGetBytePtr(IntPtr data);
        [DllImport(Core)] private static extern void CFRelease(IntPtr value);
        [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlopen(string path, int mode);
        [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlsym(IntPtr handle, string name);
        private static readonly IntPtr SecurityHandle = dlopen(Security, 1), CoreHandle = dlopen(Core, 1);
        private static IntPtr Constant(string name)
        {
            var symbol = dlsym(name.StartsWith("kCF", StringComparison.Ordinal) ? CoreHandle : SecurityHandle, name);
            if (symbol == IntPtr.Zero) throw Unavailable();
            return Marshal.ReadIntPtr(symbol);
        }

        private sealed class Query : IDisposable
        {
            internal readonly IntPtr Value = CFDictionaryCreateMutable(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            private readonly System.Collections.Generic.List<IntPtr> owned = new System.Collections.Generic.List<IntPtr>();
            internal void Set(string key, string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var text = CFStringCreateWithBytes(IntPtr.Zero, bytes, (IntPtr)bytes.Length, 0x08000100, false);
                owned.Add(text); CFDictionarySetValue(Value, Constant(key), text);
            }
            internal void SetConstant(string key, string value) => CFDictionarySetValue(Value, Constant(key), Constant(value));
            internal void SetData(string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                try
                {
                    var data = CFDataCreate(IntPtr.Zero, bytes, (IntPtr)bytes.Length);
                    owned.Add(data); CFDictionarySetValue(Value, Constant("kSecValueData"), data);
                }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            public void Dispose() { CFRelease(Value); foreach (var value in owned) CFRelease(value); }
        }
        private Query Match()
        {
            var query = new Query();
            query.SetConstant("kSecClass", "kSecClassGenericPassword");
            query.Set("kSecAttrService", target); query.Set("kSecAttrAccount", "api-key");
            return query;
        }
        public string Read()
        {
            using (var query = Match())
            {
                query.SetConstant("kSecReturnData", "kCFBooleanTrue");
                var status = SecItemCopyMatching(query.Value, out var data);
                if (status == -25300) return null;
                if (status != 0) throw Unavailable();
                try
                {
                    var bytes = new byte[CFDataGetLength(data).ToInt32()];
                    try { Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
                    finally { Array.Clear(bytes, 0, bytes.Length); }
                }
                finally { CFRelease(data); }
            }
        }
        public void Write(string value)
        {
            using (var query = Match())
            using (var update = new Query())
            {
                update.SetData(value);
                var status = SecItemUpdate(query.Value, update.Value);
                if (status == -25300) { query.SetData(value); status = SecItemAdd(query.Value, IntPtr.Zero); }
                if (status != 0) throw Unavailable();
            }
        }
        public void Delete()
        {
            using (var query = Match())
            {
                var status = SecItemDelete(query.Value);
                if (status != 0 && status != -25300) throw Unavailable();
            }
        }
#elif UNITY_EDITOR_LINUX
        // Secret values travel through stdin/stdout, never command arguments or a shell.
        private string Run(string operation, string value = null)
        {
            var arguments = operation + (operation == "store" ? " --label=Mochiya-Avatar-Tools" : "") + " application mochiya-avatar-tools project " + target.Substring(target.IndexOf('/') + 1);
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = new System.Diagnostics.ProcessStartInfo("secret-tool", arguments)
                    {
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    process.Start();
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    if (value != null) process.StandardInput.Write(value);
                    process.StandardInput.Close();
                    if (!process.WaitForExit(30000)) { process.Kill(); throw Unavailable(); }
                    var result = output.GetAwaiter().GetResult();
                    var error = errors.GetAwaiter().GetResult();
                    if (process.ExitCode == 0) return result.TrimEnd('\r', '\n');
                    if (operation != "store" && process.ExitCode == 1 && string.IsNullOrEmpty(error)) return null;
                    throw Unavailable();
                }
            }
            catch { throw Unavailable(); }
        }
        public string Read() => Run("lookup");
        public void Write(string value) => Run("store", value);
        public void Delete() => Run("clear");
#else
        public string Read() => throw Unavailable();
        public void Write(string value) => throw Unavailable();
        public void Delete() => throw Unavailable();
#endif
    }
}
