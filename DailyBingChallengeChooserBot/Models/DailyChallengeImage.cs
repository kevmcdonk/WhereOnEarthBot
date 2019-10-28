using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WhereOnEarthBot.Models
{
    public class DailyChallengeImage : TableEntity
    {
        public string Url { get; set; }
        public string ImageText { get; set; }
        public string ImageRegion { get; set; }

        public float Longitutde { get; set; }
        public float Latitude { get; set; }
    }
}