using DailyBingChallengeBot.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DailyBingChallengeBot.Core.Services
{
    public class BingImageService
    {
        public BingImageService()
        {

        }

        public string getImageCodeById(int id)
        {
            switch (id.ToString())
            {
                case "0":
                    return "en-UK";
                case "1":
                    return "de-DE";
                case "2":
                    return "en-AU";
                case "3":
                    return "en-CA";
                case "4":
                    return "en-NZ";
                case "5":
                    return "en-US";
                case "6":
                    return "ja-JP";
                case "7":
                    return "zh-CN";
                default:
                    return "en-UK";
            }
        }

        public DailyBingImage GetBingImageUrl(int id)
        {
            return GetBingImageUrl(getImageCodeById(id));
        }

        public DailyBingImage GetBingImageUrl(string locationCode)
        {

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=" + locationCode).Result;
            string responseText = response.Content.ReadAsStringAsync().Result;
            dynamic bingImageResponse = JObject.Parse(responseText);

            var cultInfo = System.Globalization.CultureInfo.GetCultureInfo(locationCode);
            DailyBingImage bingImage = new DailyBingImage()
            {
                Url = "https://www.bing.com" + bingImageResponse.images[0].url,
                ImageText = bingImageResponse.images[0].copyright,
                ImageRegion = cultInfo.DisplayName
            };
            /*
            var imageUrl = "";
            var imageText = "";
            switch (locationCode)
            {
                case "en-UK":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-5.jpg";
                    imageText = "UK";
                    break;
                case "de-DE":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-1.jpg";
                    imageText = "Deutsch";
                    break;
                case "en-AU":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-3.jpg";
                    imageText = "Aus";
                    break;
                case "en-CA":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-4.jpg";
                    imageText = "Aboot";
                    break;
                case "en-NZ":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-6.jpg";
                    imageText = "Kiwi";
                    break;
                case "en-US":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-7.jpg";
                    imageText = "UK";
                    break;
                case "ja-JP":
                    imageUrl = "https://www.bing.com/az/hprichbg/rb/PulauWayagIslands_EN-GB12253313122_1920x1080.jpg";
                    imageText = "UK";
                    break;
                case "zh-CN":
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-2.jpg";
                    imageText = "Test China";
                    break;
                default:
                    imageUrl = "http://i1-news.softpedia-static.com/images/news2/New-Best-of-Bing-Themes-for-Windows-7-China-2-and-Australia-3-5.jpg";
                    imageText = "UK";
                    break;
            }

            var cultInfo = System.Globalization.CultureInfo.GetCultureInfo(locationCode);
            DailyBingImage bingImage = new DailyBingImage()
            {
                Url = imageUrl,
                ImageText = imageText,
                ImageRegion = cultInfo.DisplayName
            };
            */
            return bingImage;
        }
    }
}