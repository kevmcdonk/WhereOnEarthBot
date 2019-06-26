// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using DailyBingChallengeBot.Models;
using DailyBingChallengeBot.Services;
using System.Collections.Generic;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams.Models;
using System.Text;
using Microsoft.Bot.Builder.ApplicationInsights;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class BingGuesserDialog : ComponentDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;

        public BingGuesserDialog(string id, IConfiguration configuration, ILogger logger)
            : base(id)

        {
            Configuration = configuration;
            Logger = logger;
            
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBing dailyBing = await storageService.GetDailyBing();
            DailyBingInfo info = await storageService.GetLatestInfo();
            bool allresultsreceived = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyBing, info);

            if (allresultsreceived)
            {
                await CheckResults(stepContext, cancellationToken, dailyBing, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("What is your guess?") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            BingMapService mapService = new BingMapService();

            string guessText = stepContext.Result.ToString();
            DailyBingEntry entry = await mapService.GetLocationDetails(guessText);
            if (entry == null)
            {
                var locationSplit = stepContext.Result.ToString().Split(' ');
                if (locationSplit.Length > 1)
                {
                    var searchText = guessText.Substring(guessText.IndexOf(' '));
                    entry = await mapService.GetLocationDetails(searchText);
                }
            }

            if (entry == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Sorry, bing maps couldn't identify the location '{stepContext.Result.ToString()}'. Please try again."), cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(BingGuesserDialog), null, cancellationToken);
            }
            else
            {
                StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
                DailyBing dailyBing = await storageService.GetDailyBing();


                double distanceFromResult = (Math.Pow(entry.longitude - dailyBing.longitude, 2) + Math.Pow(entry.latitude - dailyBing.latitude, 2));
                entry.distanceFrom = distanceFromResult;
                entry.userName = stepContext.Context.Activity.From.Name;
                entry.userId = stepContext.Context.Activity.From.Id;
                dailyBing.entries.Add(entry);
                await storageService.SaveDailyBing(dailyBing);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Saving your guess as {entry.BingResponse}"), cancellationToken);
                DailyBingInfo info = await storageService.GetLatestInfo();
                bool allresultsreceived = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyBing, info);

                if (allresultsreceived)
                {
                    await CheckResults(stepContext, cancellationToken, dailyBing, info);
                    return await stepContext.EndDialogAsync(cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Still more results from users to come."), cancellationToken);
                    return await stepContext.BeginDialogAsync(nameof(BingGuesserDialog), null, cancellationToken);
                }
            }
        }

        private async Task<bool> CheckWhetherAllEntriesReceived(WaterfallStepContext stepContext, CancellationToken cancellationToken, DailyBing dailyBing, DailyBingInfo info)
        {
            try
            {
                // Fill in the "standard" properties for BotMessageReceived
                // and add our own property.
                Logger.LogInformation("Checking whether all entries received");

                
                List<DailyBingEntry> todayEntries = dailyBing.entries;
                if (info.users == null)
                {
                    info.users = new List<DailyBingUser>();
                }
                List<DailyBingUser> bingUsers = new List<DailyBingUser>();

                var microsoftAppId = Configuration["MicrosoftAppId"];
                var microsoftAppPassword = Configuration["MicrosoftAppPassword"];

                var connector = new ConnectorClient(new Uri(stepContext.Context.Activity.ServiceUrl), microsoftAppId, microsoftAppPassword);
                var response = await connector.Conversations.GetConversationMembersWithHttpMessagesAsync(stepContext.Context.Activity.Conversation.Id);
                //var response = (await connectorClient.Conversations.GetConversationMembersAsync());
                foreach (var user in response.Body)
                {
                    bingUsers.Add(new DailyBingUser()
                    {
                        id = user.Id,
                        username = user.Name
                    });
                }

                int userCount = bingUsers.Count;
                int usersWithEntryCount = 0;

                foreach (var user in bingUsers)
                {
                    if (todayEntries.Exists(matchingItem => matchingItem.userName == user.username))
                    {
                        usersWithEntryCount++;
                    }
                }
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Still more results from users to come - {usersWithEntryCount} users have entered out of the {userCount} in this channel."), cancellationToken);

                if (usersWithEntryCount >= userCount)
                {
                    return true;
                }

                //TODO: remove this for a user
                if (todayEntries.Count > userCount)
                {
                    return true;
                }
                return false;
            }
            catch(Exception exp)
            {
                Logger.LogError(exp, $"Error checking whether all entries received: {exp.Message} - {exp.StackTrace}", null);
                throw exp;
            }
        }

        private async Task CheckResults(WaterfallStepContext stepContext, CancellationToken cancellationToken, DailyBing dailyBing, DailyBingInfo info)
        {
           List<DailyBingEntry> todayEntries = dailyBing.entries;

            string currentWinningUser = "";
            string currentWinningEntry = "";
            double currentWinningDistance = double.MaxValue;

            foreach (var entry in todayEntries)
            {
                if (entry.distanceFrom < currentWinningDistance)
                {
                    currentWinningUser = entry.userName;
                    currentWinningEntry = entry.BingResponse;
                    currentWinningDistance = entry.distanceFrom;
                }
            }
            try
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"The winning result was {currentWinningEntry} from {currentWinningUser.ToString()} which was {currentWinningDistance.ToString("#.##")} km from the real answer of {dailyBing.extractedLocation}"), cancellationToken);
            }
            catch (Exception exp)
            {
                Console.WriteLine("Error checking results: " + exp.Message);
            }
            return;
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
