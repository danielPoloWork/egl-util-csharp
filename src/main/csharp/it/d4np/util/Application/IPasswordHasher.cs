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
    /// Verifies <paramref name="password"/> against a hash previously produced by <see cref="Hash"/>,
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
}
