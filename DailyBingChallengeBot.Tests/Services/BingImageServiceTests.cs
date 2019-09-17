using Microsoft.VisualStudio.TestTools.UnitTesting;
using DailyBingChallengeBot.Services;
using DailyBingChallengeBot.Models;

namespace DailyBingChallengeBot.Tests.Services
{
    [TestClass]
    public class BingImageServiceTests
    {
        [TestMethod]
        public void GetBingImageUrlTest()
        {
            BingImageService service = new BingImageService();
            DailyBingImage image = service.GetBingImageUrl(1);
            Assert.IsTrue(!string.IsNullOrEmpty(image.ImageText) && !string.IsNullOrEmpty(image.ImageRegion));
        }
    }
}