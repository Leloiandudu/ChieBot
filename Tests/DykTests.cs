using ChieBot;
using ChieBot.DYK;
using Moq;

namespace Tests;

public class DykTests
{
    public static readonly DateTimeOffset IssueDate = new DateTime(2012, 5, 4).WithTimeZone(DYKModule.TimeZone);

    public DykTests()
    {
        DYKUtils.ReferenceDate = IssueDate.ToDateOnly();
    }

    [Fact]
    public void EndToEnd()
    {
        const string ArchiveName = "Проект:Знаете ли вы/Архив рубрики/2012-05";
        const string DraftArchiveName = "Обсуждение проекта:Знаете ли вы/Черновик/Архив/1";
        var futureStblDate = new DateTimeOffset(new DateOnly(2099, 1, 1), default, default);

        Dictionary<string, string> hooks = new(DykResources.Hooks.Split("\n!!!\n").Select(hook =>
        {
            var lines = hook.Split("\n", 2);
            var parts = lines[1].Split("!!!");

            var text = "{{Сообщение ЗЛВ|даты=15—18 мая 2012|текст=" + parts[0] + "|архив=2012-05#15—18 мая";
            if (parts.Length > 1)
                text += "|иллюстрация2=" + parts[1];
            text += "}}\n";

            return new KeyValuePair<string, string>($"Talk:{lines[0]}", text);
        }));

        // arrange
        var wiki = new MockWiki();
        wiki.SetPage(DidYouKnow.DraftName, DykResources.DraftBefore);
        wiki.SetPage(DidYouKnow.DraftTalkName, DykResources.DraftTalkBefore);
        wiki.SetPage(ArchiveName, DykResources.ArchiveBefore);
        wiki.SetPage(DidYouKnow.NextIssueName, DykResources.PreparationBefore);
        wiki.SetPage(DidYouKnow.TemplateName, DykResources.TemplateBefore);
        wiki.SetPage(DidYouKnow.NextIssueNameHeader, DykResources.TimetableBefore);
        wiki.SetPage(DraftArchiveName, DykResources.DraftArchiveBefore);

        foreach (var hook in hooks.Keys)
            wiki.SetPage(hook, null);

        wiki.SetStabilization("frutiger Aero", null);
        wiki.SetStabilization("Мустафа I", futureStblDate);

        // act
        var module = new DYKModule { ExecutionTime = new DateTime(2012, 5, 18, 0, 0, 0, DateTimeKind.Utc)};
        DYKUtils.ReferenceDate = module.ExecutionTime.ToDateOnly();
        module.Execute(wiki, []);

        // assert
        Assert.Equal(DykResources.DraftAfter, wiki.GetPage(DidYouKnow.DraftName));
        Assert.Equal(DykResources.DraftTalkAfter, wiki.GetPage(DidYouKnow.DraftTalkName));
        Assert.Equal(DykResources.ArchiveAfter, wiki.GetPage(ArchiveName));
        Assert.Equal(DykResources.PreparationAfter, wiki.GetPage(DidYouKnow.NextIssueName));
        Assert.Equal(DykResources.TemplateAfter, wiki.GetPage(DidYouKnow.TemplateName));
        Assert.Equal(DykResources.TimetableAfter, wiki.GetPage(DidYouKnow.NextIssueNameHeader));
        Assert.Equal(DykResources.DraftArchiveAfter, wiki.GetPage(DraftArchiveName));

        foreach (var hook in hooks)
            Assert.Equal(hook.Value, wiki.GetPage(hook.Key));

        AssertStabilizationUntil(wiki, "frutiger Aero", null);
        AssertStabilizationUntil(wiki, "Мустафа I", futureStblDate);

        var pages = new[]
        {
            "Антаманов, Майк", "Ахмед I", "Гвидо Новелло да Полента", "Гумбатов, Энвер",
            "Кадо (пойнтер)", "Мавзолей «Джомард-Кассаб»", "Мягкая белорусизация",
            "Навратилова, Мартина", "Некрасова, Зинаида Николаевна",
            "Палауский диалект английского языка", "Пиранези, Франческо", "Хиллз, Арнолд",
        };

        var expectedUntil = new DateTime(2012, 5, 21).WithTimeZone(DYKModule.TimeZone);
        foreach (var page in pages)
            AssertStabilizationUntil(wiki, page, expectedUntil);
    }

