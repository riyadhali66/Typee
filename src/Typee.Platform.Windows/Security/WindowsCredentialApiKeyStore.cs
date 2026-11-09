using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Platform.Windows.Interop;

namespace Typee.Platform.Windows.Security;

/// <summary>
/// Persists LLM API keys in the per-user Windows Credential Manager (CRED_TYPE_GENERIC).
/// Never logs key material.
/// </summary>
public sealed class WindowsCredentialApiKeyStore : IApiKeyStore
{
    private const string TargetPrefix = "Typee/llm/";
    private readonly ILogger<WindowsCredentialApiKeyStore> _logger;

    public WindowsCredentialApiKeyStore(ILogger<WindowsCredentialApiKeyStore> logger)
    {
        _logger = logger;
    }

    public void Save(string profileName, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var target = TargetName(profileName);
        var secret = apiKey.Trim();
        var blob = Encoding.Unicode.GetBytes(secret);
        var targetPtr = Marshal.StringToCoTaskMemUni(target);
        var userPtr = Marshal.StringToCoTaskMemUni(Environment.UserName);
        var blobHandle = GCHandle.Alloc(blob, GCHandleType.Pinned);

        try
        {
            var credential = new NativeMethods.Credential
            {
                Type = NativeMethods.CredTypeGeneric,
                TargetName = targetPtr,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobHandle.AddrOfPinnedObject(),
                Persist = NativeMethods.CredPersistLocalMachine,
                UserName = userPtr,
                AttributeCount = 0,
                Attributes = nint.Zero,
                Comment = nint.Zero,
                TargetAlias = nint.Zero,
                Flags = 0
            };

            if (!NativeMethods.CredWriteW(ref credential, 0))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("CredWrite failed (win32={Error})", error);
                throw new InvalidOperationException($"Credential Manager write failed (win32={error}).");
            }

            _logger.LogInformation("API key saved to Credential Manager for profile {Profile}", profileName);
        }
        finally
        {
            if (blobHandle.IsAllocated)
            {
                blobHandle.Free();
            }

            if (targetPtr != nint.Zero)
            {
                Marshal.FreeCoTaskMem(targetPtr);
            }

            if (userPtr != nint.Zero)
            {
                Marshal.FreeCoTaskMem(userPtr);
            }

            Array.Clear(blob);
        }
    }

    public void Clear(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var target = TargetName(profileName);
        if (NativeMethods.CredDeleteW(target, NativeMethods.CredTypeGeneric, 0))
        {
            _logger.LogInformation("API key cleared from Credential Manager for profile {Profile}", profileName);
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error == NativeMethods.ErrorNotFound)
        {
            return;
        }

        _logger.LogWarning("CredDelete failed (win32={Error})", error);
        throw new InvalidOperationException($"Credential Manager delete failed (win32={error}).");
    }

    public bool TryRead(string profileName, out string? apiKey)
    {
        apiKey = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var target = TargetName(profileName);

        if (!NativeMethods.CredReadW(target, NativeMethods.CredTypeGeneric, 0, out var ptr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ErrorNotFound)
            {
                _logger.LogWarning("CredRead failed (win32={Error})", error);
            }

            return false;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeMethods.Credential>(ptr);
            if (credential.CredentialBlob == nint.Zero || credential.CredentialBlobSize == 0)
            {
                return false;
            }

            var byteCount = (int)credential.CredentialBlobSize;
            // Unicode blob from CredWrite — length is byte count.
            apiKey = Marshal.PtrToStringUni(credential.CredentialBlob, byteCount / 2);
            return !string.IsNullOrEmpty(apiKey);
        }
        finally
        {
            NativeMethods.CredFree(ptr);
        }
    }

    public string? GetMaskedLast4(string profileName) =>
        TryRead(profileName, out var key) ? ApiKeyMask.Last4(key) : null;

    internal static string TargetName(string profileName) =>
        TargetPrefix + profileName.Trim();
}
