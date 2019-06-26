using System;
using System.Collections.Generic;
using System.Text;

namespace DailyBingChallengeBot.Models
{
    public enum ImageSource
    {
        Bing,
        Google
    }

    public class DailyBingInfo
    {
        public int currentImageIndex { get; set; }
        public ImageSource currentSource { get; set; }

        public List<DailyBingUser> users { get; set; }
    }
}
