namespace It.D4np.Util.Application;

/// <summary>
/// An algorithm-agnostic abstraction for hashing and verifying secrets such as user passwords.
/// An implementation encapsulates one interchangeable hashing strategy (for example PBKDF2) behind
/// this contract, so the algorithm and its work factor can evolve without touching callers.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes <paramref name="password"/> and returns a self-describing string that embeds every
    /// parameter needed to verify it later (algorithm, work factor, and a freshly generated random
    /// salt). Two calls with the same input return different strings because the salt is random.
    /// </summary>
    /// <param name="password">The plaintext secret to hash; must not be <see langword="null"/>.</param>
    /// <returns>An opaque, self-describing hash string suitable for persistence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="password"/> is <see langword="null"/>.</exception>
    string Hash(string password);

    /// <summary>
    /// Hashes the secret held in <paramref name="password"/>, letting a caller that controls a clearable
    /// buffer (e.g. a <c>char[]</c>) bound the plaintext's lifetime rather than pin it in an immutable
    /// <see cref="string"/> until garbage collection. The default implementation forwards to
    /// <see cref="Hash(string)"/> (materializing a string); an implementation that can derive directly
    /// from the span should override this to avoid the copy.
    /// </summary>
    /// <param name="password">The plaintext secret to hash.</param>
    /// <returns>An opaque, self-describing hash string suitable for persistence.</returns>
    string Hash(ReadOnlySpan<char> password) => Hash(password.ToString());

    /// <summary>
    /// Verifies <paramref name="password"/> against a hash previously produced by <see cref="Hash(string)"/>,
    /// in constant time with respect to the stored hash so verification does not leak information
    /// through timing.
    /// </summary>
    /// <param name="password">The plaintext secret to check; must not be <see langword="null"/>.</param>
    /// <param name="hash">The stored hash to verify against; must not be <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="password"/> matches <paramref name="hash"/>; otherwise
    /// <see langword="false"/>, including when <paramref name="hash"/> is malformed or uses an
    /// unrecognised format.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="password"/> or <paramref name="hash"/> is <see langword="null"/>.
    /// </exception>
    bool Verify(string password, string hash);

    /// <summary>
    /// Verifies the secret held in <paramref name="password"/> against <paramref name="hash"/>, letting a
    /// caller that controls a clearable buffer bound the plaintext's lifetime. The default implementation
    /// forwards to <see cref="Verify(string, string)"/> (materializing a string); an implementation that
    /// can derive directly from the span should override this to avoid the copy.
    /// </summary>
    /// <param name="password">The plaintext secret to check.</param>
    /// <param name="hash">The stored hash to verify against; must not be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the secret matches; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hash"/> is <see langword="null"/>.</exception>
    bool Verify(ReadOnlySpan<char> password, string hash) => Verify(password.ToString(), hash);

    /// <summary>
    /// Whether <paramref name="hash"/> should be re-hashed because it was produced under weaker parameters
    /// than this hasher's current configuration (an older scheme or a lower work factor) or is not a hash
    /// this hasher recognises. Call it after a successful <see cref="Verify(string, string)"/> and, when it
    /// returns <see langword="true"/>, re-hash the just-verified plaintext and persist the new hash — the
    /// verify-then-conditionally-rehash upgrade path. The default implementation returns
    /// <see langword="false"/> (never rehash); a work-factor-aware implementation should override it.
    /// </summary>
    /// <param name="hash">The stored hash to assess; must not be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="hash"/> should be upgraded; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hash"/> is <see langword="null"/>.</exception>
    bool NeedsRehash(string hash) => false;
}
