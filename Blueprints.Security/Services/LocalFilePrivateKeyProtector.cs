using System.Security.Cryptography;
using Blueprints.Security.Abstractions;

namespace Blueprints.Security.Services;

public sealed class LocalFilePrivateKeyProtector : IPrivateKeyProtector
{
    private const byte PayloadVersion = 1;
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private readonly string _keyPath;

    public LocalFilePrivateKeyProtector(string keyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _keyPath = keyPath;
    }

    public string ProviderName => "Local AES-GCM";

    public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes)
    {
        if (privateKeyBytes.IsEmpty)
        {
            throw new ArgumentException("Private key payload must not be empty.", nameof(privateKeyBytes));
        }

        var key = LoadOrCreateKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherText = new byte[privateKeyBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, privateKeyBytes, cipherText, tag);

        var payload = new byte[1 + NonceSizeBytes + TagSizeBytes + cipherText.Length];
        payload[0] = PayloadVersion;
        nonce.CopyTo(payload.AsSpan(1, NonceSizeBytes));
        tag.CopyTo(payload.AsSpan(1 + NonceSizeBytes, TagSizeBytes));
        cipherText.CopyTo(payload.AsSpan(1 + NonceSizeBytes + TagSizeBytes));

        return payload;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPrivateKeyBytes)
    {
        if (protectedPrivateKeyBytes.Length <= 1 + NonceSizeBytes + TagSizeBytes)
        {
            throw new ArgumentException("Protected private key payload is invalid.", nameof(protectedPrivateKeyBytes));
        }

        if (protectedPrivateKeyBytes[0] != PayloadVersion)
        {
            throw new InvalidOperationException("Protected private key payload version is unsupported.");
        }

        var key = LoadOrCreateKey();
        var nonce = protectedPrivateKeyBytes.Slice(1, NonceSizeBytes);
        var tag = protectedPrivateKeyBytes.Slice(1 + NonceSizeBytes, TagSizeBytes);
        var cipherText = protectedPrivateKeyBytes[(1 + NonceSizeBytes + TagSizeBytes)..];
        var privateKeyBytes = new byte[cipherText.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, cipherText, tag, privateKeyBytes);

        return privateKeyBytes;
    }

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            var existingKey = File.ReadAllBytes(_keyPath);
            if (existingKey.Length != KeySizeBytes)
            {
                throw new InvalidOperationException("Local private key protection key has an invalid length.");
            }

            return existingKey;
        }

        var directory = Path.GetDirectoryName(_keyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            RestrictDirectoryToCurrentUser(directory);
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        File.WriteAllBytes(_keyPath, key);
        RestrictFileToCurrentUser(_keyPath);
        return key;
    }

    private static void RestrictDirectoryToCurrentUser(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
            }
        }
    }

    private static void RestrictFileToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
            }
        }
    }
}
