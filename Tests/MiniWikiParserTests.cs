using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChieBot;
using static System.Net.Mime.MediaTypeNames;
using static ChieBot.MiniWikiParser;

namespace Tests;

public class MiniWikiParserTests
{
    [Theory]
    [InlineData(false, """
        [[link
        with an enter]]
        """)]
    [InlineData(false, """
        [[link
        |with an enter]]
        """)]
    [InlineData(true, """
        [[link|
        with an enter]]
        """)]
    [InlineData(false, """
        {{template
        with an enter}}
        """)]
    [InlineData(false, """
        {{template
        with|an enter}}
        """)]
    [InlineData(false, """
        {{template
        {{with}}|an enter}}
        """)]
    [InlineData(true, """
        {{template
        |with an enter}}
        """)]
    [InlineData(true, """
        {{template


        |with an enter}}
        """)]
    [InlineData(false, """
        {{template

        x

        |with an enter}}
        """)]
    [InlineData(true, """
        {{template
         |with an enter}}
        """)]
    [InlineData(true, """
        {{template|
        with an enter}}
        """)]
    public void Newlines_in_links_and_tempaltes(bool shouldParse, string text)
    {
        var result = new MiniWikiParser().Tokenize(text);

        var single = Assert.Single(result);

        if (shouldParse)
            Assert.NotEqual(TokenType.Text, single.Type);
        else
            Assert.Equal(TokenType.Text, single.Type);
    }

    [Fact]
    public void Newlines_in_links_followed_by_a_link()
    {
        var text = """
            [[link
            with an [[enter]]
            """;

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.Link);
        Assert.Equal("[[enter]]", result[1].Text);
    }

    [Fact]
    public void Parses_newlines_in_links_and_tempaltes_correctly()
    {
        var text = """
            text [[link]] [[another
            link]] [[another|
            link]] [[more]]
            """;

        var result = new MiniWikiParser().Tokenize(text);

        AssertTokenTypes(result, TokenType.Text, TokenType.Link, TokenType.Text, TokenType.Link, TokenType.Whitespace, TokenType.Link);
    }

    [Fact]
    public void Does_not_allow_nested_links()
    {
        var text = "[[link|nested [[link]] isnt allowed]]";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.Link, TokenType.Text);
        Assert.Equal("[[link]]", result[1].Text);
    }

    [Fact]
    public void Does_not_allow_nested_links_for_non_files()
    {
        var text = "[[NonFile:link|nested [[link]] isnt allowed]]";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.Link, TokenType.Text);
        Assert.Equal("[[link]]", result[1].Text);
    }

    [Theory]
    [InlineData("[[File:link|nested [[link]] is allowed]]")]
    [InlineData("[[        File:link|nested [[link]] is allowed]]")]
    public void Allows_nested_links_in_files(string text)
    {
        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Link);
    }

    [Fact]
    public void Allows_double_nested_files()
    {
        var text = "[[File:link|nested [[File:link|with description]] is allowed]]";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Link);
    }

    [Fact]
    public void Allows_nested_links_in_multiline_files()
    {
        var text = """
            [[File:link|
            nested
             [[link]] is
            allowed]]
            """;

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Link);
    }

    [Fact]
    public void Does_not_allow_nested_links_in_file_names()
    {
        var text = "[[File:nested [[link]] is not allowed]]";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.Link, TokenType.Text);
        Assert.Equal("[[link]]", result[1].Text);
    }

    [Fact]
    public void Does_not_allow_double_nested_links_in_files()
    {
        var text = "[[File:link|nested [[File:link|nested [[link]] is not allowed]] is not allowed]]";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.Link, TokenType.Text);
        Assert.Equal("[[link]]", result[1].Text);
    }

    [Fact(Skip = "not implemented")]
    public void Trailing_text_is_included_in_link()
    {
        var text = "[[link|with]]trailing text";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Link, TokenType.Text);
        Assert.Equal("[[link|with]]trailing", result[0].Text);
    }

    [Fact]
    public void Newline_is_parsed_correctly_after_whitespace()
    {
        var text = "text\t\nmore text";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Text, TokenType.NewLine, TokenType.Text);
    }

    [Theory]
    [InlineData("{{template|with {{nested}} tempalte|inside}}")]
    public void Nested_templates_are_allowed(string text)
    {
        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Template);
    }

    [Fact(Skip = "not implemented")]
    public void Allows_links_in_templates()
    {
        var text = "{{template|[[with|}}link]]}}";

        var result = new MiniWikiParser().Tokenize(text).ToArray();

        AssertTokenTypes(result, TokenType.Template);
    }

    private static void AssertTokenTypes(IEnumerable<Token> result, params TokenType[] expected)
    {
        Assert.Equal(expected, result.Select(r => r.Type));
    }
}