    private static void AssertStabilizationUntil(IMediaWiki wiki, string title, DateTimeOffset? expected)
    {
        var result = wiki.GetStabilizationExpiry(title, out var actual);
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PopDraft_FindsCorrectDraft()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftName, false)).Returns(DraftPage.FullPageBefore);
        wiki.Setup(w => w.Edit(DidYouKnow.DraftName, DraftPage.FullPageAfter, It.IsAny<string>(), null, null, null));

        var dyk = new DidYouKnow(wiki.Object, IssueDate);
        var draft = dyk.PopDraft();

        Assert.Equal(DraftPage.Issue, draft);
        wiki.VerifyAll();
    }

    [Fact]
    public void PopDraft_CantFindNonExistent()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftName, false)).Returns(DraftPage.FullPageBefore);

        Assert.Throws<DidYouKnowException>(() => new DidYouKnow(wiki.Object, IssueDate.AddDays(-1)).PopDraft());
        Assert.Throws<DidYouKnowException>(() => new DidYouKnow(wiki.Object, IssueDate.AddDays(+1)).PopDraft());

        wiki.VerifyAll();
    }

    [Fact]
    public void ArchiveDraftTalk_MovesTheWholeSectionToArchive()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftTalkName, false)).Returns(DraftPage.FullPageBefore);
        wiki.Setup(w => w.Edit(DidYouKnow.DraftTalkName, DraftPage.FullPageAfter, It.IsAny<string>(), null, null, null));
        wiki.Setup(w => w.Edit(It.Is<string>(s => s.StartsWith(DidYouKnow.DraftTalkName + "/")), "\n\n" + DraftPage.IssueFullText + "\n\n", It.IsAny<string>(), true, null, null));

        var dyk = new DidYouKnow(wiki.Object, IssueDate.AddDays(3));
        var result = dyk.ArchiveDraftTalk();

        Assert.True(result);
        wiki.VerifyAll();
    }

    [Fact]
    public void ArchiveDraftTalk_DoesntArchiveIfDoesntExist()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftTalkName, false)).Returns(DraftPage.FullPageBefore);

        var dyk = new DidYouKnow(wiki.Object, IssueDate.AddDays(1));
        var result = dyk.ArchiveDraftTalk();

        Assert.False(result);
        wiki.VerifyAll();
    }

    [Fact]
    public void ArchiveCurrent_Works()
    {
        var expectedContents = $@"== 4—7 мая ==

{DraftPage.Issue}

";

        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.TemplateName, false)).Returns(TemplatePage.FullText);
        wiki.Setup(w => w.GetPage("Talk:items", false)).Returns<string>(null!);
        wiki.Setup(w => w.GetPage("Talk:them", false)).Returns("{{Сообщение ЗЛВ}}");
        wiki.Setup(w => w.GetPage("Talk:one", false)).Returns<string>(null!);

        wiki.Setup(w => w.Edit("Проект:Знаете ли вы/Архив рубрики/2012-05", expectedContents, It.IsAny<string>(), false, null, null));
        wiki.Setup(w => w.Edit("Talk:items", "{{Сообщение ЗЛВ|даты=4—7 мая 2012|текст=A lot more '''[[items]]''' here {{наилл}}|архив=2012-05#4—7 мая|иллюстрация2=[[Файл:Some pic.jpg|140px|Some pic]]}}\n", It.IsAny<string>(), false, null, null));
        wiki.Setup(w => w.Edit("Talk:one", "{{Сообщение ЗЛВ|даты=4—7 мая 2012|текст=One last '''[[one]]'''|архив=2012-05#4—7 мая}}\n", It.IsAny<string>(), false, null, null));

        var dyk = new DidYouKnow(wiki.Object, IssueDate.AddDays(3));
        dyk.ArchiveCurrent();

        wiki.VerifyAll();
    }

    [Theory]
    [InlineData(4, 5, "4—7 мая")]
    [InlineData(30, 5, "30 мая — 2 июня")]
    public void ArchiveCurrent_UsesCorrectTitle(int day1, int month1, string result)
    {
        var issueDate = new DateTime(DateTime.UtcNow.Year, month1, day1, 0, 0, 0, DateTimeKind.Utc);

        var expectedContents = $"== {result} ==";

        var template = TemplatePage.GetTemplateText($@"== Выпуск {issueDate:d MMMM} ==
{DraftPage.Issue}");

        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.TemplateName, false)).Returns(template);
        wiki.Setup(w => w.Edit(It.IsAny<string>(), It.Is<string>(str => str.StartsWith(expectedContents)), It.IsAny<string>(), false, null, null));
        wiki.Setup(w => w.GetPage(It.IsRegex("^Talk:.*"), false)).Returns<string>(null!);
        wiki.Setup(w => w.Edit(It.IsRegex("^Talk:.*"), It.IsAny<string>(), It.IsAny<string>(), false, null, null));

        var dyk = new DidYouKnow(wiki.Object, issueDate.AddDays(3));
        dyk.ArchiveCurrent();

        wiki.VerifyAll();
    }

    [Theory]
    [InlineData("'''[[one]]''' '''[[two]]''' [[x]] '''[[three]]'''")]
    [InlineData("'''[[one]]''''''[[two]]''', [[three|'''three''']]")]
    [InlineData("'''[[one]] [[two]]''' '''[[three]]'''")]
    [InlineData("'''[[one]] [[two]] [[three]]'''")]
    [InlineData("'''[[one]] [[two]]''' [[x]] '''[[three]]'''")]
    public void Finds_all_bold_links(string text)
    {
        var links = ParserUtils.FindBoldLinks(text);
        Assert.Equal(new[] { "one", "two", "three" }, links);
    }

    static class DraftPage
    {
        public const string Before = @"{{shortcut|ПРО:ЗЛВ-Ч|ПРО:ЗЛВЧ}}

== Выпуск 1 мая (выпускающий Robot) ==
[[Файл:Some pic.jpg|right|140px|Some pic]]
* Some items here {{наилл}}
* Lots of them
* One more
{{-}}";

        public const string IssueTitle = "== Выпуск 4 мая (выпускающий C3PO) ==";

        public const string Issue = @"[[Файл:Some pic.jpg|right|140px|Some pic]]
* A lot more '''[[items]]''' here {{наилл}}
* Lots of '''[[them]]'''
* One last '''[[one]]'''
{{-}}";

        public const string IssueFullText = $"{IssueTitle}\n{Issue}";

        public const string After = @"== Выпуск 7 мая (выпускающий R2D2) ==
[[Файл:Some pic.jpg|right|140px|Some pic]]
* Even more items here {{наилл}}
* Lots of them
* One very last one
{{-}}
";

        public const string FullPageBefore = @$"{Before}

{IssueTitle}
{Issue}

{After}";

        public const string FullPageAfter = @$"{Before}

{After}";
    }

    static class TemplatePage
    {
        public static readonly string FullText = GetTemplateText(DraftPage.Issue);

        public static string GetTemplateText(string template) => @$"<noinclude>{{{{doc}}}}</noinclude>
<!-- BOT — между этими комментариями бот Lê Lợi (bot) вставит из черновика очередной выпуск -->
{template}
<!-- BOT — между этими комментариями бот Lê Lợi (bot) вставит из черновика очередной выпуск -->
{{{{clear|1={{{{{{clear-inline|both}}}}}}}}}}
";
    }
}
