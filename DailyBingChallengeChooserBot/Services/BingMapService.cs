using BingMapsRESTToolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using DailyBingChallengeBot.Models;

namespace DailyBingChallengeBot.Services
{
    public class BingMapService
    {
        public string BingMapsKey;

        public BingMapService(string bingMapsKey)
        {
            BingMapsKey = bingMapsKey;
        }

        public async Task<DailyBingEntry> GetLocationDetails(string locationQueryText)
        {
            try
            {
                //Create a request.
                var request = new GeocodeRequest()
                {
                    Query = locationQueryText,
                    IncludeIso2 = true,
                    IncludeNeighborhood = true,
                    MaxResults = 25,
                    BingMapsKey = BingMapsKey
                };

                //Process the request by using the ServiceManager.
                var response = await request.Execute();

                if (response != null &&
                    response.ResourceSets != null &&
                    response.ResourceSets.Length > 0 &&
                    response.ResourceSets[0].Resources != null &&
                    response.ResourceSets[0].Resources.Length > 0)
                {
                    var locationResult = response.ResourceSets[0].Resources[0] as BingMapsRESTToolkit.Location;
                    DailyBingEntry entry = new DailyBingEntry()
                    {
                        BingResponse = locationResult.Name,
                        latitude = float.Parse(locationResult.Point.Coordinates[0].ToString()),
                        longitude = float.Parse(locationResult.Point.Coordinates[1].ToString())
                    };

                    return entry;
                }
                throw new Exception("Location not found");
            }
            catch (Exception exp)
            {
                Console.WriteLine("Grrr error: " + exp.Message);
                return null;
            }
        }
    }
}