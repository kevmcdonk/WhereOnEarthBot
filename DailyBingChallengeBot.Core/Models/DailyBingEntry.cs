using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace DailyBingChallengeBot.Core.Models
{
    public class DailyBingEntry
    {
        public string id { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string BingResponse { get; set; }
        public float longitude { get; set; }
        public float latitude { get; set; }
        public double distanceFrom { get; set; }
        public DateTime BingDate { get; set; }
        public string fromId { get; set; }
        public string fromName { get; set; }
        public string serviceUrl { get; set; }
        public string channelId { get; set; }
        public string conversationId { get; set; }

        public DailyBingEntry()
        {

        }

        public async Task SaveEntry(string token)
        {
            HttpClient client = new HttpClient();

            //https://graph.microsoft.com/v1.0/sites/mcdonnell.sharepoint.com:/sites/thedailybing
            //"id": "mcdonnell.sharepoint.com,ebe470ba-7b38-4ae8-829b-046a89e1d390,40b90a36-eaf1-4e99-8b70-cdcad5118c2f",
            //GET
            //https://graph.microsoft.com/beta/sites/mcdonnell.sharepoint.com,ebe470ba-7b38-4ae8-829b-046a89e1d390,40b90a36-eaf1-4e99-8b70-cdcad5118c2f/lists/BingResponses
            //The Graph supports the following query options: $filter, $orderby, $expand, $top, and $format. The following query options are not currently supported: $count, $inlinecount, and $skip.
            //POST
            //https://graph.microsoft.com/beta/sites/mcdonnell.sharepoint.com,ebe470ba-7b38-4ae8-829b-046a89e1d390,40b90a36-eaf1-4e99-8b70-cdcad5118c2f/lists/BingResponses/items
            /*
             {
                  "fields": {
                    "Title": "Widget",
                    "Replier": "Purple",
                    "Response": "32"
                  }
                }
                */

            //search for the user
            /*
            var endpoint = String.Format("https://graph.microsoft.com/v1.0/groups?$filter=startswith(displayName,'{0}')%20or%20startswith(mail,'{0}')", searchPhrase);
            var json = await client.MSGraphGET(token, endpoint);
            groups = ((JArray)json["value"]).ToGroupList();

            return groups;
            */
            string requestUri = "https://graph.microsoft.com/beta/sites/mcdonnell.sharepoint.com,ebe470ba-7b38-4ae8-829b-046a89e1d390,40b90a36-eaf1-4e99-8b70-cdcad5118c2f/lists/BingResponses/items";
            // var response = await client.MSGraphPOST(token, requestUri, this);

        }
    }
}
