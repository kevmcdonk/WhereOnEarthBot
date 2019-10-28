// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using WhereOnEarthBot.Models;
using WhereOnEarthBot.Services;
using System.Collections.Generic;
using Microsoft.Bot.Connector;
using WhereOnEarthBot.Helpers;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class ChallengeGuesserDialog : ComponentDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;
        private TableService tableService;

        public ChallengeGuesserDialog(string id, IConfiguration configuration, ILogger logger, IBotTelemetryClient telemetryClient)
            : base(id)

        {
            Configuration = configuration;
            Logger = logger;
            TelemetryClient = telemetryClient;

            tableService = new TableService(Configuration["DailyChallengeTableConnectionString"], Configuration["DailyChallengeTableName"]);

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
            DailyChallenge dailyChallenge = await tableService.GetDailyChallenge();
            DailyChallengeInfo info = await tableService.GetLatestInfo();
            DailyChallengeEntriesStatus currentStatus = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyChallenge, info);

            if (currentStatus.allResultsReceived)
            {
                await CheckResults(stepContext, cancellationToken, dailyChallenge, info);
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
                        await CheckResults(stepContext, cancellationToken, dailyChallenge, info);
                        return await stepContext.EndDialogAsync(cancellationToken);
                    }

                    var userEntries = dailyChallenge.entries.FindAll(e => e.userName == stepContext.Context.Activity.From.Name);
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
                reply.Attachments.Add(AttachmentHelper.ImageChosen(dailyChallenge.photoUrl));
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
            DailyChallengeInfo info = await tableService.GetLatestInfo();

            if (guessText.ToLower().Contains("check results"))
            {
                DailyChallenge dailyChallenge = await tableService.GetDailyChallenge();
                
                await CheckResults(stepContext, cancellationToken, dailyChallenge, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
                TelemetryClient.TrackTrace("Checking for guess: " + guessText, Severity.Information, null);
                try
                {
                    DailyChallengeEntry entry = await mapService.GetLocationDetails(guessText);
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
                        return await stepContext.EndDialogAsync();
                    }
                    else
                    {
                        DailyChallenge dailyChallenge = await tableService.GetDailyChallenge();
                        if (dailyChallenge.entries != null)
                        {
                            var matchingEntries = dailyChallenge.entries.Where<DailyChallengeEntry>(e => e.imageResponse == entry.imageResponse);
                            if (matchingEntries.Count() > 0)
                            {
                                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Sorry, someone has beaten you to suggesting '{stepContext.Result.ToString()}'. Please try again."), cancellationToken);
                                // This line caused a bit of a meltdown so changing to end dialogue
                                //return await stepContext.BeginDialogAsync(nameof(ChallengeGuesserDialog), null, cancellationToken);
                                return await stepContext.EndDialogAsync();
                            }
                        }
                        
                        double distanceFromResult = DistanceMeasureHelper.GetDistanceFromResult(entry.latitude, entry.longitude, dailyChallenge.latitude, dailyChallenge.longitude);

                        entry.distanceFrom = distanceFromResult;
                        entry.userName = stepContext.Context.Activity.From.Name;
                        entry.userId = stepContext.Context.Activity.From.Id;
                        dailyChallenge.entries.Add(entry);
                        
                        await tableService.SaveDailyChallenge(dailyChallenge);

                        IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());
                        DailyChallengeEntriesStatus currentStatus = await CheckWhetherAllEntriesReceived(stepContext, cancellationToken, dailyChallenge, info);
                        reply.Attachments.Add(AttachmentHelper.AwaitingGuesses(currentStatus.userCount, dailyChallenge.photoUrl, currentStatus.usersWithEntryCount, entry.userName, entry.imageResponse));

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

        private async Task<DailyChallengeEntriesStatus> CheckWhetherAllEntriesReceived(WaterfallStepContext stepContext, CancellationToken cancellationToken, DailyChallenge dailyChallenge, DailyChallengeInfo info)
        {
            try
            {
                // Fill in the "standard" properties for BotMessageReceived
                // and add our own property.
                Logger.LogInformation("Checking whether all entries received");
                DailyChallengeEntriesStatus currentStatus = new DailyChallengeEntriesStatus()
                {
                    allResultsReceived = false
                };
                
                List<DailyChallengeEntry> todayEntries = dailyChallenge.entries;
                if (info.users == null)
                {
                    info.users = new List<DailyChallengeUser>();
                }
                List<DailyChallengeUser> challengeUsers = new List<DailyChallengeUser>();

                var microsoftAppId = Configuration["MicrosoftAppId"];
                var microsoftAppPassword = Configuration["MicrosoftAppPassword"];

                var connector = new ConnectorClient(new Uri(stepContext.Context.Activity.ServiceUrl), microsoftAppId, microsoftAppPassword);
                var response = await connector.Conversations.GetConversationMembersWithHttpMessagesAsync(stepContext.Context.Activity.Conversation.Id);
                //var response = (await connectorClient.Conversations.GetConversationMembersAsync());
                foreach (var user in response.Body)
                {
                    challengeUsers.Add(new DailyChallengeUser()
                    {
                        id = user.Id,
                        username = user.Name
                    });
                }

                int userCount = challengeUsers.Count;
                int usersWithEntryCount = 0;

                foreach (var user in challengeUsers)
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

        private async Task CheckResults(WaterfallStepContext stepContext, CancellationToken cancellationToken, DailyChallenge dailyChallenge, DailyChallengeInfo info)
        {
           List<DailyChallengeEntry> todayEntries = dailyChallenge.entries;

            string currentWinningUser = "";
            string currentWinningEntry = "";
            double currentWinningDistance = double.MaxValue;
           

            foreach (var entry in todayEntries)
            {
                if (entry.distanceFrom < currentWinningDistance)
                {
                    currentWinningUser = entry.userName;
                    currentWinningEntry = entry.imageResponse;
                    currentWinningDistance = entry.distanceFrom;
                }
            }
            try
            {
                DailyChallengeImage image = await tableService.getDailyChallengeImage();

                dailyChallenge.distanceToEntry = currentWinningDistance;
                dailyChallenge.winnerName = currentWinningUser;
                dailyChallenge.winnerGuess = currentWinningEntry;
                dailyChallenge.resultSet = true;

                await tableService.SaveDailyChallenge(dailyChallenge);
                IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());
                
                reply.Attachments.Add(AttachmentHelper.ResultCardAttachment(currentWinningUser.ToString(), image.Url, currentWinningEntry, currentWinningDistance.ToString("#.##"), dailyChallenge.extractedLocation, dailyChallenge.text));
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

