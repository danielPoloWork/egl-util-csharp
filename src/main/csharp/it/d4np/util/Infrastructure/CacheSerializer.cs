using System.Text.Json;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Shared compact UTF-8 <c>System.Text.Json</c> serialization for the distributed cache layer, used
/// by both <see cref="DistributedCacheExtensions"/> and <see cref="CacheService"/> so a value written
/// by one is readable by the other.
/// </summary>
/// <remarks>
/// Values are serialised by their <b>declared</b> type <c>T</c> — there is no <c>$type</c> discriminator
/// — so <c>T</c> is part of the cache contract: it must be a concrete, fully round-trippable type, and a
/// stored value's runtime type must equal <c>T</c>. A polymorphic declared type (<see cref="object"/>, an
/// interface, or an abstract class) or a runtime subtype would serialise only <c>T</c>'s members and lose
/// the rest, so those are rejected up front rather than silently corrupting the entry (#33, ADR-0023).
/// </remarks>
internal static class CacheSerializer
{
    // Note: no DefaultIgnoreCondition. Null properties are written explicitly so a value round-trips
    // faithfully — omitting them let deserialization resurrect a type's non-null C# defaults, silently
    // corrupting a cached value (#58, ADR-0021). Compactness comes from Web defaults + no indentation.
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    internal static byte[] Serialize<T>(T value)
    {
        EnsureDeclaredTypeIsRoundTrippable<T>();

        // Guard the runtime type too: the value is serialised as T, so a subtype's extra members would be
        // silently dropped (e.g. caching a Dog under GetOrCreateAsync<Animal>). Callers reject null before
        // serialising, but the check is null-safe regardless. Nullable<T> boxes to its underlying type, so
        // compare against the underlying type to avoid a false positive for e.g. int? holding an int (#33).
        if (value is not null)
        {
            Type declared = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (value.GetType() != declared)
            {
                throw new NotSupportedException(
                    $"The cache serialises the declared type '{typeof(T)}', so a value of runtime type " +
                    $"'{value.GetType()}' would be stored with only '{typeof(T)}'s members and the subtype's " +
                    "data silently lost. Cache the value under its concrete type (#33).");
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    internal static T? Deserialize<T>(byte[] bytes)
    {
        EnsureDeclaredTypeIsRoundTrippable<T>();
        return JsonSerializer.Deserialize<T>(bytes, Options);
    }

    /// <summary>
    /// Rejects a declared cache type <typeparamref name="T"/> that cannot round-trip through declared-type
    /// serialization — <see cref="object"/>, an interface, or an abstract class — because such a value's
    /// data lives in a concrete subtype that would be silently lost. Fails fast with a
    /// <see cref="NotSupportedException"/> instead (#33, ADR-0023).
    /// </summary>
    internal static void EnsureDeclaredTypeIsRoundTrippable<T>()
    {
        Type declared = typeof(T);
        if (declared == typeof(object) || declared.IsInterface || declared.IsAbstract)
        {
            throw new NotSupportedException(
                $"The cache cannot round-trip the declared type '{declared}': it serialises the declared " +
                "type (no $type discriminator), so an object, interface, or abstract type would silently " +
                "lose the concrete value's members. Use a concrete, fully round-trippable type as T (#33).");
        }
    }
}
