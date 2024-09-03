using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;

public class Browser : HttpClient
{
    private readonly HttpClientHandler _handler;

    public Browser()
        : this(new HttpClientHandler())
    {
    }

    private Browser(HttpClientHandler handler)
        : base(handler)
    {
        _handler = handler;
    }

    public CookieContainer Cookies
    {
        get => _handler.CookieContainer;
        set => _handler.CookieContainer = value;
    }

    public string UserAgent
    {
        get => DefaultRequestHeaders.UserAgent.ToString();
        set
        {
            DefaultRequestHeaders.UserAgent.Clear();
            DefaultRequestHeaders.UserAgent.ParseAdd(value);
        }
    }

    public string Post(string url, IDictionary<string, string> args)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new FormUrlEncodedContent(args.Where(x => x.Value != null));
            using var resp = SendAsync(req).Result;
            return resp.EnsureSuccessStatusCode().Content.ReadAsStringAsync().Result;
        }
        catch (AggregateException aex)
        {
            if (aex.InnerExceptions.Count == 1)
            {
                // EDI preserves the original exception's stack trace
                ExceptionDispatchInfo.Capture(aex.InnerExceptions[0]).Throw();
            }

            throw;
        }
    }
}
