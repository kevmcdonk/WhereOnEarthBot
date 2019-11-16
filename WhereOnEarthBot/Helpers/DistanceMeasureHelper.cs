using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WhereOnEarthBot.Helpers
{
    public static class DistanceMeasureHelper
    {
        public static double GetDistanceFromResult(float guessLatitude, float guessLongitude, double actualLatitude, double actualLongitude)
        {
            double magic = Math.PI / 180;
            double radius_km = 6367.4445;
            double distanceFromResult = Math.Acos(Math.Sin(guessLatitude * magic)
                * Math.Sin(actualLatitude * magic)
                + Math.Cos(guessLatitude * magic)
                * Math.Cos(actualLatitude * magic)
                * Math.Cos(guessLongitude * magic - actualLongitude * magic)) * radius_km;

            return distanceFromResult;
        }
    }
}
