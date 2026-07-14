using System.Globalization;
using System.Security.Cryptography;

using It.D4np.Util.Application;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// A PBKDF2 (RFC 2898) implementation of <see cref="IPasswordHasher"/> using HMAC-SHA256, a
/// per-password 128-bit random salt, and a configurable high iteration count (default and floor
/// <see cref="MinimumIterations"/>, the OWASP 2024 guidance for PBKDF2-HMAC-SHA256). Verification is
/// constant-time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hash format.</b> <see cref="Hash(string)"/> returns a self-describing string
/// <c>PBKDF2-SHA256-V1.{iterations}.{saltBase64}.{keyBase64}</c>. Because every parameter needed to
/// verify is embedded, the iteration count can be raised later without invalidating already-stored
/// hashes: <see cref="Verify(string, string)"/> always reads the work factor and salt from the stored value, never
/// from the current configuration. <see cref="Verify(string, string)"/> accepts only hashes carrying the V1 scheme's
/// fixed 16-byte salt and 32-byte key; a well-formed but off-spec hash returns <see langword="false"/>.
/// </para>
/// <para>
/// <b>Constant time.</b> The derived-key comparison uses
/// <see cref="CryptographicOperations.FixedTimeEquals(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte})"/>
/// so verification time does not depend on how many leading bytes match, closing a timing side channel.
/// A malformed or unrecognised hash returns <see langword="false"/> rather than throwing.
/// </para>
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>The OWASP 2024 minimum iteration count for PBKDF2-HMAC-SHA256.</summary>
    public const int MinimumIterations = 600_000;

    /// <summary>
    /// The maximum accepted iteration count — 100× <see cref="MinimumIterations"/>, far above any
    /// legitimate configuration. Bounding the work factor (both at construction and when read from a
    /// stored hash during <see cref="Verify(string, string)"/>) is defense-in-depth: a pathological value cannot pin a
    /// CPU on verification.
    /// </summary>
    public const int MaximumIterations = MinimumIterations * 100;

    private const string Scheme = "PBKDF2-SHA256-V1";
    private const int SaltSize = 16; // 128-bit random salt.
    private const int KeySize = 32;  // 256-bit derived key (the natural HMAC-SHA256 output size).
    private const char Separator = '.';

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private readonly int _iterations;

    /// <summary>Creates a hasher with the given PBKDF2 iteration count.</summary>
    /// <param name="iterations">
    /// The PBKDF2 work factor; must be between <see cref="MinimumIterations"/> and
    /// <see cref="MaximumIterations"/> inclusive. Defaults to <see cref="MinimumIterations"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iterations"/> is below <see cref="MinimumIterations"/> or above
    /// <see cref="MaximumIterations"/>.
    /// </exception>
    public PasswordHasher(int iterations = MinimumIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, MinimumIterations);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, MaximumIterations);
        _iterations = iterations;
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return Hash(password.AsSpan());
    }

    /// <inheritdoc />
    public string Hash(ReadOnlySpan<char> password)
    {
        // The ReadOnlySpan overload is the primitive: it derives directly from the span (no intermediate
        // string), so a caller holding the plaintext in a clearable buffer can bound its lifetime (#41).
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, KeySize);

        return string.Join(
            Separator,
            Scheme,
            _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    /// <inheritdoc />
    public bool Verify(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);
        return Verify(password.AsSpan(), hash);
    }

    /// <inheritdoc />
    public bool Verify(ReadOnlySpan<char> password, string hash)
    {
        // The ReadOnlySpan overload is the primitive (derives from the span directly), so a caller can
        // bound the plaintext's lifetime (#41).
        ArgumentNullException.ThrowIfNull(hash);

        string[] parts = hash.Split(Separator);
        if (parts.Length != 4 || !string.Equals(parts[0], Scheme, StringComparison.Ordinal))
        {
            return false;
        }

        // Accept a lower count than today's floor (older hashes still verify, then rehash on next use),
        // but reject a pathological upper value so a crafted hash cannot pin a CPU.
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) ||
            iterations <= 0 || iterations > MaximumIterations)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Enforce the V1 scheme's fixed parameters: only a hash produced under the documented 16-byte salt
        // and 32-byte key validates. A well-formed but off-spec hash (e.g. a 1-byte salt, an 8-byte key)
        // is rejected rather than fed to PBKDF2 (#40).
        if (salt.Length != SaltSize || expectedKey.Length != KeySize)
        {
            return false;
        }

        // Re-derive with the work factor and salt read from the stored hash, never from current config,
        // so hashes written with a smaller (or larger) iteration count still verify.
        byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    /// <inheritdoc />
    public bool NeedsRehash(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        string[] parts = hash.Split(Separator);

        // An unrecognised scheme (or malformed structure/work factor) means the hash is not one this hasher
        // now produces, and a stored count below the configured work factor is under-strength — both should
        // be re-hashed on next use. A well-formed V1 hash at or above the current count does not (#35).
        if (parts.Length != 4 || !string.Equals(parts[0], Scheme, StringComparison.Ordinal))
        {
            return true;
        }

        return !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations)
            || iterations < _iterations;
    }
}
