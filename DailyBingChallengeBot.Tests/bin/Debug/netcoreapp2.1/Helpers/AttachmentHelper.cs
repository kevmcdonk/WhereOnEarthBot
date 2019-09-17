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

        public static Attachment AwaitingGuesses(int userCount, string imageUrl, int usersWithEntryCount, string userName, string guessLocation)
        {
            var heroCard = new HeroCard
            {
                Title = $"Thanks {userName}",
                Subtitle = $"I'm saving your guess as {guessLocation}",
                Text = $"Still more results from users to come - {usersWithEntryCount} users have entered out of the {userCount} in this channel.",
                Images = new List<CardImage> { new CardImage(imageUrl) }
            };

            IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());

            Microsoft.Bot.Schema.Attachment attachment = heroCard.ToAttachment();
            return attachment;

        }

        public static Attachment Reminder(string imageUrl)
        {
            var heroCard = new HeroCard
            {
                Title = $"Don't forget to get your guess in",
                Text = $"There's just 3 1/2 hours remaining (depending on my maths!)",
                Images = new List<CardImage> { new CardImage(imageUrl) }
            };

            IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());

            Microsoft.Bot.Schema.Attachment attachment = heroCard.ToAttachment();
            return attachment;

        }

        public static Attachment ImageChosen(string imageUrl)
        {
            var heroCard = new HeroCard
            {
                Title = "The image has been chosen",
                Subtitle = $"Time to get your guesses in",
                Text = $"Reply with @BingBot and your guess. Results will come in when everyone has added a guess or at 16:00. Good luck!",
                Images = new List<CardImage> { new CardImage(imageUrl) }
            };
            IMessageActivity reply = MessageFactory.Attachment(new List<Attachment>());

            Microsoft.Bot.Schema.Attachment attachment = heroCard.ToAttachment();
            return attachment;

        }
    }
}
