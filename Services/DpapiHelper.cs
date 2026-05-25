using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexEnvironmentManager.Services;

public static class DpapiHelper
{
    [StructLayout(LayoutKind.Sequential)]
    struct DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, string? szDataDescr, ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] plaintext, byte[]? entropy = null)
    {
        var dataIn = new DATA_BLOB { cbData = (uint)plaintext.Length, pbData = Marshal.AllocHGlobal(plaintext.Length) };
        Marshal.Copy(plaintext, 0, dataIn.pbData, plaintext.Length);
        var entropyBlob = new DATA_BLOB();
        if (entropy != null)
        {
            entropyBlob.cbData = (uint)entropy.Length;
            entropyBlob.pbData = Marshal.AllocHGlobal(entropy.Length);
            Marshal.Copy(entropy, 0, entropyBlob.pbData, entropy.Length);
        }
        var dataOut = new DATA_BLOB();
        try
        {
            if (!CryptProtectData(ref dataIn, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref dataOut))
                throw new Exception($"CryptProtectData failed: {Marshal.GetLastWin32Error()}");
            var cipher = new byte[dataOut.cbData];
            Marshal.Copy(dataOut.pbData, cipher, 0, (int)dataOut.cbData);
            return cipher;
        }
        finally
        {
            Marshal.FreeHGlobal(dataIn.pbData);
            if (entropy != null) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (dataOut.pbData != IntPtr.Zero) LocalFree(dataOut.pbData);
        }
    }

    public static byte[] Unprotect(byte[] ciphertext, byte[]? entropy = null)
    {
        var dataIn = new DATA_BLOB { cbData = (uint)ciphertext.Length, pbData = Marshal.AllocHGlobal(ciphertext.Length) };
        Marshal.Copy(ciphertext, 0, dataIn.pbData, ciphertext.Length);
        var entropyBlob = new DATA_BLOB();
        if (entropy != null)
        {
            entropyBlob.cbData = (uint)entropy.Length;
            entropyBlob.pbData = Marshal.AllocHGlobal(entropy.Length);
            Marshal.Copy(entropy, 0, entropyBlob.pbData, entropy.Length);
        }
        var dataOut = new DATA_BLOB();
        try
        {
            if (!CryptUnprotectData(ref dataIn, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref dataOut))
                throw new Exception($"CryptUnprotectData failed: {Marshal.GetLastWin32Error()}");
            var plain = new byte[dataOut.cbData];
            Marshal.Copy(dataOut.pbData, plain, 0, (int)dataOut.cbData);
            return plain;
        }
        finally
        {
            Marshal.FreeHGlobal(dataIn.pbData);
            if (entropy != null) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (dataOut.pbData != IntPtr.Zero) LocalFree(dataOut.pbData);
        }
    }

    public static string EncryptToBase64(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(Protect(bytes));
    }

    public static string DecryptFromBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(Unprotect(bytes));
    }
}
