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
    public class MainDialog : LogoutDialog
    {
        protected readonly IConfiguration Configuration;
        protected readonly ILogger Logger;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            Configuration = configuration;
            Logger = logger;

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please login",
                    Title = "Login",
                    Timeout = 300000, // User has 5 minutes to login
                }));

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new BingGuesserDialog(nameof(BingGuesserDialog), configuration, logger));
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
            if (string.IsNullOrEmpty(Configuration["DailyBingStorageConnectionString"]))
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: Storage Connection String is not configured. To continue, add 'DailyBingStorageConnectionString' to the appsettings.json file."), cancellationToken);

                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            else
            {
                IMessageActivity reply = null;
                StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
                DailyBing dailyBing = await storageService.GetDailyBing();
                if (dailyBing.photoUrl == null)
                {
                    reply = MessageFactory.Attachment(new List<Attachment>());
                    Attachment attachment = null;

                    DailyBingInfo info = await GetInfo(stepContext);

                    if (info.currentSource == ImageSource.Google)
                    {
                        attachment = await GetGoogleImageChoiceAttachment();
                    }
                    else
                    {
                        int imageIndex = info.currentImageIndex;
                        attachment = GetBingImageChoiceAttachment(imageIndex);
                    }

                    reply.Attachments.Add(attachment);
                    await stepContext.Context.SendActivityAsync(reply);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Take your choice") }, cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Today's image has already been chosen so you can make your guesses now."), cancellationToken);
                    reply = MessageFactory.Attachment(new List<Attachment>());
                    int imageIndex = await GetImageIndex(stepContext);
                    Attachment attachment = GetBingImageAttachment(imageIndex);
                    reply.Attachments.Add(attachment);

                    await stepContext.Context.SendActivityAsync(reply);
                    
                    return await stepContext.ReplaceDialogAsync(nameof(BingGuesserDialog), null, cancellationToken);
                }
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var command = stepContext.Result.ToString();
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);

            if (command.ToLower().Contains("choose"))
            {
                int imageIndex = await GetImageIndex(stepContext);
                BingImageService imageService = new BingImageService();
                DailyBingImage image = imageService.GetBingImageUrl(imageIndex);
                BingMapService mapService = new BingMapService();
                DailyBingEntry bingEntry = await mapService.GetLocationDetails(image.ImageText);
                var dailyBing = await storageService.GetDailyBing();

                dailyBing.photoUrl = image.Url;
                dailyBing.text = image.ImageText;
                dailyBing.latitude = bingEntry.latitude;
                dailyBing.longitude = bingEntry.longitude;
                dailyBing.extractedLocation = bingEntry.BingResponse;
                dailyBing.entries = new List<DailyBingEntry>();
                dailyBing.publishedTime = DateTime.Now;
                await storageService.SaveDailyBing(dailyBing);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thanks for choosing the image. Now it is time for everyone to start guessing!"), cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(BingGuesserDialog), null, cancellationToken);
                // return await stepContext.BeginDialogAsync(nameof(BingChooserDialog), null, cancellationToken);
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
                var attachment = GetBingImageChoiceAttachment(imageIndex);
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
            // If the child dialog ("BookingDialog") was cancelled or the user failed to confirm, the Result here will be null.
            if (stepContext.Result != null)
            {
                var result = (BookingDetails)stepContext.Result;

                // Now we have all the booking details call the booking service.

                // If the call to the booking service was successful tell the user.

                var timeProperty = new TimexProperty(result.TravelDate);
                var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
                var msg = $"I have you booked to {result.Destination} from {result.Origin} on {travelDateMsg}";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you."), cancellationToken);
            }
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }


        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the token from the previous step. Note that we could also have gotten the
            // token directly from the prompt itself. There is an example of this in the next method.
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse != null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                // return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to do? (type 'me', 'send <EMAIL>' or 'recent')") }, cancellationToken);

                IMessageActivity reply = null;

                reply = MessageFactory.Attachment(new List<Attachment>());
                int imageIndex = await GetImageIndex(stepContext);
                Attachment attachment = GetBingImageChoiceAttachment(imageIndex);
                // reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments.Add(attachment);

                await stepContext.Context.SendActivityAsync(reply);
                //TODO: Replace with ChopicePrompt?
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Take your choice") }, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
            }
            return await stepContext.EndDialogAsync();
        }
        private Attachment GetBingImageChoiceAttachment(int imageIndex)
        {
            BingImageService imageService = new BingImageService();
            DailyBingImage image = imageService.GetBingImageUrl(imageIndex);

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
            
            DailyBingImage image = await mapService.GetRandomLocation();

            var heroCard = new HeroCard
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

            return heroCard.ToAttachment();
        }

        private Attachment GetBingImageAttachment(int imageIndex)
        {
            BingImageService imageService = new BingImageService();
            DailyBingImage image = imageService.GetBingImageUrl(imageIndex);

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
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBingInfo info = await storageService.GetLatestInfo();
            return info;
        }

        private async Task<int> GetImageIndex(WaterfallStepContext context)
        {
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBingInfo info = await storageService.GetLatestInfo();
            return info.currentImageIndex;
        }

        private async Task<ImageSource> GetImageSource(WaterfallStepContext context)
        {
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBingInfo info = await storageService.GetLatestInfo();
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

        private async Task<DialogTurnResult> ProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result != null)
            {
                // We do not need to store the token in the bot. When we need the token we can
                // send another prompt. If the token is valid the user will not need to log back in.
                // The token will be available in the Result property of the task.
                var tokenResponse = stepContext.Result as TokenResponse;

                // If we have the token use the user is authenticated so we may use it to make API calls.
                if (tokenResponse?.Token != null)
                {
                    var command = ((string)stepContext.Values["command"] ?? string.Empty).ToLowerInvariant();
                    
                    if (command.ToLower().Contains("choose"))
                    {
                        
                        GraphService graphService = new GraphService(Configuration["DailyBingSiteId"], Configuration["DailyBingEntryListId"], Configuration["DailyBingUserListId"], Configuration["DailyBingsListId"]);
                        var accessToken = tokenResponse?.Token;

                        int imageIndex = await GetImageIndex(stepContext);
                        BingImageService imageService = new BingImageService();
                        DailyBingImage image = imageService.GetBingImageUrl(imageIndex);
                        BingMapService mapService = new BingMapService();
                        DailyBingEntry bingEntry = await mapService.GetLocationDetails(image.ImageText);

                        await graphService.SaveDailyBing(accessToken, image.ImageText, DateTime.Now, image.Url, bingEntry.BingResponse, bingEntry.latitude.ToString(), bingEntry.longitude.ToString());
                        /*StateClient stateClient = context.Activity.GetStateClient();
                        var userData = stateClient.BotState.GetUserData(context.Activity.ChannelId, context.Activity.From.Id);
                        userData.SetProperty<DailyBingEntry>("CurrentDailyBing", bingEntry);
                        stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                        */
                        return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Thanks for choosing the image. Now it is time for everyone to start guessing!") }, cancellationToken);
                    }
                    else if (command.ToLower().Contains("try another image"))
                    {
                        
                        var reply = MessageFactory.Attachment(new List<Attachment>());
                        int imageIndex = await IncrementAndReturnImageIndex();
                        var attachment = GetBingImageChoiceAttachment(imageIndex);
                        // reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        reply.Attachments.Add(attachment);
                    }
                    else
                    {
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text("Sorry, not sure about that"), cancellationToken);
                    }
                }

                /*
                 *  var message = await result;

            if (message.Text.ToLower().Contains("choose image"))
            {
                GraphService graphService = new GraphService();
                var accessToken = await context.GetAccessToken();

                int imageIndex = GetImageIndex(context);
                BingImageService imageService = new BingImageService();
                DailyBingImage image = imageService.GetBingImageUrl(imageIndex);
                BingMapService mapService = new BingMapService();
                DailyBingEntry bingEntry = await mapService.GetLocationDetails(image.ImageText);

                await graphService.SaveDailyBing(accessToken, image.ImageText, DateTime.Now, image.Url, bingEntry.BingResponse, bingEntry.latitude.ToString(), bingEntry.longitude.ToString());
                StateClient stateClient = context.Activity.GetStateClient();
                var userData = stateClient.BotState.GetUserData(context.Activity.ChannelId, context.Activity.From.Id);
                userData.SetProperty<DailyBingEntry>("CurrentDailyBing", bingEntry);
                stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                await context.PostAsync("Thanks for choosing the image. Now it is time to start guessing everyone!");
                await context.Forward(new BingBotDialog(), null, message, CancellationToken.None);
            }
            else if (message.Text.ToLower().Contains("try another image"))
            {
                var reply = context.MakeMessage();
                int imageIndex = IncrementAndReturnImageIndex(context);
                Attachment attachment = GetBingImageAttachment(imageIndex, context);
                reply.Attachments.Add(attachment);

                await context.PostAsync(reply);
                context.Wait(this.OnOptionSelected);
            }
            else
            {
                //send image again
                await context.PostAsync("Sorry, not sure about that");
                context.Wait(MessageReceivedAsync);
            }
            */
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We couldn't log you in. Please try again later."), cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessResponsesAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result != null)
            {
                // We do not need to store the token in the bot. When we need the token we can
                // send another prompt. If the token is valid the user will not need to log back in.
                // The token will be available in the Result property of the task.
                var tokenResponse = stepContext.Result as TokenResponse;

                // If we have the token use the user is authenticated so we may use it to make API calls.
                if (tokenResponse?.Token != null)
                {
                    //TODO: Log response by user
                    //TODO: Check if all users have replied or if it's late
                    //TODO: Check for someone saying process results
                    //TODO: Process results and reply with winner

                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Thanks, just need to get everyone's answers. If you are sure that's the end, reply 'all done' - I'll trust you") }, cancellationToken);
                    // await stepContext.Context.SendActivityAsync(MessageFactory.Text("Sorry, not sure about that"), cancellationToken);

                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We couldn't log you in. Please try again later."), cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<int> IncrementAndReturnImageIndex()
        {
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBingInfo info = await storageService.GetLatestInfo();
            info.currentImageIndex++;

            if (info.currentImageIndex > 7)
            {
                info.currentImageIndex = 0;
            }

            await storageService.SaveLatestInfo(info);

            return info.currentImageIndex;
        }

        private async Task<ImageSource> UpdateImageSource(ImageSource imageSource)
        {
            StorageService storageService = new StorageService(Configuration["DailyBingStorageConnectionString"], Configuration["DailyBingResultsContainer"]);
            DailyBingInfo info = await storageService.GetLatestInfo();
            info.currentSource = imageSource;

            await storageService.SaveLatestInfo(info);

            return info.currentSource;
        }
    }
}
