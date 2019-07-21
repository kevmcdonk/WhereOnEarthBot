using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Bots;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DailyBingChallengeBot.Controllers
{
    [Route("api/getresults")]
    [ApiController]
    public class GetResultsController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;
        protected readonly BotState ConversationState;
        IConfiguration _configuration;
        ILogger<MainDialog> _logger;

        public GetResultsController(IBotFrameworkHttpAdapter adapter, IConfiguration configuration, ConversationState conversationState,
            ILogger<MainDialog> logger, 
            ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _adapter = adapter;
            _conversationReferences = conversationReferences;
            ConversationState = conversationState;
            _appId = configuration["MicrosoftAppId"];
            _logger = logger;
            _configuration = configuration;

            // If the channel is the Emulator, and authentication is not in use,
            // the AppId will be null.  We generate a random AppId for this case only.
            // This is not required for production, since the AppId will have a value.
            if (string.IsNullOrEmpty(_appId))
            {
                _appId = Guid.NewGuid().ToString(); //if no AppId, use a random Guid
            }
        }

        public async Task<IActionResult> Get()
        {
            var contentText = "<html><body><h1>Proactive messages have been sent.</h1>";
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, BotCallback, default(CancellationToken));
                contentText += $"<p>Conversation Reference {conversationReference}</p>";
            }
            contentText += "</body></html>";
            // Let the caller know proactive messages have been sent
            return new ContentResult()
            {
                Content = contentText,
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
            };
        }

        private async Task BotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var conversationStateAccessors = ConversationState.CreateProperty<DialogState>(nameof(DialogState));

                var dialogSet = new DialogSet(conversationStateAccessors);
                Dialog mainDialog = new MainDialog(_configuration, _logger);
                // BingGuesserDialog dialog = new BingGuesserDialog(mainDialog.Id, _configuration, _logger);
                BingGuesserDialog dialog = new BingGuesserDialog(typeof(BingGuesserDialog).Name.ToString(), _configuration, _logger);
                dialogSet.Add(mainDialog);
                dialogSet.Add(dialog);

                var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);
                //if (results.Status == DialogTurnStatus.Empty)
                //{
                IMessageActivity beginReply = MessageFactory.Text($"check results");

                PromptOptions beginOptions = new PromptOptions()
                {
                    Prompt = (Activity)beginReply
                };
                await dialogContext.BeginDialogAsync(mainDialog.Id, beginOptions, cancellationToken);
                await ConversationState.SaveChangesAsync(dialogContext.Context, false, cancellationToken);
                //}
                //else
                //    await turnContext.SendActivityAsync("Starting proactive message bot call back");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.Message);
            }
        }
    }
}