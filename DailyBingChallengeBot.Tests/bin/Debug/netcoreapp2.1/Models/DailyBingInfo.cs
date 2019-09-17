using Microsoft.Azure.Cosmos.Table;
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

    public class DailyBingInfo : CustomSerializationTableEntity
    {
        public int currentImageIndex { get; set; }
        [NotSerialized]
        public ImageSource currentSource { get; set; }
        public string serializableImageSource { get; set; }
        [NotSerialized]
        public List<DailyBingUser> users { get; set; }
        public string SerializedUsers { get; set; }
    }
}
