using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WhereOnEarthBot.Models
{
    public class DailyChallengeResult
    {
        public string id { get; set; }
        public string winnerGuess { get; set; }
        public string winnerName { get; set; }
        public DateTime publishedTime { get; set; }
        public double distanceToEntry { get; set; }
        public string actualLocation { get; set; }
        public string actualLocationText { get; set; }
        public string actualLocationImageUrl { get; set; }
    }
}