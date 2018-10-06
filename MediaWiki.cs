using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChieBot;

public class MediaWiki
{
    private readonly Browser _browser;
    private readonly Uri _apiUri;
    private string _csrfToken;

    public MediaWiki(Uri apiUri, string userAgent)
    {
        _browser = new Browser { UserAgent = userAgent };
        _apiUri = apiUri;
        ReadOnly = true;
    }

    public bool ReadOnly { get; set; }

    private static string JoinList(IEnumerable<string> tokens)
    {
        return string.Join("|", tokens.ToArray());
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

        if (revisons == null)
            return null;
        return revisons.Item2[0].Value<string>("*");
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
        }, pages).ToDictionary(x => x.Key, x => {
            if (x.Value == null)
                return null;
            return new Page
            {
                Title = x.Value.Item1,
                Text = x.Value.Item2[0].Value<string>("*"),
            };
        });
    }

    public IDictionary<string, string[]> GetPageTransclusions(string[] pages, int inNamespace = -1)
    {
        return QueryPages("transcludedin", new Dictionary<string, string>
        {
            { "tinamespace", inNamespace != -1 ? inNamespace.ToString() : null },
            { "tiprop", "title" },
            { "tilimit", "5000" },
        }, false, pages).Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value.Item2.Select(y => y.Value<string>("title")).ToArray());
    }

    public string[] GetPageTransclusions(string page, int inNamespace = -1)
    {
        return GetPageTransclusions(new[] { page }, inNamespace).Values.SingleOrDefault() ?? new string[0];
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

    public IDictionary<string, string[]> GetLinksTo(string[] titles, int? inNamespace = -1)
    {
        return QueryPages("linkshere", new Dictionary<string, string>
        {
            { "lhprop", "title" },
            { "lhnamespace", inNamespace != -1 ? inNamespace.ToString() : null },
            { "lhshow", "!redirect" },
            { "lhlimit", "5000" },
        }, false, titles).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value.Item2.Select(x => x.Value<string>("title")).ToArray());
    }

    public IDictionary<string, string[]> GetTransclusionsOf(string[] titles, int? inNamespace = -1)
    {
        return QueryPages("transcludedin", new Dictionary<string, string>
        {
            { "tiprop", "title" },
            { "tinamespace", inNamespace != -1 ? inNamespace.ToString() : null },
            { "tishow", "!redirect" },
            { "tilimit", "5000" },
        }, false, titles).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value.Item2.Select(x => x.Value<string>("title")).ToArray());
    }

    public RevisionInfo[] GetHistory(string page, DateTimeOffset from)
    {
        var revisions = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "ids|timestamp|size|flags|user" },
            { "rvlimit", "5000" },
            { "rvdir", "newer" },
            { "rvstart", from.ToUniversalTime().ToString("s") },
            { "redirects", "" },
        }, page)[page];

        if (revisions == null)
            return null;
        return revisions.Item2.ToObject<RevisionInfo[]>();
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
        return RawQueryPages("revisions", new Dictionary<string, string> {
            { "revids", revId.ToString() },
            { "rvprop", "ids|timestamp|size" },
            { "redirects", "" },
        })["pages"].Values().Single()["revisions"].Single().ToObject<RevisionInfo>();
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

    public string[] GetPagesInCategory(string categoryName, int pageNamespace = -1)
    {
        return Query(new Dictionary<string, string>
        {
            { "list", "categorymembers" },
            { "cmtitle", "Category:" + categoryName },
            { "cmprop", "title" },
            { "cmnamespace", pageNamespace != -1 ? pageNamespace.ToString() : null },
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

    private IDictionary<int, string[]> _namespaces;
    public IDictionary<int, string[]> GetNamespaces()
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
        ).Distinct().GroupBy(x => x.Item1, x => x.Item2).ToDictionary(x => x.Key, x => x.ToArray());
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

    public void Edit(string page, string contents, string summary, bool? append = null, DateTime? timestamp = null)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "edit" },
            { "title", page },
            { !append.HasValue ? "text" : append.Value ? "appendtext" : "prependtext", contents },
            { "summary", summary },
            { "token", GetCsrfToken() },
            { "basetimestamp", timestamp == null ? null : timestamp.Value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'") },
            { "bot", "" },
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
                catch (System.Net.WebException)
                {
                    sleepTime = TimeSpan.FromSeconds(5);
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
                if (mex != null && mex.Code == ErrorCode.EditConflict) {
                    throw;
                }
                
                if (retries == MaxRetries || !sleepTime.HasValue)
                {
                    Console.Error.WriteLine("After {0} retries: {1}", retries, ex);
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
    }
}

[Serializable]
public class MediaWikiException : Exception
{
    public MediaWikiException() { }
    public MediaWikiException(string message) : base(message) { }
    public MediaWikiException(string message, Exception inner) : base(message, inner) { }
    protected MediaWikiException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
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

    protected MediaWikiApiException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
}
