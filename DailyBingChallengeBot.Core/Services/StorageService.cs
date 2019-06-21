using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;
using DailyBingChallengeBot.Core.Models;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DailyBingChallengeBot.Core.Services
{
    public class StorageService
    {
        private CloudStorageAccount storageAccount;
        private CloudBlobContainer cloudBlobContainer;

        public StorageService(string ConnectionString)
        {
            // Check whether the connection string can be parsed.
            
            if (CloudStorageAccount.TryParse(ConnectionString, out storageAccount))
            {
                // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                cloudBlobContainer = cloudBlobClient.GetContainerReference("dailybingdata");
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
            string blobName = "dailybing" + DateTime.Now.ToString("yyyyMMdd") + ".json";
            try
            {
                string blobText = await GetBlobContents(blobName);
                DailyBing dailyBing = JsonConvert.DeserializeObject<DailyBing>(blobText);
                return dailyBing;
            }
            catch(Exception exp)
            {
                if (exp.Message == "Blob does not exist")
                {
                    DailyBing newDailyBing = new DailyBing()
                    {
                        
                    };
                    await SaveBlob(newDailyBing, blobName);
                    return newDailyBing;
                }
                throw exp;
            }
        }

        public async Task<DailyBingInfo> GetLatestInfo()
        {
            string blobName = "DailyBingInfo.json";
            try
            {
                string blobText = await GetBlobContents(blobName);
                DailyBingInfo info = JsonConvert.DeserializeObject<DailyBingInfo>(blobText);
                return info;
            }
            catch (Exception exp)
            {
                if (exp.Message == "Blob does not exist")
                {
                    List<DailyBingUser> basicUsers = new List<DailyBingUser>();
                    basicUsers.Add(new DailyBingUser()
                    {
                        id = "1",
                        username = "Admin"
                    });
                    DailyBingInfo newInfo = new DailyBingInfo()
                    {
                        currentImageIndex = 0,
                        currentSource = ImageSource.Bing,
                        users = new List<DailyBingUser>()
                    };
                    await SaveBlob(newInfo, blobName);
                    return newInfo;
                }
                throw exp;
            }
        }

        public async Task SaveDailyBing(DailyBing dailyBing)
        {
            string blobName = "dailybing" + DateTime.Now.ToString("yyyyMMdd") + ".json";
            await SaveBlob(dailyBing, blobName);
        }

        public async Task SaveLatestInfo(DailyBingInfo info)
        {
            if (info.users == null)
            {
                info.users = new List<DailyBingUser>();
            }
            await SaveBlob(info, "DailyBingInfo.json");
        }

        private async Task SaveBlob(object blobContents, string blobName)
        {
            string blobText = JsonConvert.SerializeObject(blobContents);
            CloudBlockBlob blob = cloudBlobContainer.GetBlockBlobReference(blobName);
            await blob.UploadTextAsync(blobText);
        }

        private async Task<string> GetBlobContents(string blobName)
        {
            CloudBlockBlob blob = cloudBlobContainer.GetBlockBlobReference(blobName);
            bool blobExists = await blob.ExistsAsync();
            if (!blobExists)
            {
                throw new Exception("Blob does not exist");
            }
            string blobText = await blob.DownloadTextAsync();
            return blobText;
        }
    }
}
