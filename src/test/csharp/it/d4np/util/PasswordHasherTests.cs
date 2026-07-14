using System.Globalization;
using System.Security.Cryptography;

using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class PasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    private static string ComposeHash(int iterations, byte[] salt, byte[] key) =>
        string.Join(
            '.',
            "PBKDF2-SHA256-V1",
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));

    [Fact]
    public void Hash_then_Verify_round_trips_for_the_same_password()
    {
        var hasher = new PasswordHasher();

        string hash = hasher.Hash(Password);

        Assert.True(hasher.Verify(Password, hash));
    }

    [Fact]
    public void Verify_rejects_a_wrong_password()
    {
        var hasher = new PasswordHasher();

        string hash = hasher.Hash(Password);

        Assert.False(hasher.Verify("not the password", hash));
    }

    [Fact]
    public void Hash_uses_a_fresh_random_salt_on_every_call()
    {
        var hasher = new PasswordHasher();

        string first = hasher.Hash(Password);
        string second = hasher.Hash(Password);

        Assert.NotEqual(first, second); // a random salt makes the output differ for identical input
        Assert.True(hasher.Verify(Password, first));
        Assert.True(hasher.Verify(Password, second));
    }

    [Fact]
    public void Hash_embeds_the_scheme_and_an_iteration_count_at_the_OWASP_minimum()
    {
        var hasher = new PasswordHasher();

        string[] parts = hasher.Hash(Password).Split('.');
        int iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);

        Assert.Equal("PBKDF2-SHA256-V1", parts[0]);
        Assert.True(iterations >= PasswordHasher.MinimumIterations);
        Assert.Equal(PasswordHasher.MinimumIterations, iterations);
    }

    [Fact]
    public void Constructor_rejects_an_iteration_count_below_the_minimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PasswordHasher(PasswordHasher.MinimumIterations - 1));
    }

    [Fact]
    public void Constructor_rejects_an_iteration_count_above_the_maximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PasswordHasher(PasswordHasher.MaximumIterations + 1));
    }

    [Fact]
    public void Verify_returns_false_when_the_stored_iteration_count_exceeds_the_maximum()
    {
        // Defense-in-depth: a pathological work factor in a stored hash is rejected before any
        // derivation runs, so a crafted hash cannot pin a CPU.
        int pathological = PasswordHasher.MaximumIterations + 1;
        string stored = ComposeHash(pathological, new byte[16], new byte[32]);

        var hasher = new PasswordHasher();

        Assert.False(hasher.Verify(Password, stored));
    }

    [Fact]
    public void Verify_accepts_an_independently_computed_PBKDF2_hash()
    {
        // Known-answer / cross-check: build a stored hash the way a foreign system would, using the BCL
        // primitive directly, then assert our format parser and derivation agree with it.
        const int iterations = PasswordHasher.MinimumIterations;
        byte[] salt = Convert.FromBase64String("AAECAwQFBgcICQoLDA0ODw=="); // 16 fixed bytes
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Password, salt, iterations, HashAlgorithmName.SHA256, 32);
        string stored = ComposeHash(iterations, salt, key);

        var hasher = new PasswordHasher();

        Assert.True(hasher.Verify(Password, stored));
        Assert.False(hasher.Verify("not the password", stored));
    }

    [Fact]
    public void Verify_honours_the_iteration_count_stored_in_the_hash_not_the_current_configuration()
    {
        // A hasher configured with MORE iterations must still verify a hash stored with fewer
        // (forward-compatible upgrades): the work factor is read from the stored value.
        const int storedIterations = PasswordHasher.MinimumIterations;
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Password, salt, storedIterations, HashAlgorithmName.SHA256, 32);
        string stored = ComposeHash(storedIterations, salt, key);

        var hasher = new PasswordHasher(PasswordHasher.MinimumIterations + 1);

        Assert.True(hasher.Verify(Password, stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256-V1.notanumber.AAAA.AAAA")]
    [InlineData("UNKNOWN-SCHEME.600000.AAAA.AAAA")]
    [InlineData("PBKDF2-SHA256-V1.600000.!!!notbase64!!!.AAAA")]
    [InlineData("PBKDF2-SHA256-V1.600000..QUJD")] // empty salt segment
    [InlineData("PBKDF2-SHA256-V1.2147483647.QUJD.QUJD")] // int.MaxValue iterations (pathological)
    [InlineData("PBKDF2-SHA256-V1.600000.AAAA")] // too few segments
    public void Verify_returns_false_for_a_malformed_or_unknown_hash(string malformed)
    {
        var hasher = new PasswordHasher();

        Assert.False(hasher.Verify(Password, malformed));
    }

    [Fact]
    public void Verify_rejects_an_off_spec_salt_or_key_length_even_when_the_derived_key_would_match()
    {
        // #40: enforce the V1 scheme's fixed 16-byte salt / 32-byte key. Each hash below carries a
        // CORRECTLY derived key for its (off-spec) parameters, so it would validate under the pre-fix
        // guards (salt/key merely non-empty); the length enforcement now rejects it. A wrong-length *zero*
        // key would fail regardless, so it would not distinguish the fix.
        var hasher = new PasswordHasher();
        const int iterations = PasswordHasher.MinimumIterations;

        byte[] shortSalt = RandomNumberGenerator.GetBytes(8); // off-spec salt (8 != 16)
        byte[] keyForShortSalt = Rfc2898DeriveBytes.Pbkdf2(Password, shortSalt, iterations, HashAlgorithmName.SHA256, 32);
        Assert.False(hasher.Verify(Password, ComposeHash(iterations, shortSalt, keyForShortSalt)));

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] shortKey = Rfc2898DeriveBytes.Pbkdf2(Password, salt, iterations, HashAlgorithmName.SHA256, 8); // off-spec key (8 != 32)
        Assert.False(hasher.Verify(Password, ComposeHash(iterations, salt, shortKey)));
    }

    [Fact]
    public void Verify_returns_false_when_the_stored_salt_is_tampered()
    {
        var hasher = new PasswordHasher();
        string[] parts = hasher.Hash(Password).Split('.');

        // Flip the first base64 character of the salt: same length, still valid base64, different bytes.
        char head = parts[2][0];
        parts[2] = (head == 'A' ? 'B' : 'A') + parts[2][1..];
        string tampered = string.Join('.', parts);

        Assert.False(hasher.Verify(Password, tampered));
    }

    [Fact]
    public void Hash_rejects_a_null_password()
    {
        var hasher = new PasswordHasher();

        Assert.Throws<ArgumentNullException>(() => hasher.Hash(null!));
    }

    [Fact]
    public void Verify_rejects_null_arguments()
    {
        var hasher = new PasswordHasher();
        string hash = hasher.Hash(Password);

        Assert.Throws<ArgumentNullException>(() => hasher.Verify(null!, hash));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify(Password, null!));
    }
}
