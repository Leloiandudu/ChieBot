using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChieBot;
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

    private void AssertTokenTypes(IEnumerable<Token> result, params TokenType[] expected)
    {
        Assert.Equal(expected, result.Select(r => r.Type));
    }
}
