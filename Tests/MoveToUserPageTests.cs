using ChieBot.TemplateTasks;
using Moq;
using Newtonsoft.Json.Linq;
using static MediaWiki;

namespace Tests;

public class MoveToUserPageTests
{
    private const string TaskDate = "29 февраля 2024";
    private const string TaskPage = $"Википедия:К удалению/{TaskDate}";
    private const string SomePage = "123";
    private const string SomeUser= "John 2 14";
    private const int SomePageRevId = 111;
    private const int TaskPageRevId = 1;

    private readonly Mock<IMediaWiki> _wiki = new();

    public MoveToUserPageTests()
    {
        _wiki.Setup(w => w.GetAllPageNames(It.Is<string>(str => str.StartsWith("Template:"))))
            .Returns([MoveToUserPageModule.TemplateName]);

        _wiki.Setup(w => w.GetPagesInCategory(MoveToUserPageModule.CategoryName, It.IsAny<MediaWiki.Namespace?>()))
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
                Id = TaskPageRevId,
                User = "Jane",
            }]);

        _wiki.Setup(w => w.GetPage(TaskPageRevId))
            .Returns($"""
            == {SomePage} ==
            удалить

            === Итог ===

            {"{{"}{MoveToUserPageModule.TemplateName}|{SomePage}|{SomeUser}{"}}"}
            """);

        _wiki.Setup(w => w.GetUsers(SomeUser))
            .Returns(new Dictionary<string, JToken>
            {
                [SomeUser] = new JObject(),
            });
    }


    [Fact]
    public void Updates_task_page()
    {
        Setup();

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(TaskPage, $"""
        == {SomePage} ==
        удалить

        === Итог ===

        {"{{"}{MoveToUserPageModule.TemplateName}|{SomePage}|{SomeUser}|сделано{"}}"}
        """, It.IsAny<string>(), null, null, null));
    }

    [Fact]
    public void Updates_target_page()
    {
        Setup("""
            <noinclude>{{К удалению}}</noinclude>

            текст статьи

            немого текста {{с|шаблонами}}

            * {{еще|один}}

            {{навигационный шаблон}} <!-- вложенный комментарий -->

            [[Категория:123]]
            [[К:456]]
            """);

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, """
            {{Временная статья}}

            текст статьи

            немого текста {{с|шаблонами}}

            * {{еще|один}}

            <!--{{навигационный шаблон}} <!~~ вложенный комментарий ~~>

            [[Категория:123]]
            [[К:456]]-->
            """, It.IsAny<string>(), null, null, SomePageRevId));
    }

    [Fact]
    public void Updates_target_page_2()
    {
        Setup("""
            текст статьи

            немого текста {{с|шаблонами}}

            [[обычная ссылка]]


            {{навигационный шаблон}} <!-- вложенный комментарий -->

            [[Категория:123]]
            [[К:456]]

            """);

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, """
            {{Временная статья}}
            текст статьи

            немого текста {{с|шаблонами}}

            [[обычная ссылка]]


            <!--{{навигационный шаблон}} <!~~ вложенный комментарий ~~>

            [[Категория:123]]
            [[К:456]]
            -->
            """, It.IsAny<string>(), null, null, SomePageRevId));
    }

    [Fact]
    public void Moves_the_page()
    {
        Setup();

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Move(SomePage, $"User:{SomeUser}/{SomePage}", It.IsAny<string>(), false));
    }

    [Fact]
    public void Does_nothing_if_page_doesnt_exist()
    {
        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(SomePage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
        _wiki.Verify(w => w.Move(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Theory]
    [InlineData(Namespace.User)]
    [InlineData(Namespace.TemplateTalk)]
    public void Does_nothing_if_target_page_namespace_is_wrong(Namespace ns)
    {
        _wiki.Setup(w => w.GetLastRevision(SomePage, false))
            .Returns(new FullRevisionInfo()
            {
                Id = SomePageRevId,
                Namespace = ns,
                Text = "lala",
            });

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(TaskPage, It.Is<string>(s => s.StartsWith($"""
            == {SomePage} ==
            удалить

            === Итог ===

            <span style='color: red'>Ошибка в шаблоне <nowiki>{"{{"}{MoveToUserPageModule.TemplateName}|{SomePage}|{SomeUser}{"}}"}</nowiki>: '''
            """)), It.IsAny<string>(), null, null, null));

        _wiki.Verify(w => w.Edit(SomePage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
        _wiki.Verify(w => w.Move(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void Does_nothing_if_user_doesnt_exist()
    {
        const string nonExistentUser = "nonexistent-user";

        Setup();
        _wiki.Setup(w => w.GetPage(TaskPageRevId))
            .Returns($"""
            == {SomePage} ==
            удалить

            === Итог ===

            {"{{"}{MoveToUserPageModule.TemplateName}|{SomePage}|{nonExistentUser}{"}}"}
            """);

        _wiki.Setup(w => w.GetUsers(nonExistentUser)).Returns(new Dictionary<string, JToken>());

        new MoveToUserPageModule().Execute(_wiki.Object, []);

        _wiki.Verify(w => w.Edit(TaskPage, It.Is<string>(s => s.StartsWith($"""
            == {SomePage} ==
            удалить

            === Итог ===

            <span style='color: red'>Ошибка в шаблоне <nowiki>{"{{"}{MoveToUserPageModule.TemplateName}|{SomePage}|{nonExistentUser}{"}}"}</nowiki>: '''участник не существует'''</span>
            """)), It.IsAny<string>(), null, null, null));

        _wiki.Verify(w => w.GetUsers(nonExistentUser));
        _wiki.Verify(w => w.Edit(SomePage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
        _wiki.Verify(w => w.Move(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    private void Setup(string page = "")
    {
        _wiki.Setup(w => w.GetLastRevision(SomePage, false))
            .Returns(new FullRevisionInfo()
            {
                Id = SomePageRevId,
                Text = page,
            });
    }
}
