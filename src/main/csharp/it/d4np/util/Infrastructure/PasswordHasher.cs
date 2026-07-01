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
/// <b>Hash format.</b> <see cref="Hash"/> returns a self-describing string
/// <c>PBKDF2-SHA256-V1.{iterations}.{saltBase64}.{keyBase64}</c>. Because every parameter needed to
/// verify is embedded, the iteration count can be raised later without invalidating already-stored
/// hashes: <see cref="Verify"/> always reads the work factor and salt from the stored value, never
/// from the current configuration.
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
    /// stored hash during <see cref="Verify"/>) is defense-in-depth: a pathological value cannot pin a
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

        if (salt.Length == 0 || expectedKey.Length == 0)
        {
            return false;
        }

        // Re-derive with the work factor and salt read from the stored hash, never from current config,
        // so hashes written with a smaller (or larger) iteration count still verify.
        byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
