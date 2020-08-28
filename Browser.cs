using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

public class Browser
{
    static Browser()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }

    public Browser()
    {
        Cookies = new CookieContainer();
    }

    public CookieContainer Cookies { get; set; }

    public string UserAgent { get; set; }

    public string Get(string url, Dictionary<string, string> args)
    {
        return GetStringResponse(GetRequest(string.Format("{0}?{1}", url, ArgsToString(args))));
    }

    public string Post(string url, IDictionary<string, string> args)
    {
        var request = GetRequest(url);
        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";

        var data = ArgsToString(args);
        using (var stream = request.GetRequestStream())
        {
            var buf = Encoding.ASCII.GetBytes(data);
            stream.Write(buf, 0, buf.Length);
        }

        return GetStringResponse(request);
    }

    private HttpWebRequest GetRequest(string url)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.CookieContainer = Cookies;
        request.UserAgent = UserAgent;
        request.ServicePoint.Expect100Continue = false;
        return request;
    }

    private string ArgsToString(IDictionary<string, string> args)
    {
        return string.Join("&", (
            from p in args
            where p.Value != null
            select string.Format("{0}={1}", Escape(p.Key), Escape(p.Value))
        ).ToArray());
    }

    private static string Escape(string str)
    {
        const int maxLength = 32766;
        var sb = new StringBuilder();
        for (int i = 0; i < str.Length; i += maxLength)
            sb.Append(Uri.EscapeDataString(str.Substring(i, Math.Min(str.Length - i, maxLength))));
        return sb.ToString();
    }

    private string GetStringResponse(HttpWebRequest request)
    {
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }
}
