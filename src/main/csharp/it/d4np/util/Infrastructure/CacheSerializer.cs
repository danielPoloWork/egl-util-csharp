using System.Text.Json;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Shared compact UTF-8 <c>System.Text.Json</c> serialization for the distributed cache layer, used
/// by both <see cref="DistributedCacheExtensions"/> and <see cref="CacheService"/> so a value written
/// by one is readable by the other.
/// </summary>
internal static class CacheSerializer
{
    // Note: no DefaultIgnoreCondition. Null properties are written explicitly so a value round-trips
    // faithfully — omitting them let deserialization resurrect a type's non-null C# defaults, silently
    // corrupting a cached value (#58, ADR-0021). Compactness comes from Web defaults + no indentation.
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    internal static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    internal static T? Deserialize<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options);
}
