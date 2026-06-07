using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooms.Persistence.Json;

/// <summary>Shared JSON serializer options for all Rooms persistence.</summary>
internal static class RoomsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
