using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DailyBingChallengeBot.Models
{
    public enum DailyBingStatus
    {
        NotSet,
        Guessing,
        Completed
    }

    public class DailyBing : CustomSerializationTableEntity
    {
        public string id { get; set; }
        public string text { get; set; }
        public string photoUrl { get; set; }
        public string extractedLocation { get; set; }
        public double longitude { get; set; }
        public double latitude { get; set; }
        public DateTime publishedTime { get; set; }
        [NotSerialized]
        public List<DailyBingEntry> entries { get; set; }
        public string SerializedEntries { get; set; }
        
        public bool resultSet { get; set; }
        public string winnerGuess { get; set; }
        public string winnerName { get; set; }
        public double distanceToEntry { get; set; }
        public DailyBingStatus currentStatus { get; set; }
        public string serializableCurrentStatus { get; set; }
        public override string ToString()
        {
            return this.text;
        }
    }
}