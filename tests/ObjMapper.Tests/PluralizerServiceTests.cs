using ObjMapper.Services;

namespace ObjMapper.Tests;

public class PluralizerServiceTests
{
    [Theory]
    [InlineData("en-us", "user", "users")]
    [InlineData("en-us", "person", "people")]
    [InlineData("en-us", "child", "children")]
    [InlineData("en-us", "category", "categories")]
    [InlineData("en-us", "box", "boxes")]
    public void PluralizeEnglish_ReturnsCorrectPlural(string locale, string input, string expected)
    {
        var pluralizer = new PluralizerService(locale);
        var result = pluralizer.Pluralize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("en-us", "users", "user")]
    [InlineData("en-us", "people", "person")]
    [InlineData("en-us", "children", "child")]
    [InlineData("en-us", "categories", "category")]
    [InlineData("en-us", "boxes", "box")]
    public void SingularizeEnglish_ReturnsCorrectSingular(string locale, string input, string expected)
    {
        var pluralizer = new PluralizerService(locale);
        var result = pluralizer.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("pt-br", "usuario", "usuarios")]
    [InlineData("pt-br", "animal", "animais")]
    [InlineData("pt-br", "papel", "papéis")]
    public void PluralizePortuguese_ReturnsCorrectPlural(string locale, string input, string expected)
    {
        var pluralizer = new PluralizerService(locale);
        var result = pluralizer.Pluralize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("es-es", "usuario", "usuarios")]
    [InlineData("es-es", "luz", "luces")]
    public void PluralizeSpanish_ReturnsCorrectPlural(string locale, string input, string expected)
    {
        var pluralizer = new PluralizerService(locale);
        var result = pluralizer.Pluralize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Pluralize_WhenDisabled_ReturnsOriginal()
    {
        var pluralizer = new PluralizerService("en-us", disabled: true);
        var result = pluralizer.Pluralize("user");
        Assert.Equal("user", result);
    }

    [Fact]
    public void Singularize_WhenDisabled_ReturnsOriginal()
    {
        var pluralizer = new PluralizerService("en-us", disabled: true);
        var result = pluralizer.Singularize("users");
        Assert.Equal("users", result);
    }

    [Theory]
    [InlineData("ja-jp")]
    [InlineData("ko-kr")]
    [InlineData("zh-cn")]
    public void Pluralize_ForAsianLanguages_ReturnsOriginal(string locale)
    {
        var pluralizer = new PluralizerService(locale);
        var result = pluralizer.Pluralize("user");
        Assert.Equal("user", result);
    }

    [Fact]
    public void SupportedLocales_ContainsExpectedLocales()
    {
        Assert.Contains("en-us", PluralizerService.SupportedLocales);
        Assert.Contains("pt-br", PluralizerService.SupportedLocales);
        Assert.Contains("es-es", PluralizerService.SupportedLocales);
        Assert.Contains("fr-fr", PluralizerService.SupportedLocales);
        Assert.Contains("de-de", PluralizerService.SupportedLocales);
    }
}
