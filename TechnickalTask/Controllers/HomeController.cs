using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TechnickalTask.Responses;

namespace TechnickalTask.Controllers
{
    public class HomeController : Controller
    {
        private static IMemoryCache _cache;

        public HomeController(IMemoryCache memCache)
        {
            _cache = memCache;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
         
        [HttpPost]
        public async Task<IActionResult> IndexAsync(string urls)
        {
            var response = new Response();
            if (string.IsNullOrWhiteSpace(urls))
            {
                response.Error = "String is null";
                return View(response);
            }

            var urlsArray = urls.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );

            var downloadTasksQuery =
                from url in urlsArray
                select CheckUrlAsync(url);

            var downloadTasks = downloadTasksQuery.ToList();

            while (downloadTasks.Any())
            {
                var finishedTask = await Task.WhenAny(downloadTasks);
                downloadTasks.Remove(finishedTask);
                response.UrlResponses.Add(await finishedTask);
            }

            return View(response);
        }

        private static async Task<UrlResponse> CheckUrlAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return new UrlResponse
                {
                    Error = "Url is null",
                };

            if (_cache.TryGetValue(url, out UrlResponse page))
            {
                return page;
            }

            using var client = new WebClient();
            try
            {
                var stream = client.OpenRead(url);
                var streamReader = new StreamReader(stream!, System.Text.Encoding.GetEncoding("UTF-8"));

                var pageHtml = await streamReader.ReadToEndAsync();

                if (!string.IsNullOrEmpty(pageHtml))
                {
                    var title = Regex.Match(pageHtml, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>", RegexOptions.IgnoreCase).Groups["Title"].Value;

                    if (!string.IsNullOrEmpty(title))
                    {
                        if (title.Contains("|"))
                            title = title.Split("|").First();
                        else if (title.Contains(":"))
                            title = title.Split(":").First();

                        var result = new UrlResponse()
                        {
                            Title = title,
                            Url = url,
                            StatusCode = 200,
                        };

                        _cache.Set(url, result, DateTimeOffset.Now.AddHours(12));

                        page = result;
                    }
                }

                // Cleanup.
                await stream.FlushAsync();
                stream.Close();
                client.Dispose();
            }
            catch (WebException e)
            {
                if (e.Response != null)
                    return new UrlResponse()
                    {
                        StatusCode = (int) ((HttpWebResponse) e.Response).StatusCode,
                        Error = e.Message,
                        Url = url
                    };

                return new UrlResponse()
                {
                    Url = url,
                    Error = e.Message,
                };
            }

            return page;

        }
    }
}
