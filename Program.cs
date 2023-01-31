
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Razor.Generator;
using System.Xml.Schema;
using static System.Net.WebRequestMethods;

internal class Program
{
    
           
    private async static Task Main(string[] args)
    {
        string url = CheckMainUrl(Console.ReadLine());
        
        string siteName = GetSiteName(url);
        Console.WriteLine("Loading... ");
        List<string> urlsFromWebSite = (await GetUrlsFromWebSiteAsync(url)).Distinct().ToList();
        List<string> urlsFromSitemap = (await GetUrlsFromSiteMapAsync(url)).Distinct().ToList();

        

        List<string> uniqueUrlsFromWebSite = GetUniqueUrls(urlsFromWebSite, urlsFromSitemap);
        List<string> uniqueUrlsFromSitemap = GetUniqueUrls(urlsFromSitemap, urlsFromWebSite);

        Console.WriteLine("Urls FOUNDED IN SITEMAP.XML but not founded after crawling a web site");
        Console.WriteLine("Url");
        int counter = 1;
        foreach (var item in uniqueUrlsFromSitemap)
        {
            Console.WriteLine($"{counter} {item}");
            counter++;
        }
        

        Console.WriteLine();
        Console.WriteLine("Urls FOUNDED BY CRAWLING THE WEBSITE but not in sitemap.xml");
        Console.WriteLine("Url");
        counter = 1;
        foreach(var item in uniqueUrlsFromWebSite)
        {
            Console.WriteLine($"{counter} {item}");
            counter++;
        }
        

        Console.WriteLine("");
        Console.WriteLine("Timing");
        Console.WriteLine("Loading... ");
        List<UrlsWithTiming> urlsWithTimings = new List<UrlsWithTiming>();
        
        var allUrls = new List<string>();
        allUrls.Add(url);
        foreach (var item in urlsFromWebSite)
        {
            allUrls.Add(item);
        }
        foreach (var item in urlsFromSitemap)
        {
            if (allUrls.FirstOrDefault(x => x == item) == null)
            {
                allUrls.Add(item);
            }
        }
       
        foreach(var item in allUrls.Distinct().ToList())
        {
            var urlWithTiming = new UrlsWithTiming();
            urlWithTiming.Url = item;
            urlWithTiming.TimeTaken = await GetTimeResponseAsync(item);
            urlsWithTimings.Add(urlWithTiming);
        }
        
        int numberOdUrls = allUrls.Count;
        counter = 0;
        
        Console.WriteLine();
        counter = 1;
        foreach (var item in urlsWithTimings.Distinct().OrderBy(x => x.TimeTaken).ToList())
        {
            Console.WriteLine($"{counter} {item.Url}            {item.TimeTaken}");
            counter++;
        }
        
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
        
        var result = new List<string>();
        var Urls =  GetUrlsFromHtml(html, siteName);
        while (Urls.Count != 0)
        {
            var i = 0;
            var u = Urls[i];
            
            var htmlcode = await GetHtmlAsync(Urls[i]);
            var urlsList = GetUrlsFromHtml(htmlcode, siteName);
            urlsList.ForEach(x =>
            {
                if (!result.Contains(x) && !Urls.Contains(x))
                {

                    if (x.EndsWith("/"))
                    {
                        result.Add(x);
                    }
                    
                    else if (IsUrlFile(x) == false)
                    {
                        result.Add($"{x}/");
                    }
                }
            });
            
            if (Urls[i].EndsWith("/"))
            {
                result.Add(Urls[i]);
            }
            

            else if (!Urls[i].EndsWith("/") && IsUrlFile(Urls[i]) == false)
            {
                result.Add($"{Urls[i]}/");
            }

            Urls.Remove(Urls[i]);
        }
        
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
        
        var regex = new Regex("<a [^>]*href=(?:'(?<href>.*?)')|(?:\"(?<href>.*?)\")", RegexOptions.IgnoreCase);
        var contentFromHref = regex.Matches(htmlCode).OfType<Match>().Select(m => m.Groups["href"].Value).ToList();
        contentFromHref.ForEach(m =>
        {
            if ( (m.StartsWith("/") || m.StartsWith($"https://{siteName}/")) && IsUrlFile(m) == false && !m.Contains("#"))
            {
                
                if (m.StartsWith("/"))
                {
                    result.Add($"https://{siteName}{m}");
                }
                else
                {
                    result.Add(m);
                }
            }
        });
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
        
        var code = await GetHtmlAsync(url);
        var allUrls = new List<string>();
        var arrcode = code.Split("<url>").ToList();
        arrcode.ForEach(m =>
        {
            Match match = Regex.Match(m, @"<loc>(.*?)</loc>");
            var value = match.Groups[1].Value;
            if (value != "")
            {
                allUrls.Add(value);
            }
        });

        //
        var regex = new Regex("<loc>(.*?)</loc>", RegexOptions.IgnoreCase);
        var contentFromHref = regex.Matches(code).OfType<Match>().Select(m => m.Groups["href"].Value).ToList();
        allUrls.ForEach(m =>
        {
            if ((m.StartsWith("/") || m.StartsWith($"https://{siteName}/")) && (m.EndsWith(".xml") || IsUrlFile(m) == false))
            {
                
                if (m.StartsWith("/"))
                {
                    result.Add($"https://{siteName}{m}");
                }
                else
                {
                    result.Add(m);
                }
            }
        });
        //
        if (result.Count == 0)
        {
            var content = await GetHtmlAsync(url);
            var urls = GetUrlsFromHtml(content, siteName);
            if (urls.Count() != 0)
            {
                foreach (var item in urls)
                {
                    result.Add(item);
                }
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

    private static string CheckMainUrl(string url)
    {
        if (!url.EndsWith("/"))
        {
            return $"{url}/";
        }
        return url;
    }

    private static bool IsUrlFile(string url)
    {
        string extention = Path.GetExtension(url);
        if (extention == ".html" || extention == "")
        {
            return false;
        }
        else
        {
            return true;
        }
    }


}