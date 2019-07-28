using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace DailyBingChallengeBot.Models
{
    public class DailyBingTeam: CustomSerializationTableEntity
    {
        public string ServiceUrl { get; set; }
        public string TeamId { get; set; }
        public string TenantId { get; set; }
        public string InstallerName { get; set; }
        public string BotId { get; set; }
    }
}
