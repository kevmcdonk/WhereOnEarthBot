# WhereOnEarthBot
| [Documentation](https://github.com/kevmcdonk/WhereOnEarthBot/wiki) | [Deployment guide](https://github.com/kevmcdonk/WhereOnEarthBot/Deployment-guide) | [Architecture](https://github.com/kevmcdonk/WhereOnEarthBot/Solution-overview) |
| ---- | ---- | ---- |

Have you ever looked at the daily background on Bing or the version on your desktop? Did you stop and wonder where that beautiful landscape or haunting castle is? Perhaps you turned to a colleague and said “I wonder where that is? Where do you think”. Then you did the same the next day and perhaps another colleauge joined in. This then turned in to a game amongst the team. Then it got bigger and bigger and bigger and soon the whole company was playing. Suddenly, a little bit of fun was taking up loads of time for the poor person who first came up with the idea.

This little tale inspired me to take a look at making a Bot in Teams that could do the same thing but with more automation. What would a Bot look like that could ask someone to choose an image and then send it round to the team, allow them to guess where it is, work out which guess was closest and then set a winner. Well, you will be happy to know that I can tell you now. It looks like the Daily Bing Challenge Bot.

To trigger the challenge, someone just needs to @mention the @BingBot and say Check Daily Results. This wakes up the Bot to choose the first Bing image. I say first because there are actually seven different regions I have found for the Bing images and will they are sometimes the same, they can often be different images. As you can see in the image above, an adaptive card shows the Bing picture with the region it is in. There are then three actions:

Choose image - this tells the Bot that you are happy with that image and the game can be triggered
Try another image - if that one doesn’t quite work for you, try the next Bing image Bot Demo Two
Switch to Google - let’s cover this in a bit more detail
Adding a little Google Places
We found that many of the Bing images were a little too easy to guess so I added an alternative using Google. The Bot would pick a random number for longitude and latitude and then look for any places with photos in a 50km radius (the maximum that Google can choose). If it didn’t find anywhere (e.g. somewhere in the middle of the ocean), then it would try another random point. This would happen forty times before it confessed to the user that it couldn’t find anywhere. Then it’s up to the user to try again or switch back to Bing.

Time to guess
Once a suitable image has been chosen, the Bot will save the image details and then start asking for guesses.

Bot Demo Three

As you can see in the image above, the Bot knows how many users are in the Team and how many have responded so far. The guess is validated as a real location and it can handle slight typos as well e.g. Lundun for London or Rio de Janeero for Rio de Janeiro.

Choosing a winner
Once all the results are in, the winner is calculated and a new card is shown with the winner. If you don’t want to wait for all the guesses to come in (for example, someone is on leave) then you can reply “Check results” to trigger the results checking.

Bot Demo Four

What next?
So the winner has been told and everyone knows where the actual photo was and how far the guess was. The challenge is complete for the day. So what does that mean for the longer term? Time for some Power BI.

Bot Demo Five

To drive some competitiveness, a Power BI Report is set up and hosted as a tab in Teams. It shows the top guessers as well as a map of the last 30 days of results. The team can keep a track of their results and the most successful winners over time.

Driving adoption for Microsoft Teams
Most importantly though, they can use Microsoft Teams to do it every day. The Team members don’t just have to chat to the Bot, they can chat to each other, laugh at bad guesses or just generally be impressed by the quality of the photo. Then they can start discussing their projects in another channel, adding their own Power BI tabs for project status or other tabs. Driving adoption of Teams through experience.
