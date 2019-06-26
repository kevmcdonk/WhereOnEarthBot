using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DailyBingChallengeBot.Models
{
    public class Place
    {
        [JsonProperty("name")]
        public string Name { get; set; }        // Name
        [JsonProperty("vicinity")]
        public string Address { get; set; }     // Address
        [JsonProperty("place_id")]
        public string PlaceId { get; set; }     //Place Id
        [JsonProperty("types")]
        public string[] Types { get; set; }     // Categories
        [JsonProperty("opening_hours")]
        public Opening Opened { get; set; }     // Returns null if unavaiable
        [JsonProperty("geometry")]
        public Geometry Geo { get; set; }       // Co-ordinates
        [JsonProperty("photos")]
        public List<Photo> Photos { get; set; }
    }

    public class Photo
    {
        [JsonProperty("photo_reference")]
        public string PhotoReference { get; set; }        // Name
        [JsonProperty("width")]
        public int Rating { get; set; }              // Rating
        [JsonProperty("height")]
        public int Height { get; set; }
    }


    public class Detail
    {
        [JsonProperty("name")]
        public string Name { get; set; }        // Name
        [JsonProperty("rating")]
        public decimal Rating = -5;             // Rating
        [JsonProperty("price_level")]
        public int Price = -5;                  // Price Rating
        [JsonProperty("formatted_address")]
        public string Address { get; set; }     // Address
        [JsonProperty("formatted_phone_number")]
        public string Phone { get; set; }       // Phone Number
        [JsonProperty("opening_hours")]
        public Opening Open { get; set; }       // Business Hours
    }

    public class Opening
    {
        [JsonProperty("open_now")]
        public bool Now = false;                // Currently open
        [JsonProperty("periods")]
        public Period[] Periods { get; set; }   // Opened time frames
    }

    public class Period
    {
        [JsonProperty("open")]
        public Range Open { get; set; }         // Opening time
        [JsonProperty("close")]
        public Range Close { get; set; }        // Closing time
    }

    public class Range
    {
        [JsonProperty("day")]
        public int Day { get; set; }
        [JsonProperty("time")]
        public short Time { get; set; }

        public static DateTime ParseTime(int day, short s)
        {
            DateTime dt = DateTime.Today.AddDays(day - (int)DateTime.Today.DayOfWeek);
            return new DateTime(dt.Year, dt.Month, dt.Day, s / 100, s % 100, 0);
        }
    }

    public class Geometry
    {
        [JsonProperty("location")]
        public Location Location { get; set; }
    }

    public class Location
    {
        [JsonProperty("lat")]
        public double Latitude { get; set; }
        [JsonProperty("lng")]
        public double Longitude { get; set; }
    }

    public class MapResponse
    {
        [JsonProperty("result")]
        public Detail Detail { get; set; }
        [JsonProperty("results")]
        public List<Place> Places { get; set; }
        [JsonProperty("next_page_token")]
        public string Next { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
