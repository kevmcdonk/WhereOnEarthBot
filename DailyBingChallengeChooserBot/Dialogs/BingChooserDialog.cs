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

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class BingChooserDialog : ComponentDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;

        public BingChooserDialog(string id, IConfiguration configuration, ILogger logger)
            : base(id)

        {
            Configuration = configuration;
            Logger = logger;
            
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new BookingDialog());
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
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"]);
            DailyBing dailyBing = await storageService.GetDailyBing();
            DailyBingInfo info = await storageService.GetLatestInfo();
            bool allresultsreceived = CheckWhetherAllEntriesReceived(dailyBing, info);

            if (allresultsreceived)
            {
                await CheckResults(stepContext, cancellationToken, dailyBing, info);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Still more results from users to come. Get yours in if you haven't already.") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            BingMapService mapService = new BingMapService();
            DailyBingEntry entry = await mapService.GetLocationDetails(stepContext.Result.ToString());
            if (entry == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Sorry, bing maps couldn't identify that location. Please try again."), cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(BingChooserDialog), null, cancellationToken);
            }
            else
            {
                StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"]);
                DailyBing dailyBing = await storageService.GetDailyBing();


                double distanceFromResult = (Math.Pow(entry.longitude - dailyBing.longitude, 2) + Math.Pow(entry.latitude - dailyBing.latitude, 2));
                entry.distanceFrom = distanceFromResult;
                entry.userName = stepContext.Context.Activity.From.Name;
                entry.userId = stepContext.Context.Activity.From.Id;
                dailyBing.entries.Add(entry);
                await storageService.SaveDailyBing(dailyBing);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Saving your guess as {entry.BingResponse}"), cancellationToken);
                DailyBingInfo info = await storageService.GetLatestInfo();
                bool allresultsreceived = CheckWhetherAllEntriesReceived(dailyBing, info);

                if (allresultsreceived)
                {
                    await CheckResults(stepContext, cancellationToken, dailyBing, info);
                    return await stepContext.EndDialogAsync(cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Still more results from users to come."), cancellationToken);
                    return await stepContext.BeginDialogAsync(nameof(BingChooserDialog), null, cancellationToken);
                }
            }
        }

        private bool CheckWhetherAllEntriesReceived(DailyBing dailyBing, DailyBingInfo info)
        {
            //GraphService graphService = new GraphService();
            //var accessToken = await context.GetAccessToken();
            List<DailyBingEntry> todayEntries = dailyBing.entries;
            if (info.users == null)
            {
                info.users = new List<DailyBingUser>();
            }
            List<DailyBingUser> bingUsers = info.users;

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

            //TODO: remove this for a user
            if (todayEntries.Count > userCount)
            {
                return true;
            }

            return false;
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
