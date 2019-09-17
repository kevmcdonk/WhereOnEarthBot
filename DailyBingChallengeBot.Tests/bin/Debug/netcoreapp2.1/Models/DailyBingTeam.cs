using Microsoft.Azure.Cosmos.Table;
using Microsoft.Bot.Schema.Teams;
using System;
using System.Collections.Generic;
using System.Text;

namespace DailyBingChallengeBot.Models
{
    public class DailyBingTeam: CustomSerializationTableEntity
    {
        public string ServiceUrl { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string TenantId { get; set; }
        public string InstallerName { get; set; }
        public string BotId { get; set; }
        public string ChannelId { get; set; }
        public TeamsChannelData ChannelData { get; set; }
        public string ChannelDataSerialized { get; set; }
    }
}
