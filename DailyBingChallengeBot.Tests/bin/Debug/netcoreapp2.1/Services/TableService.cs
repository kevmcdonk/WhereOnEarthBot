using System;
using System.Collections.Generic;
using System.Text;
using DailyBingChallengeBot.Models;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Bot.Schema.Teams;

namespace DailyBingChallengeBot.Services
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
                    "connection string as a value.");
            }
        }

        public async Task<DailyBing> GetDailyBing()
        {
            
            string rowKey = DateTime.Now.ToString("yyyyMMdd");
            string partitionKey = typeof(DailyBing).ToString();

            TableOperation retrieveOperation = TableOperation.Retrieve<DailyBing>(partitionKey, rowKey);
            TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
            DailyBing dailyBing = result.Result as DailyBing;
            if (dailyBing == null)
            {
                dailyBing = new DailyBing()
                {
                    RowKey = rowKey,
                    PartitionKey = partitionKey,
                    entries = new List<DailyBingEntry>(),
                    publishedTime = DateTime.Now,
                    resultSet = false
                };
                await SaveDailyBing(dailyBing);
            }
            if (dailyBing.entries == null)
            {
                if (dailyBing.SerializedEntries == null)
                {
                    dailyBing.entries = new List<DailyBingEntry>();
                }
                else
                {
                    dailyBing.entries = JsonConvert.DeserializeObject< List<DailyBingEntry>>(dailyBing.SerializedEntries);
                }
            }
            if (dailyBing.publishedTime == null)
            {
                dailyBing.publishedTime = DateTime.Now;
            }
            if (dailyBing.serializableCurrentStatus != null)
            {
                dailyBing.currentStatus = (DailyBingStatus)Enum.Parse(typeof(DailyBingStatus), dailyBing.serializableCurrentStatus);
            }
            return dailyBing;
        }

        public async Task SaveDailyBing(DailyBing dailyBing)
        {
            dailyBing.PartitionKey = typeof(DailyBing).ToString();
            dailyBing.RowKey = DateTime.Now.ToString("yyyyMMdd");
            if (dailyBing.entries == null)
            {
                dailyBing.entries = new List<DailyBingEntry>();
            }
            if (dailyBing.publishedTime == null)
            {
                dailyBing.publishedTime = DateTime.Now;
            }
            dailyBing.SerializedEntries = JsonConvert.SerializeObject(dailyBing.entries);
            dailyBing.serializableCurrentStatus = dailyBing.currentStatus.ToString();
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(dailyBing);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyBingInfo> GetLatestInfo()
        {
            string rowKey = "DailyBingInfo";
            string partitionKey = typeof(DailyBingInfo).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyBingInfo>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyBingInfo info = result.Result as DailyBingInfo;
                if (info== null)
                {
                    List<DailyBingUser> basicUsers = new List<DailyBingUser>();
                    basicUsers.Add(new DailyBingUser()
                    {
                        id = "1",
                        username = "Admin"
                    });
                    info = new DailyBingInfo()
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        currentImageIndex = 0,
                        currentSource = ImageSource.Bing,
                        users = new List<DailyBingUser>()
                    };
                    await SaveLatestInfo(info);
                    if (info.users == null)
                    {
                        if (info.SerializedUsers == null)
                        {
                            info.users = new List<DailyBingUser>();
                        }
                        else
                        {
                            info.users = JsonConvert.DeserializeObject<List<DailyBingUser>>(info.SerializedUsers);
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

        public async Task SaveLatestInfo(DailyBingInfo info)
        {
            if (info.users == null)
            {
                info.users = new List<DailyBingUser>();
            }
            info.SerializedUsers = JsonConvert.SerializeObject(info.users);

            info.serializableImageSource = info.currentSource.ToString();
            info.PartitionKey = typeof(DailyBingInfo).ToString();
            info.RowKey = "DailyBingInfo";
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(info);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task SaveDailyBingImage(DailyBingImage image)
        {
            image.PartitionKey = typeof(DailyBingImage).ToString();
            image.RowKey = DateTime.Now.ToString("yyyyMMdd");
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(image);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyBingImage> getDailyBingImage()
        {
            string rowKey = DateTime.Now.ToString("yyyyMMdd");
            string partitionKey = typeof(DailyBingImage).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyBingImage>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyBingImage image = result.Result as DailyBingImage;
                if (image == null)
                {
                    image = new DailyBingImage()
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

        public async Task SaveDailyBingTeamInfo(DailyBingTeam team)
        {
            team.PartitionKey = typeof(DailyBingTeam).ToString();
            team.RowKey = "DailyBingTeam";
            team.ChannelDataSerialized = JsonConvert.SerializeObject(team.ChannelData);
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(team);

            // Execute the operation.
            TableResult result = await cloudTable.ExecuteAsync(insertOrMergeOperation);
        }

        public async Task<DailyBingTeam> getDailyBingTeamInfo()
        {
            string rowKey = "DailyBingTeam";
            string partitionKey = typeof(DailyBingTeam).ToString();

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<DailyBingTeam>(partitionKey, rowKey);
                TableResult result = await cloudTable.ExecuteAsync(retrieveOperation);
                DailyBingTeam team = result.Result as DailyBingTeam;
                if (team == null)
                {
                    team = new DailyBingTeam()
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
