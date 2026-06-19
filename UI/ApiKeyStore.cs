using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace CivilClaudePlugin.UI
{
    // API anahtarini kullanici profilinde DPAPI (CurrentUser) ile sifreleyerek saklar/okur.
    // DPAPI'ye crypt32.dll uzerinden P/Invoke ile erisilir; ek bir NuGet/DLL bagimliligi yoktur
    // (tek-DLL deploy korunur). Eski duz-metin "apiKey" alani bulunursa sifreli forma tasinir.
    public static class ApiKeyStore
    {
        private static string ConfigPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CivilClaudePlugin");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }

        public static string Load()
        {
            try
            {
                var path = ConfigPath();
                if (!File.Exists(path)) return null;

                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                if (data == null) return null;

                if (data.TryGetValue("apiKeyEnc", out var enc) && !string.IsNullOrEmpty(enc))
                    return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(enc)));

                // Eski duz-metin formati: oku ve sifreli forma tasi.
                if (data.TryGetValue("apiKey", out var plain) && !string.IsNullOrEmpty(plain))
                {
                    Save(plain);
                    return plain;
                }
            }
            catch { }
            return null;
        }

        public static void Save(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey)) return;
                var enc = Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(apiKey)));
                var data = new Dictionary<string, string> { { "apiKeyEnc", enc } };
                File.WriteAllText(ConfigPath(), JsonConvert.SerializeObject(data));
            }
            catch { }
        }

        // ---- DPAPI (crypt32.dll) ----

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string szDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private static byte[] Protect(byte[] data) => Transform(data, encrypt: true);
        private static byte[] Unprotect(byte[] data) => Transform(data, encrypt: false);

        private static byte[] Transform(byte[] data, bool encrypt)
        {
            var inBlob = new DATA_BLOB();
            var outBlob = new DATA_BLOB();
            try
            {
                inBlob.cbData = data.Length;
                inBlob.pbData = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, inBlob.pbData, data.Length);

                bool ok = encrypt
                    ? CryptProtectData(ref inBlob, "CivilClaudePlugin", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob);
                if (!ok)
                    throw new Exception($"DPAPI hatasi (kod {Marshal.GetLastWin32Error()}).");

                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }
}
