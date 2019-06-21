using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DailyBingChallengeBot.Core.Models
{
    public class DailyBing
    {
        public string id { get; set; }
        public string text { get; set; }
        public string photoUrl { get; set; }
        public string extractedLocation { get; set; }
        public float longitude { get; set; }
        public float latitude { get; set; }
        public DateTime publishedTime { get; set; }
        public List<DailyBingEntry> entries { get; set; }
        public DailyBingResult result { get; set; }

        public override string ToString()
        {
            return this.text;
        }
    }
}