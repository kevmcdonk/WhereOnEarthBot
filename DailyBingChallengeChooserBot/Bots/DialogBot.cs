// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DailyBingChallengeBot.Models;
using DailyBingChallengeBot.Services;
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
        private ConcurrentDictionary<string, ConversationReference> ConversationReferences;
        private TableService tableService;
        protected readonly IConfiguration Configuration;

        public DialogBot(
            ConversationState conversationState, 
            UserState userState, 
            T dialog, 
            ILogger<DialogBot<T>> logger, 
            ConcurrentDictionary<string, ConversationReference> conversationReferences,
            IConfiguration configuration
        )
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            ConversationReferences = conversationReferences;
            Configuration = configuration;

            tableService = new TableService(Configuration["DailyBingTableConnectionString"], Configuration["DailyBingTableName"]);
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

                        await this.tableService.SaveDailyBingTeamInfo(new DailyBingTeam()
                        {
                            ServiceUrl = activity.ServiceUrl,
                            TeamId = teamId,
                            TenantId = tenantId,
                            InstallerName = personThatAddedBot,
                            BotId = myBotId,
                            ChannelId = channelId,
                            ChannelData = channelData
                        });
                    }

                    if (member.Id != turnContext.Activity.Recipient.Id)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the Daily Bing Challenge Chooser Bot. This is how the admin will choose the Daily Bing. Type anything to get logged in. Type 'logout' to sign-out."), cancellationToken);
                    }
                }
            }

            // Run the Dialog with the new message Activity.
            await Dialog.Run(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
        }

        /*
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the Daily Bing Challenge Chooser Bot. This is how the admin will choose the Daily Bing. Type anything to get logged in. Type 'logout' to sign-out."), cancellationToken);
                }
            }
        }
        */

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
            catch(System.Exception exp)
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
            // Recall all the teams where we have been added
            // For each team where bot has been added:
            //     Pull the roster of the team
            //     Remove the members who have opted out of pairups
            //     Match each member with someone else
            //     Save this pair
            // Now notify each pair found in 1:1 and ask them to reach out to the other person
            // When contacting the user in 1:1, give them the button to opt-out
            var installedTeamsCount = 0;
            var pairsNotifiedCount = 0;
            var usersNotifiedCount = 0;

            try
            {
                var team = await this.tableService.getDailyBingTeamInfo();

                    try
                    {
                        MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);
                        ConnectorClient connectorClient = new ConnectorClient(new Uri(team.ServiceUrl), Configuration["MicrosoftAppId"], Configuration["MicrosoftAppPassword"]);
                        // var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var bot = new ChannelAccount(team.BotId);
                    
                    var activity = MessageFactory.Text("Triggering the Daily Bing on schedule");
                    //TODO: check to see if already triggered
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
                    
                    await ba.ContinueConversationAsync(Configuration["MicrosoftAppId"], conversationReference, BotCallback, default(CancellationToken));

                    // foreach (var conversation in conversations.Conversations) {
                    //TODO: Return the card with the buttons and all the photo
                    // Next to change dialogue to check for stuff first
                   // await connectorClient.Conversations.SendToConversationAsync(activity);
                    
                    

                    //}
                    //.CreateOrGetDirectConversation(bot, null, team.TenantId);
                }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                    }
                
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error making pairups: {ex.Message}");
            }
        }

        private async Task BotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var conversationStateAccessors = this.ConversationState.CreateProperty<DialogState>(nameof(DialogState));
                // need to somehow set auth for clientconnector
                //turnContext.Adapter
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

        private async Task<string> GetTeamNameAsync(ConnectorClient connectorClient, string teamId)
        {
            TeamsConnectorClient teamsConnectorClient = new TeamsConnectorClient(connectorClient.BaseUri, connectorClient.Credentials);//connectorClient.GetTeamsConnectorClient();
            var teamDetailsResult = await teamsConnectorClient.Teams.FetchTeamDetailsAsync(teamId);
            return teamDetailsResult.Name;
        }
    }
}
