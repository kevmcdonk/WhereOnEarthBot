using Microsoft.VisualStudio.TestTools.UnitTesting;
using DailyBingChallengeBot.Services;
using DailyBingChallengeBot.Models;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace DailyBingChallengeBot.Tests.Services
{
    [TestClass]
    public class BingMapServiceTests
    {
        IConfiguration Configuration { get; set; }

        public BingMapServiceTests()
        {
            var builder = new ConfigurationBuilder()
                            .AddUserSecrets<BingMapServiceTests>();

            Configuration = builder.Build();
        }

        [TestMethod]
        public async Task GetBingImageUrlTest()
        {
            string bingMapsKey = Configuration["BingMapsAPI"];
            System.Console.WriteLine("BingMapsKey: " + bingMapsKey);
            BingMapService service = new BingMapService(bingMapsKey);
            DailyBingEntry entry = await service.GetLocationDetails("Nevada");
            Assert.IsTrue(entry.latitude != 0 && entry.longitude != 0);
        }
    }
}