namespace ObjMapper.Models;

/// <summary>
/// Represents the result of a code generation execution.
/// Collects all generated files in memory and tracks errors.
/// </summary>
public sealed class ExecutionResult
{
    private readonly List<(string FilePath, string Content)> _generatedFiles = [];
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private bool _hasCriticalError;

    /// <summary>
    /// Gets whether the execution has a critical error that should stop writing files.
    /// </summary>
    public bool HasCriticalError => _hasCriticalError;

    /// <summary>
    /// Gets whether there are any errors (critical or non-critical).
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// Gets whether there are any warnings.
    /// </summary>
    public bool HasWarnings => _warnings.Count > 0;

    /// <summary>
    /// Gets the list of errors that occurred.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets the list of warnings that occurred.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

    /// <summary>
    /// Gets the list of generated files (file path, content).
    /// </summary>
    public IReadOnlyList<(string FilePath, string Content)> GeneratedFiles => _generatedFiles.AsReadOnly();

    /// <summary>
    /// Gets the count of generated files.
    /// </summary>
    public int FilesCount => _generatedFiles.Count;

    /// <summary>
    /// Adds a generated file to the result.
    /// </summary>
    public void AddGeneratedFile(string filePath, string content)
    {
        _generatedFiles.Add((filePath, content));
    }

    /// <summary>
    /// Adds a non-critical error. Execution continues but the error is logged.
    /// </summary>
    public void AddError(string message)
    {
        _errors.Add(message);
    }

    /// <summary>
    /// Sets a critical error that will prevent files from being written to disk.
    /// </summary>
    public void SetCriticalError(string message)
    {
        _hasCriticalError = true;
        _errors.Add($"[CRITICAL] {message}");
    }

    /// <summary>
    /// Adds a warning. Execution continues and the warning is logged.
    /// </summary>
    public void AddWarning(string message)
    {
        _warnings.Add(message);
    }

    /// <summary>
    /// Gets whether it is safe to write files to disk.
    /// Files are only written when no critical errors occurred.
    /// </summary>
    public bool CanWriteFiles => !_hasCriticalError;

    /// <summary>
    /// Writes all generated files to disk if no critical errors occurred.
    /// </summary>
    /// <returns>Number of files written, or 0 if there was a critical error.</returns>
    public async Task<int> WriteFilesAsync()
    {
        if (!CanWriteFiles)
        {
            return 0;
        }

        var count = 0;
        foreach (var (filePath, content) in _generatedFiles)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(filePath, content);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Writes all generated files to disk synchronously if no critical errors occurred.
    /// </summary>
    /// <returns>Number of files written, or 0 if there was a critical error.</returns>
    public int WriteFiles()
    {
        if (!CanWriteFiles)
        {
            return 0;
        }

        var count = 0;
        foreach (var (filePath, content) in _generatedFiles)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, content);
            count++;
        }

        return count;
    }
}
