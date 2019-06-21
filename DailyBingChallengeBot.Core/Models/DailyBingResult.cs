using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DailyBingChallengeBot.Core.Models
{
    public class DailyBingResult
    {
        public string id { get; set; }
        public string winnerId { get; set; }
        public string winnerName { get; set; }
        public DateTime publishedTime { get; set; }
        public float distanceToEntry { get; set; }

    }
}