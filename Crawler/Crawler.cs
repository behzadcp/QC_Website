using GC_Website.Common;
using Microsoft.Playwright;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace GC_Website.Crawler
{

    public sealed record DeviceProfile(
        string Name,
        int Width,
        int Height,
        bool IsMobile = false,
        bool HasTouch = false,
        float DeviceScaleFactor = 1);

    public sealed class WebsiteCrawler : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;
        private IBrowser? _browser;
        private IPage? _page;

        private string? _url;
        private string _outputRoot = @"D:\website";

        public Common.BrowserType Browser { get; set; } = Common.BrowserType.Chrome;

        public IReadOnlyList<DeviceProfile> Devices => _devices.AsReadOnly();

        private readonly List<DeviceProfile> _devices = new();

        public WebsiteCrawler()
        {
            _playwright = Playwright.CreateAsync()
                .GetAwaiter()
                .GetResult();
        }

        // Set the website URL.
        public void SetHtmlpath(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    "A valid HTTP or HTTPS URL is required.", nameof(url));
            }

            _url = uri.AbsoluteUri;
        }

        // Optional: change the output root directory.
        public void SetOutputDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Output path is required.");

            _outputRoot = path;
        }

        // Add a predefined device or a custom viewport.
        public void SetDeviceType(string deviceName)
        {
            var device = GetDevice(deviceName);

            if (!_devices.Any(d => d.Name == device.Name))
                _devices.Add(device);
        }

        public void SetDeviceType(
            string name,
            int width,
            int height,
            float deviceScaleFactor = 1,
            bool isMobile = false,
            bool hasTouch = false)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(width), "Resolution must be positive.");

            if (deviceScaleFactor <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(deviceScaleFactor));

            var device = new DeviceProfile(
                name, width, height, isMobile, hasTouch,
                deviceScaleFactor);

            if (!_devices.Any(d => d.Name == device.Name))
                _devices.Add(device);
        }

        private static DeviceProfile GetDevice(string name)
        {
            return name.Trim().ToLowerInvariant() switch
            {
                "iphone 15" or "iphone 16" =>
                    new(name, 393, 852, true, true, 3),

                "iphone 15 pro" or "iphone 16 pro" =>
                    new(name, 402, 874, true, true, 3),

                "iphone se" =>
                    new(name, 375, 667, true, true, 2),

                "samsung galaxy s24" =>
                    new(name, 360, 780, true, true, 3),

                "samsung galaxy s24 ultra" =>
                    new(name, 384, 824, true, true, 3),

                "samsung galaxy a54" =>
                    new(name, 360, 800, true, true, 3),

                "ipad" or "ipad portrait" =>
                    new(name, 768, 1024, true, true, 2),

                "ipad landscape" =>
                    new(name, 1024, 768, true, true, 2),

                "desktop hd" =>
                    new(name, 1366, 768),

                "desktop full hd" =>
                    new(name, 1920, 1080),

                "desktop 4k" =>
                    new(name, 3840, 2160),

                _ => throw new ArgumentException(
                    $"Unknown device '{name}'. Use a built-in profile " +
                    "or configure a custom resolution.")
            };
        }

        private async Task EnsureBrowserAsync()
        {
            if (_browser is not null)
                return;

            var options = new BrowserTypeLaunchOptions
            {
                Headless = true,
            };

            switch (Browser)
            {
                case Common.BrowserType.Edge:
                    options.Channel = "msedge";
                    _browser = await _playwright.Chromium
                        .LaunchAsync(options);
                    break;

                case Common.BrowserType.Chrome:
                    options.Channel = "chrome";
                    _browser = await _playwright.Chromium
                        .LaunchAsync(options);
                    break;

                case Common.BrowserType.Safari:
                    _browser = await _playwright.Webkit
                        .LaunchAsync(options);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EnsureUrl()
        {
            if (string.IsNullOrWhiteSpace(_url))
                throw new InvalidOperationException(
                    "Call SetHtmlpath(url) before navigating.");
        }

        private async Task<IPage> OpenPageAsync(DeviceProfile device)
        {
            EnsureUrl();
            await EnsureBrowserAsync();

            if (_page is not null)
                await _page.CloseAsync();

            var context = await _browser!.NewContextAsync(
                new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize
                    {
                        Width = device.Width,
                        Height = device.Height
                    },
                    DeviceScaleFactor = device.DeviceScaleFactor,
                    IsMobile = device.IsMobile,
                    HasTouch = device.HasTouch
                });

            _page = await context.NewPageAsync();



            // 2. Log Unhandled Page Exceptions / Script Errors
            _page.PageError += (_, exceptionText) =>
            {
                Console.WriteLine($"[Page Error]: {exceptionText}");
            };


            await _page.GotoAsync(_url!, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = 60000,

            });


            return _page;
        }

        // Return all links found in anchor elements.
        public async Task<IReadOnlyList<string>> GetAllLinksAsync(
            string? deviceName = null)
        {
            EnsureUrl();

            var device = deviceName is null
                ? new DeviceProfile("Desktop", 1366, 768)
                : GetDevice(deviceName);

            var page = await OpenPageAsync(device);

            var links = await page.Locator("a[href]")
                .EvaluateAllAsync<string[]>(
                    "elements => elements.map(a => a.href)");

            return links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        
        public async Task<List<ScreenShotModel>> TakesScreenShotsAsync(
            bool fullPage = true)
        {
            EnsureUrl();

            if (_devices.Count == 0)
            {
                SetDeviceType("Desktop Full HD");
            }

            var uri = new Uri(_url!);
            var siteName = Sanitize(uri.Host);
            var siteDirectory = Path.Combine(_outputRoot, siteName);        

            var savedFiles = new List<ScreenShotModel>();
            var counter= 0;
            foreach (var device in _devices)
            {
                var page = await OpenPageAsync(device);
                var imagesDirectory = Path.Combine(siteDirectory, device.Name);
                Directory.CreateDirectory(imagesDirectory);

                int totalPageHeight = await page.EvaluateAsync<int>("() => document.documentElement.scrollHeight");
                for (int i = 0; i < totalPageHeight; i += device.Height)
                {                                                    

                    await page.WaitForTimeoutAsync(5000);

                    var fileName =
                        $"{Sanitize(device.Name)}_" +
                        $"{device.Width}x{i}.png";

                    var filePath = Path.Combine(imagesDirectory, fileName);

                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = filePath,
                        FullPage = fullPage,
                        Animations = ScreenshotAnimations.Disabled,
                    });

                    savedFiles.Add(new ScreenShotModel
                    {
                        FilePath = filePath,
                        PageHeight = device.Height,
                        PageNumber = counter++
                    });

                    await page.EvaluateAsync(
                          "window.scrollBy(0, window.innerHeight)");

                    await page.WaitForTimeoutAsync(300);
                }

                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(imagesDirectory, $"{Sanitize(device.Name)}_FullScreen.png"),
                    FullPage = true,
                    Animations = ScreenshotAnimations.Disabled,
                });

                savedFiles.Add(new ScreenShotModel
                {
                    FilePath = Path.Combine(imagesDirectory, $"{Sanitize(device.Name)}_FullScreen.png"),
                    PageHeight = 0,
                    PageNumber = counter++
                });
                await page.Context.CloseAsync();
                _page = null;

            }

            return savedFiles;
        }


        private static string Sanitize(string value)
        {
            var invalid = Regex.Escape(
                new string(Path.GetInvalidFileNameChars()));

            var result = Regex.Replace(
                value, $"[{invalid}]", "_");

            result = Regex.Replace(result, @"[^\w.-]", "_");

            return string.IsNullOrWhiteSpace(result)
                ? "website"
                : result;
        }

        public async ValueTask DisposeAsync()
        {
            if (_page is not null)
                await _page.Context.CloseAsync();

            if (_browser is not null)
                await _browser.DisposeAsync();

            _playwright.Dispose();
        }
    }
}