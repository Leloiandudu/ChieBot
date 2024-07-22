using ChieBot.TemplateTasks;
using Moq;

namespace Tests;
public class TemplateTaskUtils_RemoveForDeletionTemplate_Tests
{
    private readonly Mock<IMediaWiki> _wiki = new();

    public TemplateTaskUtils_RemoveForDeletionTemplate_Tests()
    {
        _wiki.Setup(w => w.GetNamespaces())
            .Returns(new Dictionary<MediaWiki.Namespace, string[]>
            {
                [MediaWiki.Namespace.Template] = ["Ш"],
            });
    }

    [Fact]
    public void Removes_all_rfd_templates()
    {
        var text = "<noinclude>{{К удалению}}</noinclude>\naa {{К удалению}} bb <noinclude>{{К удалению}}</noinclude> cc <noinclude>{{К удалению}}!</noinclude>";

        var result = TemplateTaskUtils.RemoveForDeletionTemplate(new(_wiki.Object), text, out var newText);

        Assert.True(result);
        Assert.Equal("aa  bb  cc <noinclude>!</noinclude>", newText);
    }

    [Fact]
    public void Returns_false_when_nothing_to_remove()
    {
        var text = "aaa";

        var result = TemplateTaskUtils.RemoveForDeletionTemplate(new(_wiki.Object), text, out _);

        Assert.False(result);
    }
}
