// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using DailyBingChallengeBot.Models;
using DailyBingChallengeBot.Services;
using System.Collections.Generic;
using DailyBingChallengeBot.Helpers;


namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : LogoutDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;
        private TableService tableService;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger, IBotTelemetryClient telemetryClient)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            Configuration = configuration;
            Logger = logger;
            TelemetryClient = telemetryClient;

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please login",
                    Title = "Login",
                    Timeout = 300000, // User has 5 minutes to login
                }));

            AddDialog(new TextPrompt(nameof(TextPrompt))
            {
                TelemetryClient = telemetryClient,
            });
            AddDialog(new BingGuesserDialog(nameof(BingGuesserDialog), configuration, logger, telemetryClient)
            {
                TelemetryClient = telemetryClient,
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync
            })
            {
                TelemetryClient = telemetryClient,
            });

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);

            tableService = new TableService(Configuration["DailyBingTableConnectionString"], Configuration["DailyBingTableName"]);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            TelemetryClient.TrackTrace("Main dialog started", Severity.Information, null);
            if (string.IsNullOrEmpty(Configuration["DailyBingTableConnectionString"]))
            {
                TelemetryClient.TrackTrace("Connection String not defined", Severity.Error, null);
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: Storage Connection String is not configured. To continue, add 'DailyBingTableConnectionString' to the appsettings.json file."), cancellationToken);

                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            else
            {
                IMessageActivity reply = null;

                TelemetryClient.TrackTrace("Get Daily Bing and Team Info", Severity.Information, null);
                DailyBing dailyBing = await tableService.GetDailyBing();
                DailyBingTeam team = await tableService.getDailyBingTeamInfo();
                TelemetryClient.TrackTrace("Check whether today's challenge exists", Severity.Information, null);
                if (dailyBing.photoUrl == null)
                {
                    TelemetryClient.TrackTrace("No Daily Bing so check details", Severity.Information, null);
                    var activity = stepContext.Context.Activity;
                    if (team.ChannelData == null)
                    {
                        team.ChannelData = activity.GetChannelData<TeamsChannelData>();
                    }
                    var teamsChannelData = team.ChannelData;

                    var channelId = teamsChannelData.Channel.Id;
                    var tenantId = teamsChannelData.Tenant.Id;
                    string myBotId = activity.Recipient.Id;
                    string teamId = teamsChannelData.Team.Id;
                    string teamName = teamsChannelData.Team.Name;
                    
                    await this.tableService.SaveDailyBingTeamInfo(new DailyBingTeam()
                    {
                        ServiceUrl = activity.ServiceUrl,
                        TeamId = teamId,
                        TeamName = teamName,
                        TenantId = tenantId,
                        InstallerName = "Automatic",
                        BotId = myBotId,
                        ChannelId = channelId,
                        ChannelData = teamsChannelData
                    });

                    reply = MessageFactory.Attachment(new List<Attachment>());
                    Attachment attachment = null;

                    DailyBingInfo info = await GetInfo(stepContext);

                    if (info.currentSource == ImageSource.Google)
                    {
                        TelemetryClient.TrackTrace("Current source is Google so get an image", Severity.Information, null);
                        attachment = await GetGoogleImageChoiceAttachment();
                        TelemetryClient.TrackTrace("Loaded Google image", Severity.Information, null);
                    }
                    else
                    {
                        TelemetryClient.TrackTrace("Current source is Bing so get the latest image", Severity.Information, null);
                        int imageIndex = info.currentImageIndex;
                        attachment = await GetBingImageChoiceAttachment(imageIndex);
                        TelemetryClient.TrackTrace("Loaded Bing image", Severity.Information, null);
                    }

                    reply.Attachments.Add(attachment);
                    TelemetryClient.TrackTrace("Sending image reply", Severity.Information, null);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = (Activity)reply }, cancellationToken);
                }
                else
                {
                    if (!dailyBing.resultSet)
                    {
                        // Pass on the check results message from the proactive controller if set
                        PromptOptions options = null;
                        if (stepContext != null && stepContext.Options != null)
                        {
                            options = (PromptOptions)stepContext.Options;
                            
                        }
                        return await stepContext.ReplaceDialogAsync(nameof(BingGuesserDialog), options, cancellationToken);
                    }
                    else
                    {
                        IMessageActivity winningReply = MessageFactory.Attachment(new List<Attachment>());

                        winningReply.Attachments.Add(AttachmentHelper.ResultCardAttachment(dailyBing.winnerName, dailyBing.photoUrl, dailyBing.winnerGuess, dailyBing.distanceToEntry.ToString("#.##"), dailyBing.extractedLocation, dailyBing.text));
                        await stepContext.Context.SendActivityAsync(winningReply);
                        return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                    }
                }
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var command = stepContext.Result.ToString();

            if (command.ToLower().Contains("choose image"))
            {
                int imageIndex = await GetImageIndex(stepContext);
                BingImageService imageService = new BingImageService();
                DailyBingImage image = await tableService.getDailyBingImage();
                BingMapService mapService = new BingMapService(Configuration["BingMapsAPI"]);
                DailyBingEntry bingEntry = await mapService.GetLocationDetails(image.ImageText);
                var dailyBing = await tableService.GetDailyBing();

                dailyBing.photoUrl = image.Url;
                dailyBing.text = image.ImageText;
                dailyBing.latitude = bingEntry.latitude;
                dailyBing.longitude = bingEntry.longitude;
                dailyBing.extractedLocation = bingEntry.BingResponse;
                dailyBing.entries = new List<DailyBingEntry>();
                dailyBing.publishedTime = DateTime.Now;
                dailyBing.currentStatus = DailyBingStatus.Guessing;
                await tableService.SaveDailyBing(dailyBing);

                IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());

                reply.Attachments.Add(AttachmentHelper.ImageChosen(dailyBing.photoUrl));
                var activity = (Activity)reply;
                
                await stepContext.Context.SendActivityAsync((Activity)reply);
                return await stepContext.EndDialogAsync(cancellationToken);
                //return await stepContext.ReplaceDialogAsync(nameof(BingGuesserDialog), promptOptions, cancellationToken);
            }
            else if (command.ToLower().Contains("try another image"))
            {
                int imageIndex = await IncrementAndReturnImageIndex();
            }

            else if (command.ToLower().Contains("switch to google"))
            {
                try
                {
                    var reply = MessageFactory.Attachment(new List<Attachment>());
                    var attachment = await GetGoogleImageChoiceAttachment();
                    await UpdateImageSource(ImageSource.Google);
                    reply.Attachments.Add(attachment);
                }
                catch(Exception exp)
                {
                    Logger.LogError(exp, $"Could not set Google Image: {exp.Message} - {exp.StackTrace}", null);
                    throw exp;
                }
            }
            else if (command.ToLower().Contains("switch to bing"))
            {

                var reply = MessageFactory.Attachment(new List<Attachment>());
                int imageIndex = await GetImageIndex(stepContext);
                await UpdateImageSource(ImageSource.Bing);
                var attachment = await GetBingImageChoiceAttachment(imageIndex);
                // reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments.Add(attachment);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Sorry, not sure about that"), cancellationToken);
            }

            return await stepContext.BeginDialogAsync(nameof(MainDialog), null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<Attachment> GetBingImageChoiceAttachment(int imageIndex)
        {
            BingImageService imageService = new BingImageService();
            DailyBingImage image = imageService.GetBingImageUrl(imageIndex);
            await tableService.SaveDailyBingImage(image);

            var heroCard = new HeroCard
            {
                Title = "Today's Bing",
                Subtitle = image.ImageRegion,
                Text = "Click to choose the image for today or try another image.",
                Images = new List<CardImage> { new CardImage(image.Url) },
                Buttons = new List<CardAction> {
                        new CardAction(ActionTypes.ImBack, "Choose image", value: "Choose image"),
                        new CardAction(ActionTypes.ImBack, "Try another image", value: "Try another image"),
                        new CardAction(ActionTypes.ImBack, "Switch to Google", value: "Switch to Google")
                    }
            };

            return heroCard.ToAttachment();
        }

        private async Task<Attachment> GetGoogleImageChoiceAttachment()
        {
            GoogleMapService mapService = new GoogleMapService(Configuration["GoogleMapsAPI"]);
            HeroCard heroCard = null;

            try
            {


                DailyBingImage image = await mapService.GetRandomLocation();
                await tableService.SaveDailyBingImage(image);

                heroCard = new HeroCard
                {
                    Title = "Today's Bing",
                    Subtitle = image.ImageRegion,
                    Text = "Click to choose the image for today or try another image.",
                    Images = new List<CardImage> { new CardImage(image.Url) },
                    Buttons = new List<CardAction> {
                            new CardAction(ActionTypes.ImBack, "Choose image", value: "Choose image"),
                            new CardAction(ActionTypes.ImBack, "Try another Google image", value: "Try another image"),
                            new CardAction(ActionTypes.ImBack, "Switch to Bing", value: "Switch to Bing")
                        }
                };
            }
            catch(Exception exp)
            {
                if (exp.Message == "Sorry, couldn't find a suitable image. Try again shortly.")
                {
                    heroCard = new HeroCard
                    {
                        Title = "Today's Bing",
                        Subtitle = "Not found",
                        Text = "After trying 50 different locations, Google couldn't find a suitable image.",
                            Buttons = new List<CardAction> {
                            new CardAction(ActionTypes.ImBack, "Try another Google image", value: "Try another image"),
                            new CardAction(ActionTypes.ImBack, "Switch to Bing", value: "Switch to Bing")
                        }
                    };
                }
                else if (exp.Message == "Over Google query limit")
                {
                    heroCard = new HeroCard
                    {
                        Title = "Today's Bing",
                        Subtitle = "Not found",
                        Text = "The Google Maps Search Service is on a low level and has exceeeded it's usage. Please wait a few minutes and try again or switch to Bing.",
                        Buttons = new List<CardAction> {
                            new CardAction(ActionTypes.ImBack, "Try another Google image", value: "Try another image"),
                            new CardAction(ActionTypes.ImBack, "Switch to Bing", value: "Switch to Bing")
                        }
                    };
                }
                else
                {
                    throw exp;
                }
            }

            return heroCard.ToAttachment();
        }

        private async Task<Attachment> GetDailyBingImageAttachment()
        {
            DailyBingImage image = await tableService.getDailyBingImage();

            var heroCard = new HeroCard
            {
                Title = "Today's Bing",
                Subtitle = image.ImageRegion,
                Images = new List<CardImage> { new CardImage(image.Url) }
            };

            return heroCard.ToAttachment();
        }

        private async Task<DailyBingInfo> GetInfo(WaterfallStepContext context)
        {
            DailyBingInfo info = await tableService.GetLatestInfo();
            return info;
        }

        private async Task<int> GetImageIndex(WaterfallStepContext context)
        {
            DailyBingInfo info = await tableService.GetLatestInfo();
            return info.currentImageIndex;
        }

        private async Task<ImageSource> GetImageSource(WaterfallStepContext context)
        {
            DailyBingInfo info = await tableService.GetLatestInfo();
            return info.currentSource;
        }

        private async Task<DialogTurnResult> CommandStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["command"] = stepContext.Result;

            // Call the prompt again because we need the token. The reasons for this are:
            // 1. If the user is already logged in we do not need to store the token locally in the bot and worry
            // about refreshing it. We can always just call the prompt again to get the token.
            // 2. We never know how long it will take a user to respond. By the time the
            // user responds the token may have expired. The user would then be prompted to login again.
            //
            // There is no reason to store the token locally in the bot because we can always just call
            // the OAuth prompt to get the token or get a new token if needed.
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<int> IncrementAndReturnImageIndex()
        {
            DailyBingInfo info = await tableService.GetLatestInfo();
            info.currentImageIndex++;

            if (info.currentImageIndex > 7)
            {
                info.currentImageIndex = 0;
            }

            await tableService.SaveLatestInfo(info);

            return info.currentImageIndex;
        }

        private async Task<ImageSource> UpdateImageSource(ImageSource imageSource)
        {
            DailyBingInfo info = await tableService.GetLatestInfo();
            info.currentSource = imageSource;

            await tableService.SaveLatestInfo(info);

            return info.currentSource;
        }

        private async Task UpdateDailyBingImage(DailyBingImage image)
        {            
            await tableService.SaveDailyBingImage(image);

            return;
        }
    }
}
