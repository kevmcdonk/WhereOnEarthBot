using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhereOnEarthBot.Services;
using WhereOnEarthBot.Models;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace WhereOnEarthBot.Tests.Services
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
            DailyChallengeEntry entry = await service.GetLocationDetails("Nevada", null);
            Assert.IsTrue(entry.latitude != 0 && entry.longitude != 0);
        }
    }
}