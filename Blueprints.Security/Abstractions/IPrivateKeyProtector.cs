namespace Blueprints.Security.Abstractions;

public interface IPrivateKeyProtector
{
    string ProviderName { get; }

    byte[] Protect(ReadOnlySpan<byte> privateKeyBytes);

    byte[] Unprotect(ReadOnlySpan<byte> protectedPrivateKeyBytes);
}
