using ObjMapper.Services;

namespace ObjMapper.Tests;

public class NamingHelperTests
{
    [Theory]
    [InlineData("user_id", "UserId")]
    [InlineData("created_at", "CreatedAt")]
    [InlineData("users", "Users")]
    [InlineData("order_items", "OrderItems")]
    [InlineData("ID", "Id")]
    [InlineData("firstName", "FirstName")]
    [InlineData("123test", "_123test")]
    public void ToPascalCase_ReturnsCorrectValue(string input, string expected)
    {
        var result = NamingHelper.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserId", "userId")]
    [InlineData("CreatedAt", "createdAt")]
    [InlineData("Users", "users")]
    public void ToCamelCase_ReturnsCorrectValue(string input, string expected)
    {
        var result = NamingHelper.ToCamelCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("users", "User")]
    [InlineData("orders", "Order")]
    [InlineData("order_items", "OrderItem")]
    [InlineData("categories", "Category")]
    [InlineData("addresses", "Address")]
    [InlineData("user", "User")]
    public void ToEntityName_ReturnsCorrectValue(string input, string expected)
    {
        var result = NamingHelper.ToEntityName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("User", "Users")]
    [InlineData("Order", "Orders")]
    [InlineData("Category", "Categories")]
    [InlineData("Address", "Addresses")]
    public void ToCollectionName_ReturnsCorrectValue(string input, string expected)
    {
        var result = NamingHelper.ToCollectionName(input);
        Assert.Equal(expected, result);
    }
}
