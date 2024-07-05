using System.Text.RegularExpressions;

namespace Tests;
internal class MockWiki : IMediaWiki
{
    private readonly Dictionary<string, string> _pages = [];

    private readonly Dictionary<string, DateTimeOffset?> _stabilization = [];

    public void SetPage(string page, string contents)
    {
        _pages.Add(page, contents);
    }

    public string GetPage(string page) => _pages[page];

    public void SetStabilization(string page, DateTimeOffset? until)
    {
        _stabilization.Add(page, until);
    }

    void IMediaWiki.Delete(string title, string summary)
    {
        throw new NotImplementedException();
    }

    void IMediaWiki.Edit(string page, string contents, string summary, bool? append, DateTime? timestamp, int? revId)
    {
        _pages.TryGetValue(page, out var text);
        text ??= "";

        if (append == null)
        {
            text = contents;
        }
        else if (append == true)
        {
            text += contents;
        }
        else
        {
            var match = new Regex("^=", RegexOptions.Multiline).Match(text);
            var index = match.Success ? match.Index : 0;
            text = text[..index] + contents + text[index..];
        }

        _pages[page] = text;
    }

    string[] IMediaWiki.GetAllPageNames(string title)
    {
        throw new NotImplementedException();
    }

    MediaWiki.RevisionInfo[] IMediaWiki.GetHistory(string page, DateTimeOffset? from, DateTimeOffset? to, bool includeContents, bool includeParsedComment)
    {
        throw new NotImplementedException();
    }

    IDictionary<string, string[]> IMediaWiki.GetLinksTo(string[] titles, MediaWiki.Namespace? inNamespace)
    {
        throw new NotImplementedException();
    }

    IDictionary<MediaWiki.Namespace, string[]> IMediaWiki.GetNamespaces()
    {
        throw new NotImplementedException();
    }

    string IMediaWiki.GetPage(int revId)
    {
        throw new NotImplementedException();
    }

    string IMediaWiki.GetPage(string page, bool followRedirects) => GetPage(page);

    MediaWiki.PageInfo IMediaWiki.GetPageInfo(string page, bool followRedirects)
    {
        throw new NotImplementedException();
    }

    IDictionary<int, string> IMediaWiki.GetPages(int[] revIds)
    {
        throw new NotImplementedException();
    }

    IDictionary<string, MediaWiki.Page> IMediaWiki.GetPages(string[] pages, bool followRedirects)
    {
        throw new NotImplementedException();
    }

    IDictionary<string, string[]> IMediaWiki.GetPagesCategories(string[] pages, bool followRedirects)
    {
        throw new NotImplementedException();
    }

    string[] IMediaWiki.GetPagesInCategory(string categoryName, MediaWiki.Namespace? pageNamespace)
    {
        throw new NotImplementedException();
    }

    Dictionary<MediaWiki.ProtectionType, MediaWiki.ProtectionInfo> IMediaWiki.GetProtection(string page)
    {
        throw new NotImplementedException();
    }

    MediaWiki.RevisionInfo IMediaWiki.GetRevisionInfo(int revId)
    {
        throw new NotImplementedException();
    }

    bool IMediaWiki.GetStabilizationExpiry(string title, out DateTimeOffset? expiry) =>
        _stabilization.TryGetValue(title, out expiry);

    IDictionary<string, string[]> IMediaWiki.GetTransclusionsOf(string[] titles, MediaWiki.Namespace? inNamespace)
    {
        throw new NotImplementedException();
    }

    IDictionary<string, string[]> IMediaWiki.GetUserGroups(string[] users)
    {
        throw new NotImplementedException();
    }

    void IMediaWiki.HideRevisions(int[] ids, bool hideComment, bool hideUser)
    {
        throw new NotImplementedException();
    }

    bool IMediaWiki.IsLoggedIn()
    {
        throw new NotImplementedException();
    }

    void IMediaWiki.Login(string login, string password)
    {
        throw new NotImplementedException();
    }

    IDictionary<string, string> IMediaWiki.Normalize(params string[] pages)
    {
        throw new NotImplementedException();
    }

    void IMediaWiki.Protect(string page, string reason, Dictionary<MediaWiki.ProtectionType, MediaWiki.ProtectionInfo> protections)
    {
        throw new NotImplementedException();
    }

    void IMediaWiki.Stabilize(string page, string reason, DateTimeOffset? expiry, bool stabilize)
    {
        _stabilization[page] = expiry;
    }

    bool IMediaWiki.BotFlag { get; set; } = true;

    bool IMediaWiki.ReadOnly => throw new NotImplementedException();
}
