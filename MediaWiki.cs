using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChieBot;

public class MediaWiki : IMediaWiki
{
    private readonly Browser _browser;
    private readonly Uri _apiUri;
    private string _csrfToken;

    public MediaWiki(Uri apiUri, Browser browser)
    {
        _browser = browser;
        _apiUri = apiUri;
    }

    public bool ReadOnly { get; set; } = true;
    public bool BotFlag { get; set; } = true;

    private static string JoinList(IEnumerable<string> tokens)
    {
        return string.Join("|", tokens.ToArray());
    }

    public bool IsLoggedIn()
    {
        var res = Query(new Dictionary<string, string>
        {
            { "meta", "userinfo" },
        }).Single();

        return res["userinfo"]["anon"] == null;
    }

    public void Login(string login, string password)
    {
        _csrfToken = null;
        var res = DoLogin(login, password)["login"];
        if (res.Value<string>("result") == "NeedToken")
            res = DoLogin(login, password, res.Value<string>("token"))["login"];
        if (res.Value<string>("result") != "Success")
            throw new MediaWikiException(res.Value<string>("result") + "\n" + res.Value<string>("reason"));
    }

    private JToken DoLogin(string login, string password, string token = null)
    {
        return Exec(
            new Dictionary<string, string>
            {
                { "action", "login" },
                { "lgname", login },
                { "lgpassword", password },
                { "lgtoken", token },
            }
        );
    }

    private string GetCsrfToken()
    {
        if (_csrfToken == null)
        {
            _csrfToken = Query(new Dictionary<string, string>
            {
                { "meta", "tokens" },
            }).Single()["tokens"].Value<string>("csrftoken");
        }
        return _csrfToken;
    }

