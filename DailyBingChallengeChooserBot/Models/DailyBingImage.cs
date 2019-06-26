using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DailyBingChallengeBot.Models
{
    public class DailyBingImage
    {
        public string Url { get; set; }
        public string ImageText { get; set; }
        public string ImageRegion { get; set; }

        public float Longitutde { get; set; }
        public float Latitude { get; set; }
    }
}