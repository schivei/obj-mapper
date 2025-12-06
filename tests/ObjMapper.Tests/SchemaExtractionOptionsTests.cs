using ObjMapper.Models;
using Xunit;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for SchemaExtractionOptions and CommandOptions.
/// </summary>
public class SchemaExtractionOptionsTests
{
    [Fact]
    public void SchemaExtractionOptions_DefaultValues()
    {
        var options = new SchemaExtractionOptions();
        
        Assert.True(options.EnableTypeInference);
        Assert.True(options.EnableDataSampling);
        Assert.True(options.IncludeViews);
        Assert.True(options.IncludeStoredProcedures);
        Assert.True(options.IncludeUserDefinedFunctions);
        Assert.True(options.IncludeRelationships);
        Assert.False(options.EnableLegacyRelationshipInference);
        Assert.Null(options.SchemaFilter);
    }
    
    [Fact]
    public void CommandOptions_NoInference_DisablesTypeInference()
    {
        var options = new CommandOptions { NoInference = true };
        
        Assert.False(options.UseTypeInference);
    }
    
    [Fact]
    public void CommandOptions_NoChecks_DisablesDataSampling()
    {
        var options = new CommandOptions { NoChecks = true };
        
        Assert.False(options.UseDataSampling);
    }
    
    [Fact]
    public void CommandOptions_NoViews_DisablesViewMapping()
    {
        var options = new CommandOptions { NoViews = true };
        
        Assert.False(options.IncludeViews);
    }
    
    [Fact]
    public void CommandOptions_NoProcs_DisablesProcedureMapping()
    {
        var options = new CommandOptions { NoProcs = true };
        
        Assert.False(options.IncludeProcs);
    }
    
    [Fact]
    public void CommandOptions_NoUdfs_DisablesFunctionMapping()
    {
        var options = new CommandOptions { NoUdfs = true };
        
        Assert.False(options.IncludeUdfs);
    }
    
    [Fact]
    public void CommandOptions_NoRel_DisablesRelationshipMapping()
    {
        var options = new CommandOptions { NoRel = true };
        
        Assert.False(options.IncludeRelationships);
    }
    
    [Fact]
    public void CommandOptions_Legacy_EnablesLegacyInference()
    {
        var options = new CommandOptions { Legacy = true };
        
        Assert.True(options.UseLegacyInference);
    }
    
    [Fact]
    public void CommandOptions_LegacyWithNoRel_DisablesLegacyInference()
    {
        // Legacy and NoRel are mutually exclusive - NoRel takes precedence
        var options = new CommandOptions { Legacy = true, NoRel = true };
        
        Assert.False(options.UseLegacyInference);
        Assert.False(options.IncludeRelationships);
    }
    
    [Fact]
    public void FromCommandOptions_MapsAllOptions()
    {
        var cmdOptions = new CommandOptions
        {
            SchemaFilter = "myschema",
            NoInference = true,
            NoChecks = true,
            NoViews = true,
            NoProcs = true,
            NoUdfs = true,
            NoRel = true,
            Legacy = false
        };
        
        var extractionOptions = SchemaExtractionOptions.FromCommandOptions(cmdOptions);
        
        Assert.Equal("myschema", extractionOptions.SchemaFilter);
        Assert.False(extractionOptions.EnableTypeInference);
        Assert.False(extractionOptions.EnableDataSampling);
        Assert.False(extractionOptions.IncludeViews);
        Assert.False(extractionOptions.IncludeStoredProcedures);
        Assert.False(extractionOptions.IncludeUserDefinedFunctions);
        Assert.False(extractionOptions.IncludeRelationships);
        Assert.False(extractionOptions.EnableLegacyRelationshipInference);
    }
    
    [Fact]
    public void FromCommandOptions_LegacyEnabled()
    {
        var cmdOptions = new CommandOptions
        {
            Legacy = true,
            NoRel = false
        };
        
        var extractionOptions = SchemaExtractionOptions.FromCommandOptions(cmdOptions);
        
        Assert.True(extractionOptions.IncludeRelationships);
        Assert.True(extractionOptions.EnableLegacyRelationshipInference);
    }
    
    [Fact]
    public void CommandOptions_UseConnectionString_TrueWhenSet()
    {
        var options = new CommandOptions { ConnectionString = "Server=localhost;Database=test" };
        
        Assert.True(options.UseConnectionString);
    }
    
    [Fact]
    public void CommandOptions_UseConnectionString_FalseWhenEmpty()
    {
        var options = new CommandOptions { ConnectionString = "" };
        
        Assert.False(options.UseConnectionString);
    }
    
    [Fact]
    public void CommandOptions_UseConnectionString_FalseWhenNull()
    {
        var options = new CommandOptions { ConnectionString = null };
        
        Assert.False(options.UseConnectionString);
    }
}
