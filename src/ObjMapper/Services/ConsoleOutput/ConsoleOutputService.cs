using System.Diagnostics;
using System.Text;
using Spectre.Console;

namespace ObjMapper.Services.ConsoleOutput;

/// <summary>
/// Provides rich console output with progress bars, colors, and logging.
/// Cross-platform compatible using Spectre.Console.
/// </summary>
public sealed class ConsoleOutputService : IDisposable
{
    private const int MaxDisplayLength = 60;
    private const int TruncatedLength = 57;
    
    private readonly StringBuilder _logBuffer = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly string _logFilePath;
    private bool _disposed;

    public string LogFilePath => _logFilePath;
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public ConsoleOutputService()
    {
        var tempDir = Path.GetTempPath();
        var logFileName = $"omap-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..8]}.log";
        _logFilePath = Path.Combine(tempDir, logFileName);
        _stopwatch.Start();
        
        Log("ObjMapper execution started");
        Log($"Log file: {_logFilePath}");
    }

    /// <summary>
    /// Writes a header banner to the console.
    /// </summary>
    public void WriteHeader()
    {
        var panel = new Panel(
            new FigletText("ObjMapper")
                .Color(Color.Cyan1))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey]Database Reverse Engineering Tool[/]");
        AnsiConsole.WriteLine();
        
        Log("Header displayed");
    }

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    public void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
        Log($"[INFO] {message}");
    }

    /// <summary>
    /// Writes a success message.
    /// </summary>
    public void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
        Log($"[SUCCESS] {message}");
    }

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
        Log($"[WARNING] {message}");
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
        Log($"[ERROR] {message}");
    }

    /// <summary>
    /// Writes a section header.
    /// </summary>
    public void WriteSection(string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(title)}[/]") { Justification = Justify.Left });
        Log($"[SECTION] {title}");
    }

    /// <summary>
    /// Writes a key-value pair.
    /// </summary>
    public void WriteKeyValue(string key, string value)
    {
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(key)}:[/] {Markup.Escape(value)}");
        Log($"  {key}: {value}");
    }

    /// <summary>
    /// Writes a file creation message.
    /// </summary>
    public void WriteFileCreated(string filePath)
    {
        AnsiConsole.MarkupLine($"  [green]→[/] [grey]{Markup.Escape(filePath)}[/]");
        Log($"  Created: {filePath}");
    }

    /// <summary>
    /// Writes a table of statistics.
    /// </summary>
    public void WriteStatistics(Dictionary<string, int> stats)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Metric[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Count[/]").RightAligned());

        foreach (var (key, value) in stats)
        {
            table.AddRow(Markup.Escape(key), value.ToString());
        }

        AnsiConsole.Write(table);
        
        foreach (var (key, value) in stats)
        {
            Log($"  {key}: {value}");
        }
    }

    /// <summary>
    /// Runs an action with a spinner.
    /// </summary>
    public async Task<T> WithSpinnerAsync<T>(string message, Func<Task<T>> action)
    {
        Log($"[TASK START] {message}");
        var taskStopwatch = Stopwatch.StartNew();
        
        T result = default!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(message, async ctx =>
            {
                result = await action();
            });
        
        taskStopwatch.Stop();
        Log($"[TASK END] {message} - {taskStopwatch.ElapsedMilliseconds}ms");
        
        return result;
    }

    /// <summary>
    /// Runs an action with a spinner (void return).
    /// </summary>
    public async Task WithSpinnerAsync(string message, Func<Task> action)
    {
        await WithSpinnerAsync(message, async () =>
        {
            await action();
            return 0;
        });
    }

    /// <summary>
    /// Runs an action with a progress bar.
    /// </summary>
    public async Task WithProgressAsync(string description, int totalSteps, Func<Action<int, string>, Task> action)
    {
        Log($"[PROGRESS START] {description} - {totalSteps} steps");
        var progressStopwatch = Stopwatch.StartNew();
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            ])
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]{Markup.Escape(description)}[/]", maxValue: totalSteps);
                
                void UpdateProgress(int step, string stepDescription)
                {
                    task.Description = $"[cyan]{Markup.Escape(stepDescription)}[/]";
                    task.Increment(1);
                    Log($"  Step {step}/{totalSteps}: {stepDescription}");
                }
                
                await action(UpdateProgress);
                
                task.Value = totalSteps;
            });
        
        progressStopwatch.Stop();
        Log($"[PROGRESS END] {description} - {progressStopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Writes the final summary with elapsed time and log file location.
    /// </summary>
    public void WriteSummary(bool success, int filesGenerated = 0)
    {
        _stopwatch.Stop();
        
        AnsiConsole.WriteLine();
        
        if (success)
        {
            var panel = new Panel(
                new Markup($"[green bold]Generation completed successfully![/]\n\n" +
                          $"[grey]Files generated:[/] [white]{filesGenerated}[/]\n" +
                          $"[grey]Total time:[/] [white]{FormatElapsed(_stopwatch.Elapsed)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 1),
                Header = new PanelHeader("[green] ✓ Success [/]")
            };
            AnsiConsole.Write(panel);
        }
        else
        {
            var panel = new Panel(
                new Markup($"[red bold]Generation failed![/]\n\n" +
                          $"[grey]Total time:[/] [white]{FormatElapsed(_stopwatch.Elapsed)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(2, 1),
                Header = new PanelHeader("[red] ✗ Error [/]")
            };
            AnsiConsole.Write(panel);
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Detailed log:[/] [link={_logFilePath}]{Markup.Escape(_logFilePath)}[/]");
        
        Log($"Execution completed - Success: {success}, Files: {filesGenerated}, Elapsed: {FormatElapsed(_stopwatch.Elapsed)}");
    }

    /// <summary>
    /// Writes the configuration table.
    /// </summary>
    public void WriteConfiguration(Dictionary<string, string> config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Setting[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Value[/]").LeftAligned());

        foreach (var (key, value) in config)
        {
            var displayValue = value.Length > MaxDisplayLength 
                ? value[..TruncatedLength] + "..." 
                : value;
            table.AddRow(Markup.Escape(key), Markup.Escape(displayValue));
        }

        AnsiConsole.Write(table);
        
        foreach (var (key, value) in config)
        {
            Log($"  {key}: {value}");
        }
    }

    private void Log(string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _logBuffer.AppendLine($"[{timestamp}] {message}");
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        if (elapsed.TotalMinutes >= 1)
            return $"{elapsed.Minutes}m {elapsed.Seconds}s";
        if (elapsed.TotalSeconds >= 1)
            return $"{elapsed.TotalSeconds:F2}s";
        return $"{elapsed.TotalMilliseconds:F0}ms";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        try
        {
            File.WriteAllText(_logFilePath, _logBuffer.ToString());
        }
        catch (IOException)
        {
            // Ignore I/O errors when writing log file (e.g., disk full)
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore permission errors when writing log file
        }
    }
}
