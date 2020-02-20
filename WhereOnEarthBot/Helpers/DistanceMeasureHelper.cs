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
            double distanceFromResult = Math.Acos(
                (Math.Sin(guessLongitude * magic) * Math.Sin(actualLongitude * magic))
                + (Math.Cos(guessLongitude * magic) * Math.Cos(actualLongitude * magic) * Math.Cos((guessLatitude * magic) - (actualLatitude * magic)))
                ) * radius_km;
     
            return distanceFromResult;
        }
    }
}
