using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace WhereOnEarthBot.Models
{
    public class DailyChallengeEntry
    {
        public string id { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string imageResponse { get; set; }
        public float longitude { get; set; }
        public float latitude { get; set; }
        public double distanceFrom { get; set; }
        public DateTime ChallengeDate { get; set; }
        public string fromId { get; set; }
        public string fromName { get; set; }
        public string serviceUrl { get; set; }
        public string channelId { get; set; }
        public string conversationId { get; set; }

        public DailyChallengeEntry()
        {

        }

    }
}
