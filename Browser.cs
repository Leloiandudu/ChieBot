using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

class Browser
{
    private readonly CookieContainer _cookies = new CookieContainer();

    public string UserAgent { get; set; }

    public string Get(string url, Dictionary<string, string> args)
    {
        return GetStringResponse(GetRequest(string.Format("{0}?{1}", url, ArgsToString(args))));
    }

    public string Post(string url, Dictionary<string, string> args)
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
        request.CookieContainer = _cookies;
        request.UserAgent = UserAgent;
        request.ServicePoint.Expect100Continue = false;
        return request;
    }

    private string ArgsToString(Dictionary<string, string> args)
    {
        return string.Join("&", (
            from p in args
            where p.Value != null
            select string.Format("{0}={1}", Uri.EscapeDataString(p.Key), Uri.EscapeDataString(p.Value))
        ).ToArray());
    }

    private string GetStringResponse(HttpWebRequest request)
    {
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }
}
