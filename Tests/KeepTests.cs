using ChieBot.TemplateTasks;
using Moq;

namespace Tests;

public class KeepTests
{
    private const string TaskDate = "29 февраля 2024";
    private const string TaskPage = $"Википедия:К удалению/{TaskDate}";
    private const string SomePage = "123";
    private const string SomePageTalk = $"Talk:{SomePage}";
    private const int SomePageRevId = 111;
    private const int SomePageTalkRevId = 222;

    private readonly Mock<IMediaWiki> _wiki = new();

    public KeepTests()
    {
        _wiki.Setup(w => w.GetAllPageNames(It.Is<string>(str => str.StartsWith("Template:"))))
            .Returns([KeepModule.TemplateName]);

        _wiki.Setup(w => w.GetPagesInCategory(KeepModule.CategoryName, It.IsAny<MediaWiki.Namespace?>()))
            .Returns([TaskPage]);

        _wiki.Setup(w => w.GetUserGroups(It.IsAny<string[]>()))
            .Returns(new Dictionary<string, string[]>
            {
                ["Jane"] = ["sysop"],
            });

        _wiki.Setup(w => w.GetNamespaces())
            .Returns(new Dictionary<MediaWiki.Namespace, string[]>
            {
                [MediaWiki.Namespace.Template] = ["Ш"],
            });

        _wiki.Setup(w => w.GetHistory(TaskPage, It.IsAny<DateTimeOffset>(), null, false, false))
            .Returns([new MediaWiki.RevisionInfo
            {
                Id = 1,
                User = "Jane",
            }]);

        _wiki.Setup(w => w.GetPage(1))
            .Returns("== " + SomePage + @" ==
удалить

=== Итог ===

{{" + KeepModule.TemplateName + "|" + SomePage + "}}");

        _wiki.Setup(w => w.GetAssociatePageTitle(SomePage))
            .Returns(new Dictionary<string, MediaWiki.PageInfo>
            {
                [SomePage] = new(MediaWiki.Namespace.Talk, SomePageTalk),
            });
    }


    [Fact]
    public void Updates_task_page()
    {
        Setup();

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(TaskPage, @"== 123 ==
удалить

=== Итог ===

{{" + KeepModule.TemplateName + "|" + SomePage + "|сделано}}", It.IsAny<string>(), null, null, null));
    }

    [Fact]
    public void Removes_all_rfd_templates_from_page()
    {
        Setup(page: "bb <noinclude>{{К удалению}}</noinclude> cc");

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, "bb  cc", It.IsAny<string>(), null, null, SomePageRevId));
    }

    [Fact]
    public void Does_not_write_page_when_no_rfd()
    {
        Setup(page: "aaa");

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public void Adds_kept_to_talk()
    {
        Setup(talk: "aaa");

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePageTalk, "{{Оставлено|" + TaskDate + "|l1=123}}\n", It.IsAny<string>(), false, null, SomePageTalkRevId));
    }

    [Fact]
    public void Adds_kept_to_talk_when_doesnt_exist()
    {
        _wiki.Setup(w => w.GetLastRevision(SomePage, false))
            .Returns(new MediaWiki.FullRevisionInfo()
            {
                Id = SomePageRevId,
                Text = "",
            });

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePageTalk, "{{Оставлено|" + TaskDate + "|l1=123}}\n", It.IsAny<string>(), false, null, null));
    }

    [Theory]
    [InlineData("aaa {{Оставлено|12 мартобря 2000|l1=ccc}} bbb", "aaa {{Оставлено|12 мартобря 2000|" + TaskDate + "|l1=ccc|l2=" + SomePage + "}} bbb")]
    [InlineData("{{Оставлено|" + TaskDate + "}}", "{{Оставлено|" + TaskDate + "|l1=" + SomePage + "}}")]
    [InlineData("{{Оставлено|12 мартобря 2000|23 сенявля 2013|l2=ccc}}", "{{Оставлено|12 мартобря 2000|23 сенявля 2013|" + TaskDate + "|l2=ccc|l3=" + SomePage + "}}")]
    [InlineData("{{Оставлено|12 мартобря 2000|l2=ccc}}", "{{Оставлено|12 мартобря 2000|" + TaskDate + "|l2=" + SomePage + "}}")]
    public void Updates_kept_in_talk(string talk, string expected)
    {
        Setup(talk: talk);

        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePageTalk, expected, It.IsAny<string>(), null, null, SomePageTalkRevId));
    }

    [Fact]
    public void Does_nothing_if_page_doesnt_exist()
    {
        new KeepModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
    }

    private void Setup(string page = "", string talk = "")
    {
        _wiki.Setup(w => w.GetLastRevision(SomePage, false))
            .Returns(new MediaWiki.FullRevisionInfo()
            {
                Id = SomePageRevId,
                Text = page,
            });

        _wiki.Setup(w => w.GetLastRevision(SomePageTalk, false))
            .Returns(new MediaWiki.FullRevisionInfo()
            {
                Id = SomePageTalkRevId,
                Text = talk,
            });
    }
}
