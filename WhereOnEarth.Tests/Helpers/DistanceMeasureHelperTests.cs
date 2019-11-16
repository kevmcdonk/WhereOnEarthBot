using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhereOnEarthBot.Services;
using WhereOnEarthBot.Models;
using WhereOnEarthBot.Helpers;

namespace WhereOnEarthBot.Tests.Services
{
    [TestClass]
    public class DistanceMeasureHelperTests
    {
        public const double LondonLatitude = 51.504749217911524;
        public const double LondonLongitude  = -0.12720999999999205;
        public const double GlasgowLatitude = 55.86377967940192;
        public const double GlasgowLongitude = -4.303298127441407;
        public const double CanberraLatitude = -35.288482678102945;
        public const double CanberraLongitude = 149.00506374511716;
        public const double SanFranLatitude = 37.79653798062841;
        public const double SanFranLongitude = -122.55053625488281;

        [TestMethod]
        public void GetDistanceFromResult_LondonGlasgow()
        {
            var distance = DistanceMeasureHelper.GetDistanceFromResult((float)LondonLatitude, (float)LondonLongitude, GlasgowLatitude, GlasgowLongitude);
            Assert.IsTrue(distance > 550);
            Assert.IsTrue(distance < 560);
        }

        [TestMethod]
        public void GetDistanceFromResult_SanFranCanberra()
        {
            var distance = DistanceMeasureHelper.GetDistanceFromResult((float)SanFranLatitude, (float)SanFranLongitude, CanberraLatitude, CanberraLongitude);
            Assert.IsTrue(distance > 12100);
            Assert.IsTrue(distance < 12300);
        }

        [TestMethod]
        public void GetDistanceFromResult_LondonCanberra()
        {
            var distance = DistanceMeasureHelper.GetDistanceFromResult((float)LondonLatitude, (float)LondonLongitude, CanberraLatitude, CanberraLongitude);
            Assert.IsTrue(distance > 16900);
            Assert.IsTrue(distance < 17100);
        }
    }
}