    public string GetPage(string page, bool followRedirects = false)
    {
        var revisons = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "content" },
            { "redirects", followRedirects ? "" : null },
        }, page)[page];

        if (revisons == null || revisons.Item2.Count == 0)
            return null;
        return revisons.Item2[0].Value<string>("*");
    }

    public PageInfo GetPageInfo(string page, bool followRedirects = false)
    {
        var revisons = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "ids|timestamp|size|content" },
            { "redirects", followRedirects ? "" : null },
        }, page)[page];

        if (revisons == null || revisons.Item2.Count == 0)
            return null;
        return revisons.Item2.Single().ToObject<PageInfo>();
    }

    public string GetPage(int revId)
    {
        var revisons = RawQueryPages("revisions", new Dictionary<string, string>
        {
            { "revids", revId.ToString(CultureInfo.InvariantCulture) },
            { "rvprop", "content" },
        });

        return revisons["pages"].Values().Select(x => x["revisions"].Single().Value<string>("*")).SingleOrDefault();
    }

    public IDictionary<int, string> GetPages(int[] revIds)
    {
        if (revIds.Length == 0)
            return new Dictionary<int, string>();

        var revisons = RawQueryPages("revisions", new Dictionary<string, string>
        {
            { "revids", JoinList(revIds.Select(revId => revId.ToString(CultureInfo.InvariantCulture))) },
            { "rvprop", "ids|content" },
        });

        return revisons["pages"].Values()
            .SelectMany(x => x["revisions"].Select(r => Tuple.Create(r.Value<int>("revid"), r.Value<string>("*"))))
            .ToDictionary(x => x.Item1, x => x.Item2);
    }

    public IDictionary<string, Page> GetPages(string[] pages, bool followRedirects = false)
    {
        return QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "content" },
            { "redirects", followRedirects ? "" : null },
        }, pages).ToDictionary(x => x.Key, x =>
        {
            if (x.Value == null || x.Value.Item2.Count == 0)
                return null;
            return new Page
            {
                Title = x.Value.Item1,
                Text = x.Value.Item2[0].Value<string>("*"),
            };
        });
    }

    public string[] GetAllPageNames(string title)
    {
        var list = new List<string>();
        title = Normalize(title).Select(x => x.Value).SingleOrDefault() ?? title;
        list.Add(title);

        var query = QueryPages("linkshere", new Dictionary<string, string>
        {
            { "lhprop", "title" },
            { "lhshow", "redirect" },
            { "lhlimit", "5000" },
        }, false, title)[title];
        if (query != null)
            list.AddRange(query.Item2.Select(x => x.Value<string>("title")));

        return list.Distinct().ToArray();
    }

    public IDictionary<string, string[]> GetLinksTo(string[] titles, Namespace? inNamespace = null)
    {
        return QueryPages("linkshere", new Dictionary<string, string>
        {
            { "lhprop", "title" },
            { "lhnamespace", GetNamespace(inNamespace) },
            { "lhshow", "!redirect" },
            { "lhlimit", "5000" },
        }, false, titles).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value.Item2.Select(x => x.Value<string>("title")).ToArray());
    }

    public IDictionary<string, string[]> GetTransclusionsOf(string[] titles, Namespace? inNamespace = null)
    {
        return QueryPages("transcludedin", new Dictionary<string, string>
        {
            { "tiprop", "title" },
            { "tinamespace", GetNamespace(inNamespace) },
            { "tishow", "!redirect" },
            { "tilimit", "5000" },
        }, false, titles).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value.Item2.Select(x => x.Value<string>("title")).ToArray());
    }

    public RevisionInfo[] GetHistory(string page, DateTimeOffset? from = null, DateTimeOffset? to = null, bool includeContents = false, bool includeParsedComment = false)
    {
        var revisions = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "ids|timestamp|size|flags|user" + (includeContents ? "|content" : "") + (includeParsedComment ? "|parsedcomment" : "") },
            { "rvlimit", "5000" },
            { "rvdir", "newer" },
            { "redirects", "" },
            { "rvstart", from?.UtcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK") },
            { "rvend", to?.UtcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK") },
        }, page)[page];

        if (revisions == null)
            return null;
        return revisions.Item2.ToObject<RevisionInfo[]>();
    }

    public Dictionary<ProtectionType, ProtectionInfo> GetProtection(string page)
    {
        var result = RawQueryPages("info", new Dictionary<string, string>
        {
            ["titles"] = page,
            ["inprop"] = "protection",
            ["redirects"] = "",
        })["pages"].Values().Single();

        if (result["missing"] != null)
            return null;

        var ser = JsonSerializer.CreateDefault();
        ser.Converters.Add(new WikiExpiryConverter());
        return result["protection"].ToDictionary(x => x["type"].ToObject<ProtectionType>(ser), x => x.ToObject<ProtectionInfo>(ser));
    }

    public void Protect(string page, string reason, Dictionary<ProtectionType, ProtectionInfo> protections)
    {
        ExecWrite(new Dictionary<string, string>
        {
            ["title"] = page,
            ["reason"] = reason,
            ["action"] = "protect",
            ["expiry"] = JoinList(protections.Select(x => WikiExpiryConverter.ToString(x.Value.Expiry))),
            ["protections"] = JoinList(protections.Select(x =>
                $"{x.Key.ToString().ToLowerInvariant()}={x.Value.Level.ToString().ToLowerInvariant()}")),
            ["token"] = GetCsrfToken(),
        });
    }

    public void HideRevisions(int[] ids, bool hideComment, bool hideUser)
    {
        foreach (var chunk in ids.Partition(500))
        {
            ExecWrite(new Dictionary<string, string>
            {
                ["action"] = "revisiondelete",
                ["type"] = "revision",
                ["ids"] = JoinList(chunk.Select(x => x.ToString())),
                ["hide"] = JoinList(new[] { hideUser ? "user" : null, hideComment ? "comment" : null }.Where(x => x != null)),
                ["token"] = GetCsrfToken(),
            });
        }
    }

    /// <summary>
    /// Normalizes the page title and follows redirects
    /// </summary>
    public IDictionary<string, string> Normalize(params string[] pages)
    {
        pages = pages.Distinct().ToArray();

        var res = RawQueryPages("revisions", new Dictionary<string, string>
        {
            { "titles", JoinList(pages) },
            { "rvprop", "ids" },
            { "rvlimit", pages.Length == 1 ? "1" : null },
            { "redirects", "" },
        }, 1);

        var dic = new Dictionary<string, string>();

        foreach (var page in pages)
        {
            var title = page;
            foreach (var norm in new[] { "normalized", "redirects" }.Select(k => res.Value<JObject>(k)))
            {
                if (norm == null)
                    continue;

                foreach (JProperty prop in norm.Properties())
                {
                    if (prop.Value.Value<string>() == title)
                        title = prop.Name;
                }
            }

            var pp = res.Value<JObject>("pages").Values().SingleOrDefault(p => p.Value<string>("title") == title);
            if (pp == null || pp["missing"] != null)
                continue;

            dic.Add(page, title);
        }

        return dic;
    }

    public RevisionInfo GetRevisionInfo(int revId)
    {
        var result = RawQueryPages("revisions", new Dictionary<string, string> {
            { "revids", revId.ToString() },
            { "rvprop", "ids|timestamp|size" },
            { "redirects", "" },
        })["pages"].Values().Single();

        if (result["missing"] != null)
            return null;

        return result["revisions"]?.Single().ToObject<RevisionInfo>();
    }

    public void Stabilize(string page, string reason, DateTimeOffset? expiry, bool stabilize = true)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "stabilize" },
            { "title", page },
            { "reason", reason },
            { "expiry", expiry.HasValue ? expiry.Value.ToUniversalTime().ToString("s") : "infinity" },
            { "token", GetCsrfToken() },
            { "default", stabilize ? "stable" : "latest" },
        };

        var result = ExecWrite(args);
        if (result["stabilize"] == null)
            throw new MediaWikiException("Invalid response: " + result);
    }

    public bool GetStabilizationExpiry(string title, out DateTimeOffset? expiry)
    {
        var result = Query(new Dictionary<string, string>
        {
            { "list", "logevents" },
            { "letype", "stable" },
            { "letitle", title },
            { "lelimit", "1" },
        }).First().Value<JArray>("logevents").SingleOrDefault();

        expiry = null;
        if (result == null || result.Value<string>("action") == "reset")
            return false;

        var pars = result.Value<JObject>("params");

        // old format was
        // "params": {
        //     "0": "override=1",
        //     "1": "autoreview=",
        //     "2": "expiry=20161222212527",
        //     "3": "precedence=1"
        // },
        if (pars["0"] != null)
        {
            pars = new JObject(
                from par in pars.Values()
                let parts = par.Value<string>().Split(new[] { '=' }, 2)
                select new JProperty(parts[0], parts[1])
            );
        }

        var expiryString = pars.Value<string>("expiry");
        if (expiryString != "infinity" && expiryString != null)
            expiry = new DateTimeOffset(DateTime.ParseExact(expiryString, "yyyyMMddHHmmss", null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)); // DateTimeParse.ParseExact is buggy in mono 3.x
        return true;
    }

    public string[] GetPagesInCategory(string categoryName, Namespace? pageNamespace = null)
    {
        return Query(new Dictionary<string, string>
        {
            { "list", "categorymembers" },
            { "cmtitle", "Category:" + categoryName },
            { "cmprop", "title" },
            { "cmnamespace", GetNamespace(pageNamespace) },
            { "cmtype", "page" },
            { "cmlimit", "5000" },
            { "redirects", "" },
        }).SelectMany(x => x.Value<JArray>("categorymembers"))
            .Select(x => x.Value<string>("title"))
            .ToArray();
    }

    public IDictionary<string, string[]> GetPagesCategories(string[] pages, bool followRedirects = false)
    {
        return QueryPages("categories", new Dictionary<string, string>
        {
            { "cllimit", "5000" },
            { "redirects", followRedirects ? "" : null },
        }, pages)
            .ToDictionary(
                x => x.Key,
                x => x.Value == null
                    ? new string[0] // page not found
                    : x.Value.Item2.Select(cat => cat.Value<string>("title")).ToArray());
    }

    public IDictionary<string, string[]> GetUserGroups(string[] users)
    {
        return Query(new Dictionary<string, string>
        {
            { "list", "users" },
            { "ususers", JoinList(users) },
            { "usprop", "groups" },
        }).SelectMany(x => x["users"]).ToDictionary(x => x.Value<string>("name"), x =>
        {
            var groups = x.Value<JArray>("groups");
            if (groups == null)
                return new string[0];
            return groups.Values<string>().ToArray();
        });
    }

    private IDictionary<Namespace, string[]> _namespaces;
    public IDictionary<Namespace, string[]> GetNamespaces()
    {
        if (_namespaces != null)
            return _namespaces;

        var props = new[] { "*", "canonical" };

        var query = Query(new Dictionary<string, string>
        {
            { "meta", "siteinfo" },
            { "siprop", "namespaces|namespacealiases" },
        }).Single();

        return _namespaces = (
            query["namespaces"].Values().SelectMany(x =>
            {
                var id = x.Value<int>("id");
                return props.Select(p => x.Value<string>(p)).Where(v => v != null).Select(v => Tuple.Create(id, v));
            })
        ).Concat(
            query["namespacealiases"].Select(x => Tuple.Create(x.Value<int>("id"), x.Value<string>("*")))
        ).Distinct().GroupBy(x => x.Item1, x => x.Item2).ToDictionary(x => (Namespace)x.Key, x => x.ToArray());
    }

    private IDictionary<string, Tuple<string, JArray>> QueryPages(string property, IDictionary<string, string> queryArgs, params string[] titles)
    {
        return QueryPages(property, queryArgs, true, titles);
    }

    private IDictionary<string, Tuple<string, JArray>> QueryPages(string property, IDictionary<string, string> queryArgs, bool nullIfMissing = true, params string[] titles)
    {
        if (titles.Length == 0)
            return new Dictionary<string, Tuple<string, JArray>>();

        var mergeSettings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };
        var normalizations = new JObject(titles.Select(t => new JProperty(t, t)));

        var result = new JObject();

        foreach (var titlesChunk in titles.Partition(500))
        {
            result.Merge(RawQueryPages(property, new Dictionary<string, string>(queryArgs)
            {
                { "titles", JoinList(titlesChunk) },
            }), mergeSettings);
        }

        var norm = result.Value<JObject>("normalized");
        if (norm != null)
            normalizations.Merge(norm, mergeSettings);

        var reds = result.Value<JObject>("redirects");
        if (reds != null)
        {
            foreach (var red in reds)
            {
                var n = normalizations.Property((string)red.Value);
                if (n == null)
                    continue;
                n.Remove();
                normalizations[red.Key] = n.Value;
            }
        }

        return result["pages"].Values().ToDictionary(
            x => normalizations.Value<string>(x.Value<string>("title")),
            x =>
            {
                if (nullIfMissing && x["missing"] != null)
                    return null;

                return Tuple.Create(
                    x.Value<string>("title"),
                    x.Value<JArray>(property) ?? new JArray()
                );
            });
    }

    private JObject RawQueryPages(string property, IDictionary<string, string> queryArgs, int? limit = null)
    {
        queryArgs = new Dictionary<string, string>(queryArgs)
        {
            { "prop", property },
        };

        var mergeSettings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };
        var result = new JObject();

        var query = Query(queryArgs);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        foreach (var res in query.Select(res => res ?? new JObject()))
        {
            var resObject = new JObject { res.Property("pages") ?? new JProperty("pages", new JObject()) };

            var normalized = res.Property("normalized");
            if (normalized != null)
                resObject.Add(normalized.Name, ReadNormalizations(normalized.Value.Value<JArray>()));

            var redirects = res.Property("redirects");
            if (redirects != null)
                resObject.Add(redirects.Name, ReadNormalizations(redirects.Value.Value<JArray>()));

            result.Merge(resObject, mergeSettings);
        }

        return result;
    }

    private static JObject ReadNormalizations(JArray normalized)
    {
        return new JObject(normalized.Select(n => new JProperty(n.Value<string>("to"), n.Value<string>("from"))));
    }

    private IEnumerable<JObject> Query(IDictionary<string, string> queryArgs)
    {
        queryArgs = new Dictionary<string, string>(queryArgs)
        {
            { "action", "query" },
        };

        var cont = new JObject(new JProperty("continue", ""));

        for (; ; )
        {
            var args = new Dictionary<string, string>(queryArgs);
            foreach (var p in cont.Properties())
                args.Add(p.Name, p.Value.Value<string>());

            var result = Exec(args);
            yield return result.Value<JObject>("query");

            cont = result.Value<JObject>("continue");
            if (cont == null)
                break;
        }
    }

    public void Edit(string page, string contents, string summary, bool? append = null, DateTime? timestamp = null, int? revId = null)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "edit" },
            { "title", page },
            { !append.HasValue ? "text" : append.Value ? "appendtext" : "prependtext", contents },
            { "summary", summary },
            { "token", GetCsrfToken() },
            { "basetimestamp", timestamp == null ? null : timestamp.Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'") },
            { "baserevid", revId?.ToString() },
            { "bot", BotFlag ? "" : null },
        };

        var result = ExecWrite(args);

        var code = result["edit"].Value<string>("result");
        if (code == null)
            throw new MediaWikiException("Invalid response: " + result);
        if (code != "Success")
            throw new MediaWikiException(code);
    }

    public void Delete(string title, string summary)
    {
        try
        {
            ExecWrite(new Dictionary<string, string>
            {
                { "action", "delete" },
                { "title", title },
                { "reason", summary },
                { "watchlist", "nochange" },
                { "token", GetCsrfToken() },
            });
        }
        catch (MediaWikiApiException ex)
        {
            if (ex.Code != ErrorCode.MissingTitle)
                throw;
        }
    }

    private JToken ExecWrite(Dictionary<string, string> args)
    {
        JToken result;
        if (ReadOnly)
        {
            result = ExecFake(args);
        }
        else
        {
            Dump(args);
            result = Exec(args);
        }
        return result;
    }

    private JToken ExecFake(Dictionary<string, string> args)
    {
        Dump(args);
        return new JObject
        {
            new JProperty(args["action"], new JObject(new JProperty("result", "Success")))
        };
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void Dump(Dictionary<string, string> args)
    {
        System.Diagnostics.Debug.WriteLine("Edit:");
        Dump(args, str => System.Diagnostics.Debug.WriteLine(str));
    }

    private static void Dump(IDictionary<string, string> args, Action<string> writeLine)
    {
        foreach (var arg in args)
        {
            var value = arg.Value;
            if (arg.Key.StartsWith("lg"))
                value = "***";
            writeLine(string.Format("  {0} = {1}", arg.Key, value));
        }
        writeLine("");
    }

    private JToken Exec(IDictionary<string, string> args)
    {
        var log = "";
        const int MaxRetries = 5;
        for (int retries = 1; ; retries++)
        {
            TimeSpan? sleepTime = null;

            try
            {
                try
                {
                    return DoExec(args);
                }
                catch (MediaWikiApiException ex)
                {
                    if (ex.Code == ErrorCode.ReadOnly)
                        sleepTime = TimeSpan.FromSeconds(30);
                    throw;
                }
                catch (System.Net.WebException wex)
                {
                    sleepTime = TimeSpan.FromMinutes(1);
                    log += $"{DateTime.UtcNow.ToLongTimeString()} Got '{wex.Message}', waiting for {sleepTime}\n";
                    throw;
                }
                catch (System.IO.IOException)
                {
                    sleepTime = TimeSpan.FromSeconds(5);
                    throw;
                }
            }
            catch (Exception ex)
            {
                var mex = ex as MediaWikiApiException;
                if (mex != null && mex.Code == ErrorCode.EditConflict)
                    throw;

                if (mex != null && mex.Code == ErrorCode.Blocked)
                {
                    Console.Error.Write("Blocked. IsLoggedIn(): ");
                    try
                    {
                        Console.Error.WriteLine(IsLoggedIn());
                    }
                    catch (Exception ex2)
                    {
                        Console.Error.WriteLine(ex2);
                    }
                }

                if (retries == MaxRetries || !sleepTime.HasValue)
                {
                    Console.Error.WriteLine($"{log}After {retries} retries: {ex}");
                    Console.Error.WriteLine("Query was:");
                    Dump(args, Console.Error.WriteLine);
                    throw;
                }

                System.Threading.Thread.Sleep(sleepTime.Value);
            }
        }
    }

    private JToken DoExec(IDictionary<string, string> args)
    {
        args["format"] = "json";
        var result = JToken.Parse(_browser.Post(_apiUri.AbsoluteUri, args));
        if (result["error"] != null)
            throw new MediaWikiApiException(result["error"].Value<string>("code"), result["error"].Value<string>("info"));
        return result;
    }

    [System.Diagnostics.DebuggerDisplay("{User,nq} {Size} {Timestamp}")]
    public class RevisionInfo
    {
        [JsonProperty("revid")]
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string User { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public int Size { get; set; }

        [JsonProperty("Anon")]
        [JsonConverter(typeof(WikiBoolConverter))]
        public bool Anonymous { get; set; }

        [JsonConverter(typeof(WikiBoolConverter))]
        public bool UserHidden { get; set; }

        [JsonConverter(typeof(WikiBoolConverter))]
        public bool CommentHidden { get; set; }

        public string ParsedComment { get; set; }

        /// <summary>Ревизорское скрытие</summary>
        [JsonConverter(typeof(WikiBoolConverter))]
        public bool Suppressed { get; set; }
    }

    public class ProtectionInfo
    {
        public ProtectionLevel Level { get; set; }

        [JsonConverter(typeof(WikiExpiryConverter))]
        public DateTimeOffset? Expiry { get; set; }
    }

    public enum ProtectionType
    {
        Edit,
        Move,
    }

    public enum ProtectionLevel
    {
        AutoConfirmed,
        EditAutoReviewProtected,
        SysOp,
    }

    public class PageInfo : RevisionInfo
    {
        [JsonProperty("*")]
        public string Text { get; set; }
    }

    public class Page
    {
        public string Title { get; set; }
        public string Text { get; set; }
    }

    public static string EscapeTitle(string title)
    {
        return title.Replace(' ', '_');
    }

    public static string UnscapeTitle(string title)
    {
        return title.Replace('_', ' ');
    }

    public static bool TitlesEqual(string title1, string title2)
    {
        if (title1 == "" || title2 == "")
            return title1 == title2;
        return char.ToUpperInvariant(title1[0]) == char.ToUpperInvariant(title2[0])
            && string.Equals(UnscapeTitle(title1.Substring(1)), UnscapeTitle(title2.Substring(1)), StringComparison.Ordinal);
    }

    public enum ErrorCode
    {
        ReadOnly,
        MissingTitle,
        EditConflict,
        Blocked,
    }

    public enum Namespace
    {
        Article,
        Talk,
        User,
        UserTalk,
        Wikipedia,
        WikipediaTalk,
        File,
        FileTalk,
        MediaWiki,
        MediaWikiTalk,
        Template,
        TemplateTalk,
        Help,
        HelpTalk,
        Category,
        CategoryTalk,
    }

    private static string GetNamespace(Namespace? nsOrNull)
        => nsOrNull is { } ns ? ((int)ns).ToString() : null;
}

[Serializable]
public class MediaWikiException : Exception
{
    public MediaWikiException() { }
    public MediaWikiException(string message) : base(message) { }
    public MediaWikiException(string message, Exception inner) : base(message, inner) { }
}

[Serializable]
public class MediaWikiApiException : MediaWikiException
{
    public MediaWiki.ErrorCode? Code { get; private set; }
    public string StringCode { get; private set; }

    public MediaWikiApiException(string code, string message)
        : base(string.Format("{0}: {1}", code, message))
    {
        StringCode = code;

        MediaWiki.ErrorCode ec;
        if (Enum.TryParse(code, true, out ec))
            Code = ec;
    }
}

class WikiBoolConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(bool);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Expected a string, found: {reader.TokenType}");

        return true;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }
}

class WikiExpiryConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        (Nullable.GetUnderlyingType(objectType) ?? objectType) == typeof(DateTimeOffset);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Date)
            return new DateTimeOffset((DateTime)reader.Value);

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Expected a string, found: {reader.TokenType}");

        var value = (string)reader.Value;
        if (value == "infinity")
            return null;

        return DateTimeOffset.Parse(value);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(ToString((DateTimeOffset?)value));
    }

    public static string ToString(DateTimeOffset? expiry) =>
        expiry == null ? "infinity" : expiry.Value.ToUniversalTime().ToString("s") + "Z";
}
