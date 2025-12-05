using System.Text.Json.Serialization;

namespace ObjMapper.Models;

/// <summary>
/// Configuration model for omap tool settings.
/// Can be stored in ~/.omap/config.json (global) or .omap/config.json (local).
/// </summary>
public class OmapConfig
{
    /// <summary>
    /// The locale for pluralization (e.g., en-us, pt-br, es-es).
    /// Default is "en-us".
    /// </summary>
    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en-us";

    /// <summary>
    /// Whether to disable pluralization entirely.
    /// </summary>
    [JsonPropertyName("noPluralizer")]
    public bool NoPluralizer { get; set; } = false;

    /// <summary>
    /// Default namespace for generated classes.
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Default database type.
    /// </summary>
    [JsonPropertyName("database")]
    public string? Database { get; set; }

    /// <summary>
    /// Default mapping type (efcore/dapper).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Default entity mode (class/record/struct/record_struct).
    /// </summary>
    [JsonPropertyName("entityMode")]
    public string? EntityMode { get; set; }

    /// <summary>
    /// Default context name.
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static OmapConfig Default => new()
    {
        Locale = "en-us",
        NoPluralizer = false
    };
}
