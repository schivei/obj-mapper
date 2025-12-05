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
    /// Gets the local configuration directory path by searching for existing config,
    /// solution file (.sln/.slnx), project file (.csproj), or falling back to current directory.
    /// </summary>
    public static string GetLocalConfigDir(string? startDirectory = null)
    {
        var searchDir = startDirectory ?? Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(searchDir);

        // First, search for existing .omap folder
        var existingConfigDir = FindExistingConfigDir(directory);
        if (existingConfigDir != null)
            return existingConfigDir;

        // Search for solution file (.sln or .slnx)
        var solutionDir = FindDirectoryWithFile(directory, "*.sln", "*.slnx");
        if (solutionDir != null)
            return Path.Combine(solutionDir, ConfigFolderName);

        // Search for project file (.csproj)
        var projectDir = FindDirectoryWithFile(directory, "*.csproj");
        if (projectDir != null)
            return Path.Combine(projectDir, ConfigFolderName);

        // Fall back to current directory
        return Path.Combine(searchDir, ConfigFolderName);
    }

    /// <summary>
    /// Gets the local configuration file path.
    /// </summary>
    public static string GetLocalConfigPath(string? startDirectory = null)
    {
        return Path.Combine(GetLocalConfigDir(startDirectory), ConfigFileName);
    }

    /// <summary>
    /// Finds an existing .omap config directory by searching up the directory tree.
    /// </summary>
    private static string? FindExistingConfigDir(DirectoryInfo? directory)
    {
        while (directory != null)
        {
            var configDir = Path.Combine(directory.FullName, ConfigFolderName);
            if (Directory.Exists(configDir))
                return configDir;
            directory = directory.Parent;
        }
        return null;
    }

    /// <summary>
    /// Finds a directory containing files matching any of the given patterns.
    /// </summary>
    private static string? FindDirectoryWithFile(DirectoryInfo? directory, params string[] patterns)
    {
        while (directory != null)
        {
            foreach (var pattern in patterns)
            {
                try
                {
                    if (directory.GetFiles(pattern).Length > 0)
                        return directory.FullName;
                }
                catch
                {
                    // Ignore access errors
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
    public static bool SaveGlobalConfig(OmapConfig config)
    {
        try
        {
            Directory.CreateDirectory(GlobalConfigDir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(GlobalConfigPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves the local configuration.
    /// </summary>
    public static bool SaveLocalConfig(OmapConfig config, string? startDirectory = null)
    {
        try
        {
            var configDir = GetLocalConfigDir(startDirectory);
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, ConfigFileName);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves configuration, trying global first, then local if global fails.
    /// </summary>
    public static (bool success, string path, bool isLocal) SaveConfig(OmapConfig config, bool forceLocal = false)
    {
        if (forceLocal)
        {
            var localPath = GetLocalConfigPath();
            if (SaveLocalConfig(config))
                return (true, localPath, true);
        }
        else
        {
            // Try global first
            if (SaveGlobalConfig(config))
                return (true, GlobalConfigPath, false);

            // Fall back to local
            var localPath = GetLocalConfigPath();
            if (SaveLocalConfig(config))
                return (true, localPath, true);
        }

        return (false, string.Empty, false);
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
    /// Updates a specific configuration value.
    /// </summary>
    public static (bool success, string path) SetConfigValue(
        string key, 
        string? value, 
        bool forceLocal = false)
    {
        // Load existing config
        OmapConfig config;
        string configPath;
        bool isLocal;

        if (forceLocal)
        {
            configPath = GetLocalConfigPath();
            config = LoadLocalConfig(Directory.GetCurrentDirectory()) ?? new OmapConfig();
            isLocal = true;
        }
        else
        {
            configPath = GlobalConfigPath;
            config = LoadGlobalConfig() ?? new OmapConfig();
            isLocal = false;
        }

        // Update the specific value
        switch (key.ToLowerInvariant())
        {
            case "locale":
            case "l":
                config.Locale = value ?? "en-us";
                break;
            case "no-pluralize":
            case "nopluralizer":
                config.NoPluralizer = value?.ToLowerInvariant() == "true" || value == "1";
                break;
            case "namespace":
            case "n":
                config.Namespace = value;
                break;
            case "database":
            case "d":
                config.Database = value;
                break;
            case "type":
            case "t":
                config.Type = value;
                break;
            case "entity-mode":
            case "entitymode":
            case "e":
                config.EntityMode = value;
                break;
            case "context":
            case "c":
                config.Context = value;
                break;
            default:
                return (false, $"Unknown configuration key: {key}");
        }

        // Save config
        bool success;
        if (isLocal)
        {
            success = SaveLocalConfig(config);
        }
        else
        {
            success = SaveGlobalConfig(config);
            if (!success)
            {
                // Fall back to local
                success = SaveLocalConfig(config);
                if (success)
                    configPath = GetLocalConfigPath();
            }
        }

        return (success, configPath);
    }

    /// <summary>
    /// Removes a specific configuration value (sets to null/default).
    /// </summary>
    public static (bool success, string path) UnsetConfigValue(
        string key,
        bool forceLocal = false)
    {
        // Load existing config
        OmapConfig config;
        string configPath;
        bool isLocal;

        if (forceLocal)
        {
            configPath = GetLocalConfigPath();
            config = LoadLocalConfig(Directory.GetCurrentDirectory()) ?? new OmapConfig();
            isLocal = true;
        }
        else
        {
            configPath = GlobalConfigPath;
            config = LoadGlobalConfig() ?? new OmapConfig();
            isLocal = false;
        }

        // Remove the specific value
        switch (key.ToLowerInvariant())
        {
            case "locale":
            case "l":
                config.Locale = "en-us"; // Reset to default
                break;
            case "no-pluralize":
            case "nopluralizer":
                config.NoPluralizer = false;
                break;
            case "namespace":
            case "n":
                config.Namespace = null;
                break;
            case "database":
            case "d":
                config.Database = null;
                break;
            case "type":
            case "t":
                config.Type = null;
                break;
            case "entity-mode":
            case "entitymode":
            case "e":
                config.EntityMode = null;
                break;
            case "context":
            case "c":
                config.Context = null;
                break;
            default:
                return (false, $"Unknown configuration key: {key}");
        }

        // Save config
        bool success;
        if (isLocal)
        {
            success = SaveLocalConfig(config);
        }
        else
        {
            success = SaveGlobalConfig(config);
            if (!success)
            {
                // Fall back to local
                success = SaveLocalConfig(config);
                if (success)
                    configPath = GetLocalConfigPath();
            }
        }

        return (success, configPath);
    }

    /// <summary>
    /// Lists all configuration values.
    /// </summary>
    public static Dictionary<string, (string? value, string source)> ListConfig()
    {
        var result = new Dictionary<string, (string? value, string source)>();
        
        var globalConfig = LoadGlobalConfig();
        var localConfig = LoadLocalConfig(Directory.GetCurrentDirectory());
        var effectiveConfig = LoadEffectiveConfig();

        void AddConfig(string key, string? globalVal, string? localVal, string? effectiveVal)
        {
            string source = "default";
            if (localVal != null && localVal != string.Empty)
                source = "local";
            else if (globalVal != null && globalVal != string.Empty)
                source = "global";
            
            result[key] = (effectiveVal, source);
        }

        AddConfig("locale", globalConfig?.Locale, localConfig?.Locale, effectiveConfig.Locale);
        AddConfig("noPluralizer", globalConfig?.NoPluralizer.ToString(), localConfig?.NoPluralizer.ToString(), effectiveConfig.NoPluralizer.ToString());
        AddConfig("namespace", globalConfig?.Namespace, localConfig?.Namespace, effectiveConfig.Namespace);
        AddConfig("database", globalConfig?.Database, localConfig?.Database, effectiveConfig.Database);
        AddConfig("type", globalConfig?.Type, localConfig?.Type, effectiveConfig.Type);
        AddConfig("entityMode", globalConfig?.EntityMode, localConfig?.EntityMode, effectiveConfig.EntityMode);
        AddConfig("context", globalConfig?.Context, localConfig?.Context, effectiveConfig.Context);

        return result;
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
