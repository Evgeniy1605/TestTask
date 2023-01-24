
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Razor.Generator;
using static System.Net.WebRequestMethods;

internal class Program
{
    private const string UrlMaskWithQuotes = @"""http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?""";
    private const string UrlMask = @"http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
    private const string UrlMaskWithHref = $"href=({UrlMaskWithQuotes})";
    private async static Task Main(string[] args)
    {
        string url = Console.ReadLine();
        string siteName = GetSiteName(url);
        Console.WriteLine("Loading... (more then 10 seconds)");
        List<string> urlsFromWebSite = await GetUrlsFromWebSiteAsync(url);
        List<string> urlsFromSitemap = await GetUrlsFromSiteMapAsync(url);
        await Task.Delay(10000);

        List<string> uniqueUrlsFromWebSite = GetUniqueUrls(urlsFromWebSite, urlsFromSitemap);
        List<string> uniqueUrlsFromSitemap = GetUniqueUrls(urlsFromSitemap, uniqueUrlsFromWebSite);

        Console.WriteLine("Urls FOUNDED IN SITEMAP.XML but not founded after crawling a web site");
        Console.WriteLine("Url");
        int counter = 1;
        uniqueUrlsFromSitemap.ForEach(s =>
        {
            Console.WriteLine($"{counter} {s}");
            counter++;
        });

        Console.WriteLine();
        Console.WriteLine("Urls FOUNDED BY CRAWLING THE WEBSITE but not in sitemap.xml");
        Console.WriteLine("Url");
        counter = 1;
        uniqueUrlsFromWebSite.ForEach(s =>
        {
            Console.WriteLine($"{counter} {s}");
            counter++;
        });

        Console.WriteLine("");
        Console.WriteLine("Timing");
        Console.WriteLine("Loading...");
        Console.WriteLine("(The maximum time of loading is 15 seconds)");
        List<UrlsWithTiming> urlsWithTimings = new List<UrlsWithTiming>();
        urlsFromSitemap.ForEach(async s =>
        {
            var urlWithTiming = new UrlsWithTiming();
            urlWithTiming.Url = s;
            urlWithTiming.TimeTaken = await GetTimeResponseAsync(s);
            urlsWithTimings.Add(urlWithTiming);
        });

        urlsFromWebSite.ForEach(async s =>
        {
            var urlWithTiming = new UrlsWithTiming();
            urlWithTiming.Url = s;
            urlWithTiming.TimeTaken = await GetTimeResponseAsync(s);
            urlsWithTimings.Add(urlWithTiming);
        });
        int numberOdUrls = urlsFromSitemap.Count + urlsFromWebSite.Count;
        counter = 0;
        while (urlsWithTimings.Count <= numberOdUrls)
        {
            await Task.Delay(1000);
            counter++;
            if (counter == 15)
            {
                break;
            }
        }
        Console.WriteLine();
        counter = 1;
        urlsWithTimings.OrderBy(x => x.TimeTaken).ToList().ForEach(x =>
        {
            Console.WriteLine($"{counter} {x.Url}            {x.TimeTaken}");
            counter++;
        });
        Console.WriteLine();
        Console.WriteLine($"Urls(html documents) found after crawling a website: {urlsFromWebSite.Count} ");
        Console.WriteLine();
        Console.WriteLine($"Urls found in sitemap: {urlsFromSitemap.Count}");

        Console.ReadLine();

    }

    private async static Task< List<string>> GetUrlsFromWebSiteAsync(string url)
    {
 
        var html = await GetHtmlAsync(url);
        var siteName = GetSiteName(url);
        var newUrls = GetUrlsFromHtml(html, siteName);
        var result = new List<string>();
        
        newUrls.ForEach(async x =>
        {
            result.Add(x);
            
            var iHtml = await GetHtmlAsync(x);
            var i = GetUrlsFromHtml(iHtml, siteName);
            i.ForEach(i => result.Add(i));


        });
        return result;
    }
    private async static Task< string> GetHtmlAsync(string url)
    {
        try
        {
            string? htmlCode;
            using (WebClient client = new WebClient())
            {
                htmlCode = await client.DownloadStringTaskAsync(url); 
            }
            return htmlCode;
        }
        catch (Exception ex)
        {

            return ex.Message;
        }
    }

    public static List<string> GetUrlsFromHtml(string htmlCode, string siteName)
    {
        
        var result = new List<string>();
        StringBuilder urlsWithHref = new StringBuilder();
        
        var linkWithHrefParser = new Regex(UrlMaskWithHref, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        foreach (Match m in linkWithHrefParser.Matches(htmlCode))
            urlsWithHref.Append(m.Value);
        var linkParser = new Regex(UrlMask, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        foreach (Match m in linkParser.Matches(urlsWithHref.ToString()))
        {
            if (!result.Contains(m.Value) && !m.Value.Contains("js") && !m.Value.Contains("ajax") &&
                m.Value.StartsWith($"https://{siteName}/") && m.Value.EndsWith("/"))
            {
                result.Add(m.Value);
            }
        }
            
        return result;
    }
    private async static Task<List<string>> GetUrlsFromSiteMapAsync(string url)
    {
        string siteName = GetSiteName(url);
        HttpResponseMessage response;
        url = $"{url}/sitemap.xml";
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(url);
            response = await client.GetAsync(url);
        }
        List<string> result = new List<string>();
        var linkWithHrefParser = new Regex(UrlMask, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        foreach (Match m in linkWithHrefParser.Matches(response.Content.ReadAsStringAsync().Result))
        {
            if (!result.Contains(m.Value) && !m.Value.Contains("js") && !m.Value.Contains("ajax") &&
                m.Value.StartsWith($"https://{siteName}/"))
            {
                result.Add(m.Value);
            }
        }
            
        return result;
    }
    private static List<string> GetUniqueUrls(List<string> target, List<string> sours)
    {
        var result = new List<string>();
        target.ForEach(x =>
        {
            var i = sours.FirstOrDefault(s => s == x);
            if (i == null)
            {
                result.Add(x);
            }
        });

        return result;
    }

    private async static Task<string> GetTimeResponseAsync(string url)
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            System.Diagnostics.Stopwatch timer = new Stopwatch();
            timer.Start();
            
            var response = await request.GetResponseAsync();

            timer.Stop();

            TimeSpan timeTaken = timer.Elapsed;

            return $"{timeTaken.TotalMilliseconds.ToString()} ms";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        
        
    }

    private  class UrlsWithTiming
    {
        public string? Url { get; set; }
        public string? TimeTaken { get; set; }
    }

    private static string GetSiteName(string url)
    {
        
        Match match = Regex.Match(url, @"https://(.*?)/");
        return match.Groups[1].Value;
    }

 
}