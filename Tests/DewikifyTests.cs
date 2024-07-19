using ChieBot.Dewikify;
using Moq;

namespace Tests;

public class DewikifyTests
{
    private const string SomePage = "SomePage";

    private readonly Mock<IMediaWiki> _wiki = new();

    public DewikifyTests()
    {
        _wiki.Setup(w => w.GetAllPageNames(It.Is<string>(str => str.StartsWith("Template:"))))
            .Returns([DewikifyModule.TemplateName]);

        _wiki.Setup(w => w.GetPagesInCategory(DewikifyModule.CategoryName, It.IsAny<MediaWiki.Namespace?>()))
            .Returns([SomePage]);

        _wiki.Setup(w => w.GetUserGroups(It.IsAny<string[]>()))
            .Returns(new Dictionary<string, string[]>
            {
                ["Jane"] = ["sysop"],
                ["John"] = [],
                ["Mary"] = [],
                ["BenBot"] = ["sysop", "bot"],
            });

        _wiki.Setup(w => w.GetNamespaces())
            .Returns(new Dictionary<MediaWiki.Namespace, string[]>
            {
                [MediaWiki.Namespace.Template] = ["Ш"],
            });
    }

    [Theory]
    [InlineData("{{Девикифицировать вхождения|x=123}}")]
    [InlineData("{{Девикифицировать вхождения|x=123|сделано}}")]
    [InlineData("{{Девикифицировать вхождения|123|543}}")]
    [InlineData("{{Девикифицировать вхождения|123|x=543}}")]
    [InlineData("{{Девикифицировать вхождения}}")]
    public void Checks_tempalte_args(string template)
    {
        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([new MediaWiki.RevisionInfo
            {
                Id = 1,
                User = "John",
            }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns(template);

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, $"<span style='color: red'>Ошибка в шаблоне <nowiki>{template}</nowiki>: '''неверный формат аргументов'''</span>", It.IsAny<string>(), null, null, null));
    }

    [Fact]
    public void Only_allows_template_in_summary()
    {
        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([new MediaWiki.RevisionInfo
            {
                Id = 1,
                User = "John",
            }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns("{{Девикифицировать вхождения|123}}");

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, "<span style='color: red'>Шаблон <nowiki>{{Девикифицировать вхождения|123}}</nowiki> должен находиться в секции '''Итоги'''.</span>", It.IsAny<string>(), null, null, null));
    }

    [Theory]
    [InlineData("John")]
    [InlineData("BenBot")]
    public void Only_allows_ops_to_use_tempalte(string user)
    {
        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([new MediaWiki.RevisionInfo
            {
                Id = 1,
                User = user,
            }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns(@"== 123 ==
удалить

=== Итог ===

{{Девикифицировать вхождения|123}}");

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, @"== 123 ==
удалить

=== Итог ===

<span style='color: red'>Шаблон <nowiki>{{Девикифицировать вхождения|123}}</nowiki> установлен пользователем {{u|" + user + "}}, не имеющим флага ПИ/А.</span>", It.IsAny<string>(), null, null, null));
    }


    [Fact]
    public void Finds_user_correctly()
    {
        const string Contents = @"== 123 ==
удалить

=== Итог ===

{{Девикифицировать вхождения|123}}";

        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([
                new() { Id = 1, User = "Mary" },
                new() { Id = 2, User = "Jane" },
                new() { Id = 3, User = "Mary" },
                new() { Id = 4, User = "John" },
                new() { Id = 5, User = "Mary" },
            ]);

        _wiki.Setup(w => w.GetPage(5))
            .Returns(Contents);

        _wiki.Setup(w => w.GetPages(new[] { 4, 3, 2, 1 }))
            .Returns(new Dictionary<int, string>
            {
                [1] = "",
                [2] = Contents,
                [3] = "",
                [4] = Contents,
            });

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, @"== 123 ==
удалить

=== Итог ===

<span style='color: red'>Шаблон <nowiki>{{Девикифицировать вхождения|123}}</nowiki> установлен пользователем {{u|John}}, не имеющим флага ПИ/А.</span>", It.IsAny<string>(), null, null, null));
    }

    [Fact]
    public void Dewikifies()
    {
        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([new MediaWiki.RevisionInfo
            {
                Id = 1,
                User = "Jane",
            }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns(@"== 123 ==
удалить

=== Итог ===

{{Девикифицировать вхождения|123}}");

        _wiki.Setup(w => w.GetAllPageNames("123"))
            .Returns(["123", "123!"]);

        _wiki.Setup(w => w.GetLinksTo(new[] { "123", "123!" }, 0))
            .Returns(new Dictionary<string, string[]>
            {
                ["123"] = ["page1"],
                ["123!"] = [],
            });

        _wiki.Setup(w => w.GetTransclusionsOf(new[] { "123", "123!" }, 0))
            .Returns(new Dictionary<string, string[]>
            {
                ["123"] = ["page2"],
                ["123!"] = [],
            });

        _wiki.Setup(w => w.GetPages(It.IsAny<string[]>(), false))
            .Returns(new Dictionary<string, MediaWiki.Page>
            {
                ["page1"] = new() { Title = "page1", Text = "try [[123|this]] link" },
                ["page2"] = new() { Title = "page2", Text = @"try {{123}} link
{{123}}

end" },
            });

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, @"== 123 ==
удалить

=== Итог ===

{{Девикифицировать вхождения|123|сделано}}", It.IsAny<string>(), null, null, null));
        _wiki.Verify(w => w.Edit("page1", "try this link", It.IsAny<string>(), null, null, null));
        _wiki.Verify(w => w.Edit("page2", @"try  link

end", It.IsAny<string>(), null, null, null));
        _wiki.Verify(w => w.Delete("123!", It.IsAny<string>()));
    }

    [Fact]
    public void Skips_done()
    {
        _wiki.Setup(w => w.GetHistory(SomePage, It.IsAny<DateTimeOffset>(), null, false, false))
    .Returns([new MediaWiki.RevisionInfo
    {
        Id = 1,
        User = "John",
    }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns(@"== 123 ==
удалить

=== Итог ===

{{Девикифицировать вхождения|123|сделано}}");

        new DewikifyModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
    }
}
