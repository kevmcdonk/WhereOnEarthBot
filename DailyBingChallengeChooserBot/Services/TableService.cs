using System;
using System.Collections.Generic;
using System.Text;
using WhereOnEarthBot.Models;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Bot.Schema.Teams;

namespace WhereOnEarthBot.Services
{
    public class TableService
    {
        private CloudStorageAccount tableAccount;
        private CloudTable cloudTable;

        public TableService(string ConnectionString, string tableName)
        {
            // Check whether the connection string can be parsed.
            
            if (CloudStorageAccount.TryParse(ConnectionString, out tableAccount))
            {
                // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                CloudTableClient cloudBlobClient = tableAccount.CreateCloudTableClient();

                cloudTable = cloudBlobClient.GetTableReference(tableName);
                cloudTable.CreateIfNotExists();
            }
            else
            {
                throw new Exception(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add an environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value. Connection String - " + ConnectionString + ", TableName - " + tableName);
            }
        }

        public async Task<DailyChallenge> GetDailyChallenge()
        {
            
            string rowKey = DateTime.Now.ToString("yyyyMMdd");
            string partitionKey = typeof(DailyChallenge).ToString();

            TableOperation retrieveOperation = TableOperation.Retrieve<DailyChallenge>(partitionKey, rowKey);
            TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
            DailyChallenge dailyChallenge = result.Result as DailyChallenge;
            if (dailyChallenge == null)
            {
                dailyChallenge = new DailyChallenge()
                {
                    RowKey = rowKey,
                    PartitionKey = partitionKey,
                    entries = new List<DailyChallengeEntry>(),
                    publishedTime = DateTime.Now,
                    resultSet = false
                };
                await SaveDailyChallenge(dailyChallenge);
            }
            if (dailyChallenge.entries == null)
            {
                if (dailyChallenge.SerializedEntries == null)
                {
                    dailyChallenge.entries = new List<DailyChallengeEntry>();
                }
                else
                {
                    dailyChallenge.entries = JsonConvert.DeserializeObject< List<DailyChallengeEntry>>(dailyChallenge.SerializedEntries);
                }
            }
            if (dailyChallenge.publishedTime == null)
            {
                dailyChallenge.publishedTime = DateTime.Now;
            }
            if (dailyChallenge.serializableCurrentStatus != null)
            {
                dailyChallenge.currentStatus = (DailyChallengeStatus)Enum.Parse(typeof(DailyChallengeStatus), dailyChallenge.serializableCurrentStatus);
            }
            return dailyChallenge;
        }

        public async Task SaveDailyChallenge(DailyChallenge dailyChallenge)
        {
            dailyChallenge.PartitionKey = typeof(DailyChallenge).ToString();
            dailyChallenge.RowKey = DateTime.Now.ToString("yyyyMMdd");
            if (dailyChallenge.entries == null)
            {
                dailyChallenge.entries = new List<DailyChallengeEntry>();
            }
            if (dailyChallenge.publishedTime == null)
            {
                dailyChallenge.publishedTime = DateTime.Now;
            }
            dailyChallenge.SerializedEntries = JsonConvert.SerializeObject(dailyChallenge.entries);
            dailyChallenge.serializableCurrentStatus = dailyChallenge.currentStatus.ToString();
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(dailyChallenge);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyChallengeInfo> GetLatestInfo()
        {
            string rowKey = "DailyChallengeInfo";
            string partitionKey = typeof(DailyChallengeInfo).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyChallengeInfo>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyChallengeInfo info = result.Result as DailyChallengeInfo;
                if (info== null)
                {
                    List<DailyChallengeUser> basicUsers = new List<DailyChallengeUser>();
                    basicUsers.Add(new DailyChallengeUser()
                    {
                        id = "1",
                        username = "Admin"
                    });
                    info = new DailyChallengeInfo()
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        currentImageIndex = 0,
                        currentSource = ImageSource.Bing,
                        users = new List<DailyChallengeUser>()
                    };
                    await SaveLatestInfo(info);
                    if (info.users == null)
                    {
                        if (info.SerializedUsers == null)
                        {
                            info.users = new List<DailyChallengeUser>();
                        }
                        else
                        {
                            info.users = JsonConvert.DeserializeObject<List<DailyChallengeUser>>(info.SerializedUsers);
                        }
                    }
                }
                if (info.serializableImageSource != null)
                {
                    info.currentSource = (ImageSource)Enum.Parse(typeof(ImageSource), info.serializableImageSource);
                }
                return info;
            }
            catch (Exception exp)
            {
                //TODO: add some logging
                throw exp;
            }
        }

        public async Task SaveLatestInfo(DailyChallengeInfo info)
        {
            if (info.users == null)
            {
                info.users = new List<DailyChallengeUser>();
            }
            info.SerializedUsers = JsonConvert.SerializeObject(info.users);

            info.serializableImageSource = info.currentSource.ToString();
            info.PartitionKey = typeof(DailyChallengeInfo).ToString();
            info.RowKey = "DailyChallengeInfo";
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(info);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task SaveDailyChallengeImage(DailyChallengeImage image)
        {
            image.PartitionKey = typeof(DailyChallengeImage).ToString();
            image.RowKey = DateTime.Now.ToString("yyyyMMdd");
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(image);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyChallengeImage> getDailyChallengeImage()
        {
            string rowKey = DateTime.Now.ToString("yyyyMMdd");
            string partitionKey = typeof(DailyChallengeImage).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyChallengeImage>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyChallengeImage image = result.Result as DailyChallengeImage;
                if (image == null)
                {
                    image = new DailyChallengeImage()
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey
                    };
                }
                return image;
            }
            catch (Exception exp)
            {
                // TODO: add logging
                throw exp;
            }
        }

        public async Task SaveDailyChallengeTeamInfo(DailyChallengeTeam team)
        {
            team.PartitionKey = typeof(DailyChallengeTeam).ToString();
            team.RowKey = "DailyChallengeTeam";
            team.ChannelDataSerialized = JsonConvert.SerializeObject(team.ChannelData);
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(team);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyChallengeTeam> getDailyChallengeTeamInfo()
        {
            string rowKey = "DailyChallengeTeam";
            string partitionKey = typeof(DailyChallengeTeam).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyChallengeTeam>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyChallengeTeam team = result.Result as DailyChallengeTeam;
                if (team == null)
                {
                    team = new DailyChallengeTeam()
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey
                    };
                }

                if (!string.IsNullOrEmpty(team.ChannelDataSerialized))
                {
                    team.ChannelData = JsonConvert.DeserializeObject<TeamsChannelData>(team.ChannelDataSerialized);
                }
                return team;
            }
            catch (Exception exp)
            {
                // TODO: add logging
                throw exp;
            }
        }
    }
}
