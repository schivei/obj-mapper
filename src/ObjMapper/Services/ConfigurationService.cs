using System.Text.Json;
using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Service for managing omap configuration files.
/// Supports global (~/.omap/config.json) and local (.omap/config.json) configurations.
/// </summary>
public static class ConfigurationService
{
    private const string ConfigFolderName = ".omap";
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the global configuration directory path.
    /// </summary>
    public static string GlobalConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ConfigFolderName);

    /// <summary>
    /// Gets the global configuration file path.
    /// </summary>
    public static string GlobalConfigPath => Path.Combine(GlobalConfigDir, ConfigFileName);

    /// <summary>
    /// Loads the effective configuration by merging local and global configs.
    /// Priority: command-line > local > global > defaults
    /// </summary>
    public static OmapConfig LoadEffectiveConfig(string? startDirectory = null)
    {
        var config = OmapConfig.Default;
        
        // Try to load global config
        var globalConfig = LoadGlobalConfig();
        if (globalConfig != null)
        {
            MergeConfig(config, globalConfig);
        }

        // Try to load local config (search recursively up)
        var localConfig = LoadLocalConfig(startDirectory ?? Directory.GetCurrentDirectory());
        if (localConfig != null)
        {
            MergeConfig(config, localConfig);
        }

        return config;
    }

    /// <summary>
    /// Loads the global configuration from ~/.omap/config.json.
    /// </summary>
    public static OmapConfig? LoadGlobalConfig()
    {
        if (!File.Exists(GlobalConfigPath))
            return null;

        try
        {
            var json = File.ReadAllText(GlobalConfigPath);
            return JsonSerializer.Deserialize<OmapConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads local configuration by searching recursively up the directory tree.
    /// </summary>
    public static OmapConfig? LoadLocalConfig(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory != null)
        {
            var configDir = Path.Combine(directory.FullName, ConfigFolderName);
            var configFile = Path.Combine(configDir, ConfigFileName);

            if (File.Exists(configFile))
            {
                try
                {
                    var json = File.ReadAllText(configFile);
                    return JsonSerializer.Deserialize<OmapConfig>(json, JsonOptions);
                }
                catch
                {
                    // Continue searching up
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Saves the global configuration to ~/.omap/config.json.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static void SaveGlobalConfig(OmapConfig config)
    {
        Directory.CreateDirectory(GlobalConfigDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(GlobalConfigPath, json);
    }

    /// <summary>
    /// Ensures the global configuration file exists with default values.
    /// </summary>
    public static void EnsureGlobalConfigExists()
    {
        if (!File.Exists(GlobalConfigPath))
        {
            SaveGlobalConfig(OmapConfig.Default);
        }
    }

    /// <summary>
    /// Merges source config into target config (non-null values from source override target).
    /// </summary>
    private static void MergeConfig(OmapConfig target, OmapConfig source)
    {
        if (!string.IsNullOrEmpty(source.Locale))
            target.Locale = source.Locale;
        
        if (source.NoPluralizer)
            target.NoPluralizer = source.NoPluralizer;

        if (!string.IsNullOrEmpty(source.Namespace))
            target.Namespace = source.Namespace;

        if (!string.IsNullOrEmpty(source.Database))
            target.Database = source.Database;

        if (!string.IsNullOrEmpty(source.Type))
            target.Type = source.Type;

        if (!string.IsNullOrEmpty(source.EntityMode))
            target.EntityMode = source.EntityMode;

        if (!string.IsNullOrEmpty(source.Context))
            target.Context = source.Context;
    }
}
