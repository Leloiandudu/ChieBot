using System;
using System.Collections.Generic;

public interface IMediaWiki
{
    bool BotFlag { get; set; }
    bool ReadOnly { get; }

    void Delete(string title, string summary);
    void Edit(string page, string contents, string summary, bool? append = null, DateTime? timestamp = null, int? revId = null);
    string[] GetAllPageNames(string title);
    MediaWiki.RevisionInfo[] GetHistory(string page, DateTimeOffset? from = null, DateTimeOffset? to = null, bool includeContents = false, bool includeParsedComment = false);
    IDictionary<string, string[]> GetLinksTo(string[] titles, MediaWiki.Namespace? inNamespace = null);
    IDictionary<MediaWiki.Namespace, string[]> GetNamespaces();
    string GetPage(int revId);
    string GetPage(string page, bool followRedirects = false);
    MediaWiki.PageInfo GetPageInfo(string page, bool followRedirects = false);
    IDictionary<int, string> GetPages(int[] revIds);
    IDictionary<string, MediaWiki.Page> GetPages(string[] pages, bool followRedirects = false);
    IDictionary<string, string[]> GetPagesCategories(string[] pages, bool followRedirects = false);
    string[] GetPagesInCategory(string categoryName, MediaWiki.Namespace? pageNamespace = null);
    Dictionary<MediaWiki.ProtectionType, MediaWiki.ProtectionInfo> GetProtection(string page);
    MediaWiki.RevisionInfo GetRevisionInfo(int revId);
    bool GetStabilizationExpiry(string title, out DateTimeOffset? expiry);
    IDictionary<string, string[]> GetTransclusionsOf(string[] titles, MediaWiki.Namespace? inNamespace = null);
    IDictionary<string, string[]> GetUserGroups(string[] users);
    void HideRevisions(int[] ids, bool hideComment, bool hideUser);
    bool IsLoggedIn();
    void Login(string login, string password);
    IDictionary<string, string> Normalize(params string[] pages);
    void Protect(string page, string reason, Dictionary<MediaWiki.ProtectionType, MediaWiki.ProtectionInfo> protections);
    void Stabilize(string page, string reason, DateTimeOffset? expiry, bool stabilize = true);
}