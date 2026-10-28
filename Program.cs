using GC_Website.Common;
using GC_Website.Crawler;
using GC_Website.Detect;

await using var crawler = new WebsiteCrawler();

crawler.Browser = BrowserType.Chrome;

crawler.SetHtmlpath("https://www.takvim.com.tr/");

// Configure multiple viewport sizes.
//crawler.SetDeviceType("iPhone 16");
//crawler.SetDeviceType("Samsung Galaxy S24");
//crawler.SetDeviceType("iPad");
//crawler.SetDeviceType("Desktop Full HD");

// Add any custom resolution.
crawler.SetDeviceType(
    "Custom Desktop",
    width: 1600,
    height: 1200);



var screenshots = await crawler.TakesScreenShotsAsync(false);




var back = new BackGroundColorDetect();

var boxes= back.DetectBoxes(screenshots, @"D:\website\www.takvim.com.tr\Custom Desktop");

 
back.FindWrongs(boxes);



Console.WriteLine("\n========== SUMMARY ==========");

 Console.ReadLine();