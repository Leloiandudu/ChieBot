﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        }, page)[page];

        if (revisons == null)
            return null;
        return revisons[0].Value<string>("*");
    }

    public RevisionInfo[] GetHistory(string page, DateTimeOffset from)
    {
        var revisions = QueryPages("revisions", new Dictionary<string, string>
        {
            { "rvprop", "ids|timestamp|size|flags" },
            { "rvlimit", "5000" },
            { "rvdir", "newer" },
            { "redirects", "" },
            { "rvstart", from.UtcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK") },
        }, page)[page];

        if (revisions == null)
            return null;
        return revisions.ToObject<RevisionInfo[]>();
    }

    public RevisionInfo GetRevisionInfo(int revId)
    {
        return QueryPages("revisions", new Dictionary<string, string> {
            { "revids", revId.ToString() },
            { "rvprop", "ids|timestamp|size" },
            { "redirects", "" },
        })["pages"].Values().Single()["revisions"].Single().ToObject<RevisionInfo>();
    }

    private IDictionary<string, JArray> QueryPages(string property, IDictionary<string, string> queryArgs, params string[] titles)
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
                var values = x.Value<JArray>(property);
                if (values == null && x["missing"] == null)
                    values = new JArray();
                return values;
            });
    }

    private JObject QueryPages(string property, IDictionary<string, string> queryArgs)
    {
        queryArgs = new Dictionary<string, string>(queryArgs)
        {
            { "prop", property },
        }; 
        
        var mergeSettings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };
        var result = new JObject();

        foreach (var res in Query(queryArgs))
        {
            var resObject = new JObject()
            {
                res.Property("pages")
            };

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

        var code = result["edit"].Value<string>("result");
        if (code == null)
            throw new MediaWikiException("Invalid response: " + result);
        if (code != "Success")
            throw new MediaWikiException(code);
    }

    private JToken ExecFake(Dictionary<string, string> args)
    {
        Dump(args);
        return JObject.FromObject(new { edit = new { result = "Success" } });
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void Dump(Dictionary<string, string> args)
    {
        System.Diagnostics.Debug.WriteLine("Edit:");
        foreach (var arg in args)
        {
            System.Diagnostics.Debug.WriteLine("  {0} = {1}", arg.Key, arg.Value);
        }
        System.Diagnostics.Debug.WriteLine("");
    }

    private JToken Exec(Dictionary<string, string> args)
    {
        args.Add("format", "json");
        var result = JToken.Parse(_browser.Post(_apiUri.ToString(), args));
        if (result["error"] != null)
            throw new MediaWikiException(result["error"].Value<string>("info"));
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
