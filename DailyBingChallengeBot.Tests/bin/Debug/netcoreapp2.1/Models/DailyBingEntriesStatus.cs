using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DailyBingChallengeBot.Models
{
    public class DailyBingEntriesStatus
    {
        public bool allResultsReceived { get; set; }
        public int usersWithEntryCount { get; set; }
        public int userCount { get; set; }
    }
}