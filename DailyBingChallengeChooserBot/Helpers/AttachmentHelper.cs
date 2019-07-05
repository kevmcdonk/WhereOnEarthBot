using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace DailyBingChallengeBot.Helpers
{
    public static class AttachmentHelper
    {
        public static Attachment ResultCardAttachment(string winningUserName, string imageUrl, string winningEntry, string winningDistance, string actualAnswer, string originalText)
        {
            var heroCard = new HeroCard
            {
                Title = "We have a winner!",
                Subtitle = $"Congratulations {winningUserName}",
                Text = $"The winning guess was {winningEntry} which was {winningDistance} km from the real answer of {actualAnswer} ({originalText})",
                Images = new List<CardImage> { new CardImage(imageUrl) }
            };

            IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());

            Microsoft.Bot.Schema.Attachment attachment = heroCard.ToAttachment();
            return attachment;

        }
    }
}
