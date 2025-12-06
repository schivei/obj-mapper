using ObjMapper.Models;

namespace ObjMapper.Tests;

public class ExecutionResultTests
{
    [Fact]
    public void NewExecutionResult_HasNoErrors()
    {
        var result = new ExecutionResult();
        
        Assert.False(result.HasErrors);
        Assert.False(result.HasCriticalError);
        Assert.False(result.HasWarnings);
        Assert.True(result.CanWriteFiles);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.GeneratedFiles);
    }

    [Fact]
    public void AddGeneratedFile_AddsToCollection()
    {
        var result = new ExecutionResult();
        
        result.AddGeneratedFile("/path/to/file.cs", "// content");
        result.AddGeneratedFile("/path/to/another.cs", "// more content");
        
        Assert.Equal(2, result.FilesCount);
        Assert.Equal(2, result.GeneratedFiles.Count);
        Assert.Equal("/path/to/file.cs", result.GeneratedFiles[0].FilePath);
        Assert.Equal("// content", result.GeneratedFiles[0].Content);
    }

    [Fact]
    public void AddError_AddsNonCriticalError()
    {
        var result = new ExecutionResult();
        
        result.AddError("Something went wrong");
        
        Assert.True(result.HasErrors);
        Assert.False(result.HasCriticalError);
        Assert.True(result.CanWriteFiles);
        Assert.Single(result.Errors);
        Assert.Equal("Something went wrong", result.Errors[0]);
    }

    [Fact]
    public void SetCriticalError_PreventsWritingFiles()
    {
        var result = new ExecutionResult();
        result.AddGeneratedFile("/path/to/file.cs", "// content");
        
        result.SetCriticalError("Fatal error");
        
        Assert.True(result.HasErrors);
        Assert.True(result.HasCriticalError);
        Assert.False(result.CanWriteFiles);
        Assert.Single(result.Errors);
        Assert.Contains("[CRITICAL]", result.Errors[0]);
    }

    [Fact]
    public void AddWarning_AddsWarning()
    {
        var result = new ExecutionResult();
        
        result.AddWarning("This is a warning");
        
        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
        Assert.Single(result.Warnings);
        Assert.Equal("This is a warning", result.Warnings[0]);
    }

    [Fact]
    public void MultipleErrorsAndWarnings_AllTracked()
    {
        var result = new ExecutionResult();
        
        result.AddError("Error 1");
        result.AddError("Error 2");
        result.AddWarning("Warning 1");
        result.AddWarning("Warning 2");
        result.AddWarning("Warning 3");
        
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(3, result.Warnings.Count);
        Assert.True(result.CanWriteFiles);
    }

    [Fact]
    public async Task WriteFilesAsync_WritesFilesWhenNoErrors()
    {
        var result = new ExecutionResult();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "test.cs");
        
        try
        {
            result.AddGeneratedFile(filePath, "// test content");
            
            var count = await result.WriteFilesAsync();
            
            Assert.Equal(1, count);
            Assert.True(File.Exists(filePath));
            Assert.Equal("// test content", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task WriteFilesAsync_ReturnsZeroOnCriticalError()
    {
        var result = new ExecutionResult();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "test.cs");
        
        result.AddGeneratedFile(filePath, "// test content");
        result.SetCriticalError("Critical error");
        
        var count = await result.WriteFilesAsync();
        
        Assert.Equal(0, count);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void WriteFiles_WritesFilesWhenNoErrors()
    {
        var result = new ExecutionResult();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "test.cs");
        
        try
        {
            result.AddGeneratedFile(filePath, "// test content");
            
            var count = result.WriteFiles();
            
            Assert.Equal(1, count);
            Assert.True(File.Exists(filePath));
            Assert.Equal("// test content", File.ReadAllText(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void WriteFiles_ReturnsZeroOnCriticalError()
    {
        var result = new ExecutionResult();
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "test.cs");
        
        result.AddGeneratedFile(filePath, "// test content");
        result.SetCriticalError("Critical error");
        
        var count = result.WriteFiles();
        
        Assert.Equal(0, count);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task WriteFilesAsync_CreatesNestedDirectories()
    {
        var result = new ExecutionResult();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nestedPath = Path.Combine(tempDir, "level1", "level2", "test.cs");
        
        try
        {
            result.AddGeneratedFile(nestedPath, "// nested content");
            
            var count = await result.WriteFilesAsync();
            
            Assert.Equal(1, count);
            Assert.True(File.Exists(nestedPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void CanWriteFiles_TrueWithNonCriticalErrors()
    {
        var result = new ExecutionResult();
        
        result.AddError("Non-critical error 1");
        result.AddError("Non-critical error 2");
        
        Assert.True(result.HasErrors);
        Assert.True(result.CanWriteFiles);
    }

    [Fact]
    public void CanWriteFiles_FalseAfterCriticalError()
    {
        var result = new ExecutionResult();
        
        result.AddError("Non-critical error");
        Assert.True(result.CanWriteFiles);
        
        result.SetCriticalError("Critical error");
        Assert.False(result.CanWriteFiles);
    }
}
