using System.Globalization;
using System.Security.Cryptography;

using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class PasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    // A minimal hasher implementing ONLY the string members, to prove the interface's new default members
    // (the span overloads and NeedsRehash) are inherited without an override — i.e. source/binary
    // compatible for existing implementers (#35, #41).
    private sealed class StringOnlyHasher : IPasswordHasher
    {
        public string Hash(string password) => "S:" + password;

        public bool Verify(string password, string hash) => string.Equals(hash, "S:" + password, StringComparison.Ordinal);
    }

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

    [Fact]
    public void Hash_and_Verify_round_trip_through_the_span_overloads()
    {
        // #41: derive from a ReadOnlySpan<char> without materializing a string.
        var hasher = new PasswordHasher();

        string hash = hasher.Hash(Password.AsSpan());

        Assert.True(hasher.Verify(Password.AsSpan(), hash));
        Assert.False(hasher.Verify("not the password".AsSpan(), hash));
    }

    [Fact]
    public void The_string_and_span_overloads_are_interchangeable()
    {
        // #41: a hash produced through one overload verifies through the other — same underlying derivation.
        var hasher = new PasswordHasher();

        string viaString = hasher.Hash(Password);
        string viaSpan = hasher.Hash(Password.AsSpan());

        Assert.True(hasher.Verify(Password.AsSpan(), viaString));
        Assert.True(hasher.Verify(Password, viaSpan));
    }

    [Fact]
    public void NeedsRehash_is_false_for_a_hash_at_the_current_work_factor()
    {
        var hasher = new PasswordHasher();
        string current = ComposeHash(PasswordHasher.MinimumIterations, new byte[16], new byte[32]);

        Assert.False(hasher.NeedsRehash(current));
    }

    [Fact]
    public void NeedsRehash_is_true_for_a_hash_below_the_current_work_factor()
    {
        // #35: a hash stored at a lower iteration count than the hasher's current configuration is
        // under-strength and should be re-hashed.
        var stronger = new PasswordHasher(PasswordHasher.MinimumIterations + 1);
        string stored = ComposeHash(PasswordHasher.MinimumIterations, new byte[16], new byte[32]);

        Assert.True(stronger.NeedsRehash(stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("UNKNOWN-SCHEME.600000.QUJD.QUJD")]
    [InlineData("PBKDF2-SHA256-V1.notanumber.QUJD.QUJD")]
    [InlineData("PBKDF2-SHA256-V1.600000.QUJD")] // too few segments
    public void NeedsRehash_is_true_for_an_unknown_or_malformed_hash(string hash)
    {
        Assert.True(new PasswordHasher().NeedsRehash(hash));
    }

    [Fact]
    public void Verify_then_conditionally_rehash_upgrades_an_under_strength_hash()
    {
        // #35: the documented upgrade path — an old hash still verifies, NeedsRehash flags it, and
        // re-hashing yields a current-strength hash that no longer needs rehashing.
        string old = new PasswordHasher(PasswordHasher.MinimumIterations).Hash(Password);
        var current = new PasswordHasher(PasswordHasher.MinimumIterations + 1);

        Assert.True(current.Verify(Password, old));
        Assert.True(current.NeedsRehash(old));

        string upgraded = current.Hash(Password);

        Assert.False(current.NeedsRehash(upgraded));
        Assert.True(current.Verify(Password, upgraded));
    }

    [Fact]
    public void NeedsRehash_and_span_Verify_reject_a_null_hash()
    {
        var hasher = new PasswordHasher();

        Assert.Throws<ArgumentNullException>(() => hasher.NeedsRehash(null!));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify(Password.AsSpan(), null!));
    }

    [Fact]
    public void Default_interface_members_delegate_to_string_and_default_to_no_rehash()
    {
        // #35, #41: a hasher implementing only the string members inherits the span overloads (delegating
        // to the string path) and NeedsRehash (returning false) — the additions don't break implementers.
        IPasswordHasher hasher = new StringOnlyHasher();

        string hash = hasher.Hash("pw".AsSpan());

        Assert.Equal("S:pw", hash);
        Assert.True(hasher.Verify("pw".AsSpan(), hash));
        Assert.False(hasher.Verify("no".AsSpan(), hash));
        Assert.False(hasher.NeedsRehash(hash));
    }
}
