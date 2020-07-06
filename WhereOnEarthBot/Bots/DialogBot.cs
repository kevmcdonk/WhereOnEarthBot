// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WhereOnEarthBot.Helpers;
using WhereOnEarthBot.Models;
using WhereOnEarthBot.Services;
using Microsoft.ApplicationInsights;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Connector.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples.Bots
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;
        protected readonly IBotTelemetryClient TelemetryClient;
        private ConcurrentDictionary<string, ConversationReference> ConversationReferences;
        private TableService tableService;
        protected readonly IConfiguration Configuration;

        public DialogBot(
            ConversationState conversationState,
            UserState userState,
            T dialog,
            ILogger<DialogBot<T>> logger,
            IBotTelemetryClient telemetryClient,
            ConcurrentDictionary<string, ConversationReference> conversationReferences,
            IConfiguration configuration
        )
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            TelemetryClient = telemetryClient;
            ConversationReferences = conversationReferences;
            Configuration = configuration;

            tableService = new TableService(Configuration["DailyChallengeTableConnectionString"], Configuration["DailyChallengeTableName"]);
        }
        private void AddConversationReference(Activity activity)
        {
            var conversationReference = activity.GetConversationReference();
            Logger.LogInformation($"Adding conversation reference - User Id {conversationReference.User.Id}, Conversation Reference: {conversationReference}");
            ConversationReferences.AddOrUpdate(conversationReference.User.Id, conversationReference, (key, newValue) => conversationReference);
        }

        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Conversation updated");
            AddConversationReference(turnContext.Activity as Activity);

            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");
            var activity = turnContext.Activity;

            var teamsChannelData = activity.GetChannelData<TeamsChannelData>();
            var channelId = teamsChannelData.Channel.Id;
            var tenantId = teamsChannelData.Tenant.Id;
            string myBotId = activity.Recipient.Id;
            string teamId = activity.Conversation.Id;
            string teamName = activity.Conversation.Name;

            using (var connectorClient = new ConnectorClient(new System.Uri(activity.ServiceUrl)))
            {
                foreach (var member in activity.MembersAdded)
                {
                    if (member.Id == myBotId)
                    {
                        var properties = new Dictionary<string, string>
                                {
                                    { "Scope", activity.Conversation?.ConversationType },
                                    { "TeamId", teamId },
                                    { "InstallerId", activity.From.Id },
                                };

                        // Try to determine the name of the person that installed the app, which is usually the sender of the message (From.Id)
                        // Note that in some cases we cannot resolve it to a team member, because the app was installed to the team programmatically via Graph
                        var teamMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
                        var personThatAddedBot = teamMembers.FirstOrDefault(x => x.Id == activity.From.Id)?.Name;
                        var channelData = turnContext.Activity.GetChannelData<TeamsChannelData>();

                        await this.tableService.SaveDailyChallengeTeamInfo(new DailyChallengeTeam()
                        {
                            ServiceUrl = activity.ServiceUrl,
                            TeamId = teamId,
                            TeamName = teamName,
                            TenantId = tenantId,
                            InstallerName = personThatAddedBot,
                            BotId = myBotId,
                            ChannelId = channelId,
                            ChannelData = channelData
                        });
                    }

                    if (member.Id != turnContext.Activity.Recipient.Id)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the Where On Earth Bot. This is how the admin will choose the Daily Challenge. Type anything to get logged in. Type 'logout' to sign-out."), cancellationToken);
                    }
                }
            }

            // Run the Dialog with the new message Activity.
            await Dialog.Run(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (turnContext?.Activity?.Type == ActivityTypes.Invoke && turnContext.Activity.ChannelId == "msteams")
                    await Dialog.Run(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                else
                    await base.OnTurnAsync(turnContext, cancellationToken);

                // Save any state changes that might have occured during the turn.
                await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
            }
            catch (System.Exception exp)
            {
                Logger.LogError(exp, $"Error setting up turn: {exp.Message} - { exp.StackTrace}", null);
            }
        }


        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");

            // Run the Dialog with the new message Activity.
            await Dialog.Run(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
        }

        public async Task TriggerResultChat(IBotFrameworkHttpAdapter adapter)
        {
            try
            {
                Logger.LogInformation("Get challenge triggered");
                var team = await this.tableService.getDailyChallengeTeamInfo();
                var dailyChallenge = await tableService.GetDailyChallenge();
                // If no photo selected, send update
                if (dailyChallenge.photoUrl == null)
                {
                    Logger.LogInformation("No current photo so need to start dialog to ask");
                    Logger.LogInformation("Team Service Url:" + team.ServiceUrl);
                    MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);
                    ConnectorClient connectorClient = new ConnectorClient(new Uri(team.ServiceUrl), Configuration["MicrosoftAppId"], Configuration["MicrosoftAppPassword"]);
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var bot = new ChannelAccount(team.BotId);
                    //string replyText = $"<at>@{teamName}</at> ";
                    string replyText = $"{teamName}";
                    Logger.LogInformation($"ReplyText:{replyText}");
                    var activity = MessageFactory.Text(replyText);

                    var mentioned = JObject.FromObject(new
                    {
                        id = team.TeamId,
                        name = teamName
                    });
                    var mentionedEntity = new Entity("mention")
                    {
                        Properties = JObject.FromObject(new { mentioned = mentioned, text = replyText }),
                    };
                   // activity.Entities = new[] { mentionedEntity };
                    activity.Text = "It's time for the daily challenge " + replyText;
                    var convParams = new ConversationParameters()
                    {
                        TenantId = team.TenantId,
                        Bot = bot,
                        IsGroup = true,
                        ChannelData = team.ChannelData,
                        Activity = activity
                    };

                    Logger.LogInformation("Attempting to create conversation. TenantID:" + team.TenantId + ", ChannelData Name: " + team.ChannelData.Channel.Name);
                    var conversation = await connectorClient.Conversations.CreateConversationAsync(convParams);
                    BotAdapter ba = (BotAdapter)adapter;
                    Logger.LogInformation("Created conversation reference");
                    var conversationReference = new ConversationReference(conversation.ActivityId);
                    conversationReference.Bot = bot;
                    conversationReference.ChannelId = team.ChannelId;
                    conversationReference.Conversation = new ConversationAccount();
                    var convAccount = new ConversationAccount(true, null, conversation.Id);
                    convAccount.TenantId = team.TenantId;

                    conversationReference.Conversation = convAccount;
                    conversationReference.ServiceUrl = team.ServiceUrl;
                    Logger.LogInformation("Getting in to the conversation");
                    await ba.ContinueConversationAsync(Configuration["MicrosoftAppId"], conversationReference, TriggerBotCallback, default(CancellationToken));
                }

            }
            catch (Microsoft.Bot.Schema.ErrorResponseException errEx)
            {
                Logger.LogError(errEx, $"Web error making pairups: {errEx.Message}:::{errEx.Body.Error.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error making pairups: {ex.Message}");
            }
        }

        public async Task CheckResultChat(IBotFrameworkHttpAdapter adapter)
        {
            try
            {
                var team = await this.tableService.getDailyChallengeTeamInfo();
                var dailyChallenge = await tableService.GetDailyChallenge();
                // If no photo selected, send update
                if (dailyChallenge.winnerName == null)
                {
                    MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);
                    ConnectorClient connectorClient = new ConnectorClient(new Uri(team.ServiceUrl), Configuration["MicrosoftAppId"], Configuration["MicrosoftAppPassword"]);
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var bot = new ChannelAccount(team.BotId);
                    // string replyText = $"<at>@{teamName}</at> ";
                    string replyText = $"@{teamName}";

                    var activity = MessageFactory.Text(replyText);

                    var mentioned = JObject.FromObject(new
                    {
                        id = team.TeamId,
                        name = teamName
                    });
                    var mentionedEntity = new Entity("mention")
                    {
                        Properties = JObject.FromObject(new { mentioned = mentioned, text = replyText }),
                    };
                    //activity.Entities = new[] { mentionedEntity };
                    activity.Text = "It's time for the results " + replyText;
                    var convParams = new ConversationParameters()
                    {
                        TenantId = team.TenantId,
                        Bot = bot,
                        IsGroup = true,
                        ChannelData = team.ChannelData,
                        Activity = activity
                    };

                    var conversation = await connectorClient.Conversations.CreateConversationAsync(convParams);
                    BotAdapter ba = (BotAdapter)adapter;

                    var conversationReference = new ConversationReference(conversation.ActivityId);
                    conversationReference.Bot = bot;
                    conversationReference.ChannelId = team.ChannelId;
                    conversationReference.Conversation = new ConversationAccount();
                    var convAccount = new ConversationAccount(true, null, conversation.Id);
                    convAccount.TenantId = team.TenantId;

                    conversationReference.Conversation = convAccount;
                    conversationReference.ServiceUrl = team.ServiceUrl;

                    await ba.ContinueConversationAsync(Configuration["MicrosoftAppId"], conversationReference, ResultsBotCallback, default(CancellationToken));
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error making pairups: {ex.Message}");
            }
        }

        public async Task SendReminderChat(IBotFrameworkHttpAdapter adapter)
        {
            try
            {
                TelemetryClient.TrackTrace("Sending reminder from Bot", Severity.Information, null);
                var team = await this.tableService.getDailyChallengeTeamInfo();
                var dailyChallenge = await tableService.GetDailyChallenge();
                // If no photo selected, send update
                if (dailyChallenge.winnerName == null)
                {
                    MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);
                    TelemetryClient.TrackTrace("Sending MicrosoftAppId: " + Configuration["MicrosoftAppId"], Severity.Information, null);
                    ConnectorClient connectorClient = new ConnectorClient(new Uri(team.ServiceUrl), Configuration["MicrosoftAppId"], Configuration["MicrosoftAppPassword"]);
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var bot = new ChannelAccount(team.BotId);
                    //string replyText = $"<at>@{teamName}</at> ";
                    string replyText = $"@{teamName}";

                    var activity = MessageFactory.Text(replyText);

                    var mentioned = JObject.FromObject(new
                    {
                        id = team.TeamId,
                        name = teamName
                    });
                    var mentionedEntity = new Entity("mention")
                    {
                        Properties = JObject.FromObject(new { mentioned = mentioned, text = replyText }),
                    };
                    //activity.Entities = new[] { mentionedEntity };
                    activity.Text = "It's reminder time, " + replyText;
                    var convParams = new ConversationParameters()
                    {
                        TenantId = team.TenantId,
                        Bot = bot,
                        IsGroup = true,
                        ChannelData = team.ChannelData,
                        Activity = (Activity)activity
                    };
                    TelemetryClient.TrackTrace("ConvParams: " + JsonConvert.SerializeObject(convParams), Severity.Information, null);
                    var conversation = await connectorClient.Conversations.CreateConversationAsync(convParams);
                    BotAdapter ba = (BotAdapter)adapter;

                    var conversationReference = new ConversationReference(conversation.ActivityId);
                    conversationReference.Bot = bot;
                    conversationReference.ChannelId = team.ChannelId;
                    conversationReference.Conversation = new ConversationAccount();
                    var convAccount = new ConversationAccount(true, null, conversation.Id);
                    convAccount.TenantId = team.TenantId;

                    conversationReference.Conversation = convAccount;
                    conversationReference.ServiceUrl = team.ServiceUrl;
                    TelemetryClient.TrackTrace("Sending to Conversation", Severity.Information, null);
                    await ba.ContinueConversationAsync(Configuration["MicrosoftAppId"], conversationReference, ReminderBotCallback, default(CancellationToken));
                }

            }
            catch (Exception ex)
            {
                TelemetryClient.TrackTrace("Error sending reminder: " + ex.Message + ex.StackTrace, Severity.Error, null);
                Logger.LogError(ex, $"Error making pairups: {ex.Message}");
            }
        }

        private async Task TriggerBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var conversationStateAccessors = this.ConversationState.CreateProperty<DialogState>(nameof(DialogState));

                var dialogSet = new DialogSet(conversationStateAccessors);
                dialogSet.Add(this.Dialog);

                var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

                var results = await dialogContext.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dialogContext.BeginDialogAsync(Dialog.Id, null, cancellationToken);
                    await ConversationState.SaveChangesAsync(dialogContext.Context, false, cancellationToken);
                }
                else
                    await turnContext.SendActivityAsync("Starting proactive message bot call back");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.Message);
            }

        }

        private async Task ResultsBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var conversationStateAccessors = this.ConversationState.CreateProperty<DialogState>(nameof(DialogState));

                var dialogSet = new DialogSet(conversationStateAccessors);
                dialogSet.Add(this.Dialog);

                var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

                var results = await dialogContext.ContinueDialogAsync(cancellationToken);
                
                if (results.Status == DialogTurnStatus.Empty)
                {
                    IMessageActivity checkResultsText = MessageFactory.Text($"@WhereOnEarthBot Check results");
                    PromptOptions checkResultsOptions = new PromptOptions()
                    {
                        Prompt = (Activity)checkResultsText
                    };

                    await dialogContext.BeginDialogAsync(Dialog.Id, checkResultsOptions, cancellationToken);
                }
                else
                    await turnContext.SendActivityAsync("Starting proactive message bot call back");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.Message);
            }
        }

        private async Task ReminderBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                TelemetryClient.TrackTrace("ReminderBotCallback called", Severity.Information, null);
                var dailyChallenge = await tableService.GetDailyChallenge();
                var activity = MessageFactory.Attachment(AttachmentHelper.Reminder(dailyChallenge.photoUrl));
                await turnContext.SendActivityAsync(activity);
            }
            catch (Exception ex)
            {
                TelemetryClient.TrackTrace("Error in reminder callback: " + ex.Message + ex.StackTrace, Severity.Error, null);
                this.Logger.LogError(ex.Message);
            }
        }

        private async Task<string> GetTeamNameAsync(ConnectorClient connectorClient, string teamId)
        {
            TeamsConnectorClient teamsConnectorClient = new TeamsConnectorClient(connectorClient.BaseUri, connectorClient.Credentials);//connectorClient.GetTeamsConnectorClient();
            var teamDetailsResult = await teamsConnectorClient.Teams.FetchTeamDetailsAsync(teamId);
            return teamDetailsResult.Name;
        }
    }
}
