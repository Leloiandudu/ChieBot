using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

public class MediaWiki
{
    private readonly Browser _browser;
    private readonly Uri _apiUri;

    public MediaWiki(Uri apiUri, string userAgent)
    {
        _browser = new Browser { UserAgent = userAgent };
        _apiUri = apiUri;
    }

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

    public string GetPage(string page)
    {
        return Query(new Dictionary<string, string>
        {
            { "prop", "revisions" },
            { "rvprop", "content" },
        }, page)[page].SelectToken("revisions[0].*").Value<string>();
    }

    private IDictionary<string, JToken> Query(IDictionary<string, string> queryArgs, params string[] titles)
    {
        var args = new Dictionary<string, string>
        {
            { "action", "query" },
            { "titles", JoinList(titles) },
        };

        foreach (var arg in queryArgs)
            args.Add(arg.Key, arg.Value);

        var res = Exec(args);

        return res["query"]["pages"].Values().ToDictionary(x => x.Value<string>("title"));
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
        Dump(args);
        var result = Exec(args);

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
