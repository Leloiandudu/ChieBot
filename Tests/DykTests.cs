using ChieBot.DYK;
using Moq;

namespace Tests;

public class DykTests
{
    public static readonly DateTime IssueDate = new(DateTime.UtcNow.Year, 5, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndToEnd()
    {
        const string ArchiveName = "Проект:Знаете ли вы/Архив рубрики/2024-05";
        const string DraftArchiveName = "Обсуждение проекта:Знаете ли вы/Черновик/Архив/13";
        var futureStblDate = new DateTimeOffset(new DateOnly(2099, 1, 1), default, default);

        // arrange
        var wiki = new MockWiki();
        wiki.SetPage(DidYouKnow.DraftName, DykResources.DraftBefore);
        wiki.SetPage(DidYouKnow.DraftTalkName, DykResources.DraftTalkBefore);
        wiki.SetPage(ArchiveName, DykResources.ArchiveBefore);
        wiki.SetPage(DidYouKnow.NextIssueName, DykResources.PreparationBefore);
        wiki.SetPage(DidYouKnow.TemplateName, DykResources.TemplateBefore);
        wiki.SetPage(DidYouKnow.NextIssueNameHeader, DykResources.TimetableBefore);
        wiki.SetPage(DraftArchiveName, DykResources.DraftArchiveBefore);

        wiki.SetStabilization("frutiger Aero", null);
        wiki.SetStabilization("Мустафа I", futureStblDate);

        // act
        var module = new DYKModule { ExecutionTime = new DateTime(2024, 5, 18, 0, 0, 0, DateTimeKind.Utc)};
        module.Execute(wiki, []);

        // assert
        Assert.Equal(DykResources.DraftAfter, wiki.GetPage(DidYouKnow.DraftName));
        Assert.Equal(DykResources.DraftTalkAfter, wiki.GetPage(DidYouKnow.DraftTalkName));
        Assert.Equal(DykResources.ArchiveAfter, wiki.GetPage(ArchiveName));
        Assert.Equal(DykResources.PreparationAfter, wiki.GetPage(DidYouKnow.NextIssueName));
        Assert.Equal(DykResources.TemplateAfter, wiki.GetPage(DidYouKnow.TemplateName));
        Assert.Equal(DykResources.TimetableAfter, wiki.GetPage(DidYouKnow.NextIssueNameHeader));
        Assert.Equal(DykResources.DraftArchiveAfter, wiki.GetPage(DraftArchiveName));

        AssertStabilizationUntil(wiki, "frutiger Aero", null);
        AssertStabilizationUntil(wiki, "Мустафа I", futureStblDate);

        var pages = new[]
        {
            "Антаманов, Майк", "Ахмед I", "Гвидо Новелло да Полента", "Гумбатов, Энвер",
            "Кадо (пойнтер)", "Мавзолей «Джомард-Кассаб»", "Мягкая белорусизация",
            "Навратилова, Мартина", "Некрасова, Зинаида Николаевна",
            "Палауский диалект английского языка", "Пиранези, Франческо", "Хиллз, Арнолд",
        };

        var expectedUntil = new DateTimeOffset(new DateOnly(2024, 5, 21), default, TimeSpan.FromHours(3));
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

        var dyk = new DidYouKnow(wiki.Object);
        var draft = dyk.PopDraft(IssueDate);

        Assert.Equal(DraftPage.Issue, draft);
        wiki.VerifyAll();
    }

    [Fact]
    public void PopDraft_CantFindNonExistent()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftName, false)).Returns(DraftPage.FullPageBefore);

        var dyk = new DidYouKnow(wiki.Object);

        Assert.Throws<DidYouKnowException>(() => dyk.PopDraft(IssueDate.AddDays(-1)));
        Assert.Throws<DidYouKnowException>(() => dyk.PopDraft(IssueDate.AddDays(+1)));

        wiki.VerifyAll();
    }

    [Fact]
    public void ArchiveDraftTalk_MovesTheWholeSectionToArchive()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftTalkName, false)).Returns(DraftPage.FullPageBefore);
        wiki.Setup(w => w.Edit(DidYouKnow.DraftTalkName, DraftPage.FullPageAfter, It.IsAny<string>(), null, null, null));
        wiki.Setup(w => w.Edit(It.Is<string>(s => s.StartsWith(DidYouKnow.DraftTalkName + "/")), "\n\n" + DraftPage.IssueFullText + "\n\n", It.IsAny<string>(), true, null, null));

        var dyk = new DidYouKnow(wiki.Object);
        var result = dyk.ArchiveDraftTalk(IssueDate);

        Assert.True(result);
        wiki.VerifyAll();
    }

    [Fact]
    public void ArchiveDraftTalk_DoesntArchiveIfDoesntExist()
    {
        var wiki = new Mock<IMediaWiki>(MockBehavior.Strict);
        wiki.Setup(w => w.GetPage(DidYouKnow.DraftTalkName, false)).Returns(DraftPage.FullPageBefore);

        var dyk = new DidYouKnow(wiki.Object);
        var result = dyk.ArchiveDraftTalk(IssueDate.AddDays(1));

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
        wiki.Setup(w => w.Edit("Проект:Знаете ли вы/Архив рубрики/2024-05", expectedContents, It.IsAny<string>(), false, null, null));

        var dyk = new DidYouKnow(wiki.Object);
        dyk.ArchiveCurrent(IssueDate, IssueDate.AddDays(3));

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

        var dyk = new DidYouKnow(wiki.Object);
        dyk.ArchiveCurrent(issueDate, issueDate.AddDays(3));

        wiki.VerifyAll();
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
* A lot more items here {{наилл}}
* Lots of them
* One last one
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
