using System.Text.Json;
using System.Text.Json.Serialization;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Shared compact UTF-8 <c>System.Text.Json</c> serialization for the distributed cache layer, used
/// by both <see cref="DistributedCacheExtensions"/> and <see cref="CacheService"/> so a value written
/// by one is readable by the other.
/// </summary>
internal static class CacheSerializer
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    internal static T? Deserialize<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options);
}
