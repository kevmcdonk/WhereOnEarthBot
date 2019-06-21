using System;
using Microsoft.Bot.Builder.Dialogs;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Graph;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DailyBingChallengeBot.Core.Models;

namespace DailyBingChallengeBot.Core.Services
{
    public class GraphService
    {
        private string _siteId;
        private string _bingEntryList;
        private string _bingUserList;
        private string _bingsList;

        public GraphService(string siteId, string bingEntryList, string bingUserList, string bingsList)
        {
            _siteId = siteId;
            _bingEntryList = bingEntryList;
            _bingUserList = bingUserList;
            _bingsList = bingsList;
        }

        public async Task SaveBingUserIfNotExists(string accessToken, string DailyBingUserId, string DailyBingUserName)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/v1.0",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    List<QueryOption> options = new List<QueryOption>
                    {
                        new QueryOption("DailyBingUserId", DailyBingUserId)
                    };
                    var listResults = await graphClient.Sites[_siteId].Lists[_bingEntryList].Items.Request(options).GetAsync();
                    if (listResults.Count < 1)
                    {
                        await SaveBingUser(accessToken, new DailyBingUser() { id = DailyBingUserId, username = DailyBingUserName });
                    }
                }

            }
            catch (Exception exp)
            {
                Console.Write("Hmmm");
            }
        }

   


        public async Task SaveBingEntry(string accessToken, string response, string longitude, string latitude, double distanceFromAnswer, string userName)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                string listItemsUrl = string.Format("https://graph.microsoft.com/beta/sites/{0}/lists/{1}/items", _siteId, _bingEntryList);
                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/v1.0",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    FieldValueSet valueSet = new FieldValueSet();
                    valueSet.AdditionalData = new Dictionary<string, object>();
                    valueSet.AdditionalData.Add("Title", "");
                    valueSet.AdditionalData.Add("BingResponse", response);
                    valueSet.AdditionalData.Add("BingDate", DateTime.Now);
                    valueSet.AdditionalData.Add("Longitude", longitude);
                    valueSet.AdditionalData.Add("Latitude", latitude);
                    valueSet.AdditionalData.Add("DistanceFromAnswer", distanceFromAnswer);
                    valueSet.AdditionalData.Add("BingResponder", userName);

                    ListItem itemAdded = await graphClient.Sites[_siteId].Lists[_bingEntryList].Items.Request().AddAsync(new ListItem()
                    {
                        Description = "Hello",
                        Fields = valueSet
                    });

                }

            }
            catch (Exception exp)
            {
                Console.Write("Hmmm");
            }
        }

        public async Task SaveBingUser(string accessToken, DailyBingUser user)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                string listItemsUrl = string.Format("https://graph.microsoft.com/beta/sites/{0}/lists/{1}/items", _siteId, _bingEntryList);
                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/v1.0",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    FieldValueSet valueSet = new FieldValueSet();
                    valueSet.AdditionalData = new Dictionary<string, object>();
                    valueSet.AdditionalData.Add("Title", "");
                    valueSet.AdditionalData.Add("DailyBingUserId", user.id);
                    valueSet.AdditionalData.Add("DailyBingUserName", user.username);

                    ListItem itemAdded = await graphClient.Sites[_siteId].Lists[_bingEntryList].Items.Request().AddAsync(new ListItem()
                    {
                        Description = "Hello",
                        Fields = valueSet
                    });

                }

            }
            catch (Exception exp)
            {
                Console.Write("Hmmm");
            }
        }

        public async Task<List<DailyBingUser>> GetBingUsers(string accessToken)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                string listItemsUrl = string.Format("https://graph.microsoft.com/beta/sites/{0}/lists/{1}/items", _siteId, _bingEntryList);
                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/v1.0",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    IListItemsCollectionPage userItems = await graphClient.Sites[_siteId].Lists[_bingUserList]
                        .Items
                        .Request()
                        .Expand("Fields")
                        .Select("*")
                        .GetAsync();

                    List<DailyBingUser> users = new List<DailyBingUser>();
                    foreach (var userItem in userItems)
                    {
                        DailyBingUser user = new DailyBingUser()
                        {
                            id = userItem.Id,
                            username = userItem.Fields.AdditionalData["DailyBingUserName"].ToString()
                        };
                        users.Add(user);
                    }
                    return users;
                }
                return null;
            }
            catch (Exception exp)
            {
                // TODO: do something with this exception darn it!
                Console.Write("Hmmm");
                return null;
            }
        }

        public async Task<string> GetUsername(string accessToken)
        {
            GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/beta",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

            if (graphClient != null)
            {
                Microsoft.Graph.User me = await graphClient.Me.Request().GetAsync();

                return me.UserPrincipalName;
            }
            return null;
        }

        public async Task<List<DailyBingEntry>> GetBingEntriesByDate(string accessToken, DateTime entryDate)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/beta",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    IListItemsCollectionPage entryItems = await graphClient.Sites[_siteId].Lists[_bingEntryList].Items
                        .Request()
                        .Expand("Fields")/*.Filter(string.Format("BingDate ge '{0}'", entryDate.ToString("yyyy-MM-dd")))*/
                        .Select("*")
                        .GetAsync();
                    List<DailyBingEntry> entries = new List<DailyBingEntry>();
                    foreach (var entryItem in entryItems)
                    {
                        DailyBingEntry entry = new DailyBingEntry()
                        {
                            id = entryItem.Id,
                            userName = entryItem.Fields.AdditionalData["BingResponder"].ToString(),
                            BingResponse = entryItem.Fields.AdditionalData["BingResponse"].ToString(),
                            distanceFrom = float.Parse(entryItem.Fields.AdditionalData["DistanceFromAnswer"].ToString())
                        };
                        entries.Add(entry);
                    }
                    return entries;
                }
                return null;
            }
            catch (Exception exp)
            {
                Console.Write("Hmmm");
                return null;
            }
        }

        public async Task SaveDailyBing(string accessToken, string imageText, DateTime imageDate, string imageUrl, string imageLocation, string imageLatitude, string imageLongitude)
        {
            // Create HTTP Client and get the response.
            var httpClient = new HttpClient();

            try
            {
                // DailyBingEntryListId

                string listItemsUrl = string.Format("https://graph.microsoft.com/beta/sites/{0}/lists/{1}/items", _siteId, _bingsList);
                GraphServiceClient graphClient = new GraphServiceClient(
                   "https://graph.microsoft.com/v1.0",
                   new DelegateAuthenticationProvider(
                       async (requestMessage) =>
                       {
                           requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                       }));

                if (graphClient != null)
                {
                    FieldValueSet valueSet = new FieldValueSet();
                    valueSet.AdditionalData = new Dictionary<string, object>();
                    valueSet.AdditionalData.Add("Title", imageText);
                    valueSet.AdditionalData.Add("BingDate", imageDate.ToLongTimeString());
                    valueSet.AdditionalData.Add("BingImageUrl", imageUrl);
                    valueSet.AdditionalData.Add("LocationText", imageLocation);
                    valueSet.AdditionalData.Add("Longitude", imageLongitude);
                    valueSet.AdditionalData.Add("Latitude", imageLatitude);


                    ListItem itemAdded = await graphClient.Sites[_siteId].Lists[_bingsList].Items.Request().AddAsync(new ListItem()
                    {
                        Description = "Hello",
                        Fields = valueSet
                    });

                }

            }
            catch (Exception exp)
            {
                Console.Write("Hmmm");
            }
        }

    }
}