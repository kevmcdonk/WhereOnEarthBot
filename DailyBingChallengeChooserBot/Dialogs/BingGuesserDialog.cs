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
using DailyBingChallengeBot.Helpers;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class BingGuesserDialog : ComponentDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;
        private TableService tableService;

        public BingGuesserDialog(string id, IConfiguration configuration, ILogger logger)
            : base(id)

        {
            Configuration = configuration;
            Logger = logger;
            tableService = new TableService(Configuration["DailyBingTableConnectionString"], Configuration["DailyBingTableName"]);

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
            DailyBing dailyBing = await tableService.GetDailyBing();
            DailyBingInfo info = await tableService.GetLatestInfo();
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
            BingMapService mapService = new BingMapService(Configuration["BingMapsAPI"]);

            string guessText = stepContext.Result.ToString();
            if (guessText.ToLower().Contains("check results"))
            {
                DailyBing dailyBing = await tableService.GetDailyBing();
                DailyBingInfo info = await tableService.GetLatestInfo();
                await CheckResults(stepContext, cancellationToken, dailyBing, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
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
                    DailyBing dailyBing = await tableService.GetDailyBing();

                    double distanceFromResult = (Math.Pow(entry.longitude - dailyBing.longitude, 2) + Math.Pow(entry.latitude - dailyBing.latitude, 2));
                    entry.distanceFrom = distanceFromResult;
                    entry.userName = stepContext.Context.Activity.From.Name;
                    entry.userId = stepContext.Context.Activity.From.Id;
                    dailyBing.entries.Add(entry);
                    await tableService.SaveDailyBing(dailyBing);
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Saving your guess as {entry.BingResponse}"), cancellationToken);
                    DailyBingInfo info = await tableService.GetLatestInfo();

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
                
                if (usersWithEntryCount >= userCount)
                {
                    return true;
                }

                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Still more results from users to come - {usersWithEntryCount} users have entered out of the {userCount} in this channel."), cancellationToken);


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
                DailyBingImage image = await tableService.getDailyBingImage();

                dailyBing.distanceToEntry = currentWinningDistance;
                dailyBing.winnerName = currentWinningUser;
                dailyBing.winnerGuess = currentWinningEntry;
                dailyBing.resultSet = true;

                await tableService.SaveDailyBing(dailyBing);
                IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());
                
                reply.Attachments.Add(AttachmentHelper.ResultCardAttachment(currentWinningUser.ToString(), image.Url, currentWinningEntry, currentWinningDistance.ToString("#.##"), dailyBing.extractedLocation, dailyBing.text));
                await stepContext.Context.SendActivityAsync(reply);
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

