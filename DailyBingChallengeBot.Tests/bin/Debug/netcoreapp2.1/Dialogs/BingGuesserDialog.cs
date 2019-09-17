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
using DailyBingChallengeBot.Helpers;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class BingGuesserDialog : ComponentDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;
        private TableService tableService;

        public BingGuesserDialog(string id, IConfiguration configuration, ILogger logger, IBotTelemetryClient telemetryClient)
            : base(id)

        {
            Configuration = configuration;
            Logger = logger;
            TelemetryClient = telemetryClient;

            tableService = new TableService(Configuration["DailyBingTableConnectionString"], Configuration["DailyBingTableName"]);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new AttachmentPrompt(nameof(AttachmentPrompt)));
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
            DailyBingEntriesStatus currentStatus = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyBing, info);

            if (currentStatus.allResultsReceived)
            {
                await CheckResults(stepContext, cancellationToken, dailyBing, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
                string messageText = null;
                if (stepContext != null && stepContext.Result != null)
                {
                    messageText = stepContext.Result.ToString();
                }
                else if (stepContext != null && stepContext.Context != null && stepContext.Context.Activity != null && stepContext.Context.Activity.Text != null)
                {
                    messageText = stepContext.Context.Activity.Text;
                }
                else if (stepContext != null && stepContext.Options != null)
                {
                    PromptOptions options = (PromptOptions)stepContext.Options;
                    messageText = options.Prompt.Text;
                }
                if (messageText != null)
                {
                    if (messageText.ToLower().Contains("check results"))
                    {
                        await CheckResults(stepContext, cancellationToken, dailyBing, info);
                        return await stepContext.EndDialogAsync(cancellationToken);
                    }

                    var userEntries = dailyBing.entries.FindAll(e => e.userName == stepContext.Context.Activity.From.Name);
                    if (userEntries != null && userEntries.Count > 0)
                    {
                        IMessageActivity beginReply = MessageFactory.Text($"Sorry {stepContext.Context.Activity.From.Name}, we already have a result from you. Time for the next person.");
                        PromptOptions beginOptions = new PromptOptions()
                        {
                            Prompt = (Activity)beginReply
                        };
                        return await stepContext.PromptAsync(nameof(TextPrompt), beginOptions, cancellationToken);
                    }
                    return await stepContext.NextAsync(messageText);
                }

                IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());
                reply.Attachments.Add(AttachmentHelper.ImageChosen(dailyBing.photoUrl));
                PromptOptions promptOptions = new PromptOptions
                {
                    Prompt = (Activity)reply,

                };
                return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            BingMapService mapService = new BingMapService(Configuration["BingMapsAPI"]);

            string guessText = stepContext.Result.ToString();
            DailyBingInfo info = await tableService.GetLatestInfo();

            if (guessText.ToLower().Contains("check results"))
            {
                DailyBing dailyBing = await tableService.GetDailyBing();
                
                await CheckResults(stepContext, cancellationToken, dailyBing, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
                TelemetryClient.TrackTrace("Checking for guess: " + guessText, Severity.Information, null);
                try
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

                        //original
                        //double distanceFromResult = (Math.Pow(entry.longitude - dailyBing.longitude, 2) + Math.Pow(entry.latitude - dailyBing.latitude, 2));

                        //new
                        //=ACOS(SIN([@LAT1]*magic)*SIN([@LAT2]*magic)+COS([@LAT1]*magic)*COS([@LAT2]*magic)*COS([@LON1]*magic-[@LON2]*magic))*radius_km
                        //LON1 entry.longitude
                        //LON2 dailyBing.longitude
                        //LAT1 entry.latitude
                        //LAT2 dailyBing.latitude
                        double magic = Math.PI / 180;
                        double radius_km = 6367.4445;
                        double distanceFromResult = Math.Acos(Math.Sin(entry.latitude * magic) * Math.Sin(dailyBing.latitude * magic) + Math.Cos(entry.latitude * magic) * Math.Cos(dailyBing.latitude * magic) * Math.Cos(entry.longitude * magic - dailyBing.longitude * magic)) * radius_km;


                        entry.distanceFrom = distanceFromResult;
                        entry.userName = stepContext.Context.Activity.From.Name;
                        entry.userId = stepContext.Context.Activity.From.Id;
                        dailyBing.entries.Add(entry);
                        await tableService.SaveDailyBing(dailyBing);

                        IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());
                        DailyBingEntriesStatus currentStatus = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyBing, info);
                        reply.Attachments.Add(AttachmentHelper.AwaitingGuesses(currentStatus.userCount, dailyBing.photoUrl, currentStatus.usersWithEntryCount, entry.userName, entry.BingResponse));

                        await stepContext.Context.SendActivityAsync((Activity)reply);
                        return await stepContext.EndDialogAsync(null, cancellationToken);
                    }
                }
                catch(Exception exp)
                {
                    TelemetryClient.TrackTrace("Error loading results: " + exp.Message + exp.StackTrace, Severity.Error, null);
                    throw exp;
                }
            }
        }

        private async Task<DailyBingEntriesStatus> CheckWhetherAllEntriesReceived(WaterfallStepContext stepContext, CancellationToken cancellationToken, DailyBing dailyBing, DailyBingInfo info)
        {
            try
            {
                // Fill in the "standard" properties for BotMessageReceived
                // and add our own property.
                Logger.LogInformation("Checking whether all entries received");
                DailyBingEntriesStatus currentStatus = new DailyBingEntriesStatus()
                {
                    allResultsReceived = false
                };
                
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
                    currentStatus.allResultsReceived = true;
                }

                currentStatus.userCount = userCount;
                currentStatus.usersWithEntryCount = usersWithEntryCount;
                return currentStatus;
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

