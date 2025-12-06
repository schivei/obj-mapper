using ObjMapper.Models;
using ObjMapper.Generators;
using Xunit;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for view extraction and code generation.
/// </summary>
public class ViewExtractionTests
{
    [Fact]
    public void EfCoreGenerator_GeneratesEntityForView()
    {
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            Tables =
            [
                new TableInfo
                {
                    Name = "active_users_view",
                    Schema = "dbo",
                    Columns =
                    [
                        new ColumnInfo { Column = "user_id", Type = "int", Schema = "dbo", Table = "active_users_view" },
                        new ColumnInfo { Column = "username", Type = "varchar", Schema = "dbo", Table = "active_users_view" },
                        new ColumnInfo { Column = "last_login", Type = "datetime", Schema = "dbo", Table = "active_users_view" }
                    ]
                }
            ]
        };
        
        var entities = generator.GenerateEntities(schema);
        
        Assert.Single(entities);
        Assert.Contains("ActiveUsersView.cs", entities.Keys);
        Assert.Contains("public partial class ActiveUsersView", entities.Values.First());
    }
    
    [Fact]
    public void DapperGenerator_GeneratesEntityForView()
    {
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "Test");
        var schema = new DatabaseSchema
        {
            Tables =
            [
                new TableInfo
                {
                    Name = "sales_summary",
                    Schema = "public",
                    Columns =
                    [
                        new ColumnInfo { Column = "product_name", Type = "varchar", Schema = "public", Table = "sales_summary" },
                        new ColumnInfo { Column = "total_sales", Type = "decimal", Schema = "public", Table = "sales_summary" }
                    ]
                }
            ]
        };
        
        var entities = generator.GenerateEntities(schema);
        
        Assert.Single(entities);
        Assert.Contains("SalesSummary.cs", entities.Keys);
    }
    
    [Fact]
    public void SchemaExtractionOptions_IncludeViews_DefaultsToTrue()
    {
        var options = new SchemaExtractionOptions();
        
        Assert.True(options.IncludeViews);
    }
    
    [Fact]
    public void CommandOptions_NoViews_MapsToExtractionOptions()
    {
        var cmdOptions = new CommandOptions { NoViews = true };
        var extractionOptions = SchemaExtractionOptions.FromCommandOptions(cmdOptions);
        
        Assert.False(extractionOptions.IncludeViews);
    }
}
