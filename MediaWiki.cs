using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MediaWiki
{
    private readonly Browser _browser;
    private readonly Uri _apiUri;

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
        var res = DoLogin(login, password)["login"];
        if (res.Value<string>("result") == "NeedToken")
            res = DoLogin(login, password, res.Value<string>("token"))["login"];
        if (res.Value<string>("result") != "Success")
            throw new MediaWikiException(res.Value<string>("result"));
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

    public string GetPage(string page, int? revId = null, bool followRedirects = false)
    {
        var revisons = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "content" },
            { "redirects", followRedirects ? "" : null },
            { "rvstartid", revId == null ? null : revId.ToString() },
            { "rvendid", revId == null ? null : revId.ToString() },
        }, page)[page];

        if (revisons == null)
            return null;
        return revisons.Item2[0].Value<string>("*");
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

    public string[] GetPageTransclusions(string page, int inNamespace = -1)
    {
        return QueryPages("transcludedin", new Dictionary<string, string>
        {
            { "tinamespace", inNamespace != -1 ? inNamespace.ToString() : null },
            { "tiprop", "title" },
            { "tilimit", "500" },
        }, page)[page].Item2.Select(x => x.Value<string>("title")).ToArray();
    }

    public RevisionInfo[] GetHistory(string page, DateTimeOffset from)
    {
        var revisions = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "ids|timestamp|size|flags" },
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

        var res = QueryPages("revisions", new Dictionary<string, string>
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
        return QueryPages("revisions", new Dictionary<string, string> {
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
            { "token", GetEditToken() },
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

        var pars = new JObject(
            from par in result.Value<JObject>("params").Values()
            let parts = par.Value<string>().Split(new[] { '=' }, 2)
            select new JProperty(parts[0], parts[1])
        );

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

    private IDictionary<string, Tuple<string, JArray>> QueryPages(string property, IDictionary<string, string> queryArgs, params string[] titles)
    {
        queryArgs = new Dictionary<string, string>(queryArgs)
        {
            { "titles", JoinList(titles) },
        };

        var mergeSettings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };
        var normalizations = new JObject(titles.Select(t => new JProperty(t, t)));

        var result = QueryPages(property, queryArgs);
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
                if (x["missing"] != null)
                    return null;

                return Tuple.Create(
                    x.Value<string>("title"),
                    x.Value<JArray>(property) ?? new JArray()
                );
            });
    }

    private JObject QueryPages(string property, IDictionary<string, string> queryArgs, int? limit = null)
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

        foreach (var res in query)
        {
            var pages = res == null ? null : res.Property("pages");
            var resObject = new JObject { pages ?? new JProperty("pages", new JObject()) };

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

    private JToken GetTokens(params string[] types)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "tokens" },
        };

        if (types.Any())
            args.Add("type", JoinList(types));

        return Exec(args)["tokens"];
    }

    private string _editToken;
    private string GetEditToken()
    {
        if (_editToken == null)
        {
            var res = GetTokens();
            _editToken = res.Value<string>("edittoken");
            if (_editToken == null)
                throw new Exception(res.ToString());
        }

        return _editToken;
    }

    public void Edit(string page, string contents, string summary, bool? append = null)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "edit" },
            { "title", page },
            { !append.HasValue ? "text" : append.Value ? "appendtext" : "prependtext", contents },
            { "summary", summary },
            { "token", GetEditToken() },
            { "bot", "" },
        };

        var result = ExecWrite(args);

        var code = result["edit"].Value<string>("result");
        if (code == null)
            throw new MediaWikiException("Invalid response: " + result);
        if (code != "Success")
            throw new MediaWikiException(code);
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

    private JToken Exec(Dictionary<string, string> args)
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

    private JToken DoExec(Dictionary<string, string> args)
    {
        args["format"] = "json";
        var result = JToken.Parse(_browser.Post(_apiUri.AbsoluteUri, args));
        if (result["error"] != null)
            throw new MediaWikiApiException(result["error"].Value<string>("code"), result["error"].Value<string>("info"));
        return result;
    }

    [System.Diagnostics.DebuggerDisplay("{Size} {Timestamp}")]
    public class RevisionInfo
    {
        [JsonProperty("revid")]
        public int Id { get; set; }

        public int ParentId { get; set; }

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
        if (Enum.TryParse<MediaWiki.ErrorCode>(code, true, out ec))
            Code = ec;
    }

    protected MediaWikiApiException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
}
