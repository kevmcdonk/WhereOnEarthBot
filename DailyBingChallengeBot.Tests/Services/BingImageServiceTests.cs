using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhereOnEarthBot.Services;
using WhereOnEarthBot.Models;

namespace WhereOnEarthBot.Tests.Services
{
    [TestClass]
    public class BingImageServiceTests
    {
        [TestMethod]
        public void GetBingImageUrlTest()
        {
            BingImageService service = new BingImageService();
            DailyChallengeImage image = service.GetBingImageUrl(1);
            Assert.IsTrue(!string.IsNullOrEmpty(image.ImageText) && !string.IsNullOrEmpty(image.ImageRegion));
        }
    }
}