using WhereOnEarthBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WhereOnEarthBot.Services
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

        public DailyChallengeImage GetBingImageUrl(int id)
        {
            return GetBingImageUrl(getImageCodeById(id));
        }

        public DailyChallengeImage GetBingImageUrl(string locationCode)
        {

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=" + locationCode).Result;
            string responseText = response.Content.ReadAsStringAsync().Result;
            dynamic bingImageResponse = JObject.Parse(responseText);

            var cultInfo = System.Globalization.CultureInfo.GetCultureInfo(locationCode);
            DailyChallengeImage bingImage = new DailyChallengeImage()
            {
                Url = "https://www.bing.com" + bingImageResponse.images[0].url,
                ImageText = bingImageResponse.images[0].copyright,
                ImageRegion = cultInfo.DisplayName
            };

            return bingImage;
        }
    }
}