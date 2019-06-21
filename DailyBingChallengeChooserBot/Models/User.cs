
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.Bot.Builder.Dialogs;

namespace DailyBingChallengeBot.Models
{
    [Serializable]
    public class User
    {
        public User() { }
        public string id { get; set; }
        public string text { get; set; }
        public bool isMe { get; set; }
        public string givenName { get; set; }
        public string surname { get; set; }
        public string jobTitle { get; set; }
        public string mail { get; set; }
        public string userPrincipalName { get; set; }
        public string mobilePhone { get; set; }
        public string officeLocation { get; set; }
    }
}
