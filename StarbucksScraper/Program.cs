using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using RestSharp;

namespace StarbucksScraper
{
    class Program
    {

        private static string _zipFilename = "zipCodes.txt";

        static void Main(string[] args)
        {

            try
            {
                // figure out where to start
                using (var db = new StoresEntities())
                {

                    var startingZip = db.Stores.Select(s => s.QueriedZipCode).Max();

                    if (startingZip == null)
                    {
                        Console.WriteLine("No existing data, starting with beginning zip.");
                    }
                    else
                    {
                        Console.WriteLine("Existing data present, starting with zip {0}", startingZip);
                    }

                    // first, read in our list of zip codes
                    var zips = File.ReadLines(_zipFilename)
                       .Skip(1)
                       .Select(line => line.Trim())
                       .Where(line => line != "")
                       .Select(line => line.Split('\t'))
                       .Select(tokens => new ZipCode { Code = tokens[0], Latitude = tokens[7], Longitude = tokens[8] })
                       .Where(zip => (startingZip == null || Int32.Parse(zip.Code) >= Int32.Parse(startingZip)))
                       .ToList();

                    // create our rest client
                    var client = new RestClient("https://openapi.starbucks.com/");
                    client.AddHandler("application/javascript", new RestSharp.Deserializers.JsonDeserializer());     // specify that we want our application/javascript deserialized as json

                    foreach (var zip in zips)
                    {

                        var offset = 0;
                        IRestResponse<StarbucksResults> response;
                        do
                        {
                            Console.WriteLine("Fetching zip {0} ({1}, {2}) stores at offset {3}", zip.Code, zip.Latitude, zip.Longitude, offset);

                            var request = new RestRequest("location/v1/stores", Method.GET);
                            request.AddParameter("apikey", "7b35m595vccu6spuuzu2rjh4");
                            request.AddParameter("callback", "");
                            request.AddParameter("radius", 100);
                            request.AddParameter("limit", 50);
                            request.AddParameter("ignore", "HoursNext7Days,today,extendedHours");

                            if (offset > 0)
                            {
                                request.AddParameter("offset", offset);
                            }

                            request.AddParameter("latLng", String.Format("{0},{1}", zip.Latitude, zip.Longitude));
                            //request.AddParameter("latLng", "34.8920374,-82.26919299999997");

                            var attempts = 0;
                            var threshold = 10;
                            do
                            {
                                if (attempts > 0)
                                {
                                    Console.WriteLine("Retrying...");
                                }

                                response = client.Execute<StarbucksResults>(request);
                                attempts++;
                            }
                            while (response != null && response.StatusCode != System.Net.HttpStatusCode.OK && attempts < threshold && response.ErrorMessage != "");

                            if (response == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                throw new Exception("Response was null or status code was invalid and we're out of retries.");
                            }

                            Console.WriteLine("Got {0} in response.", response.Data.Paging.Returned);

                            ProcessResults(response, zip.Code);

                            offset = offset + response.Data.Paging.Limit;
                        }
                        while ((response.Data.Paging.Offset == 0 && response.Data.Paging.Total > response.Data.Paging.Limit) || (response.Data.Paging.Offset > 0 && (response.Data.Paging.Total > (response.Data.Paging.Offset * response.Data.Paging.Limit))));
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            }

        }

        static void ProcessResults(IRestResponse<StarbucksResults> response, string zip)
        {
            using (var db = new StoresEntities())
            {
                foreach (var item in response.Data.Items)
                {

                    if (db.Stores.Select(s => s.StarbucksStoreID).Where(s => s == item.Store.Id).Count() > 0)
                    {
                        continue;
                    }

                    var store = db.Stores.Create();
                    store.BrandName = item.Store.BrandName;
                    store.City = item.Store.Address.City;
                    store.CountryCode = item.Store.Address.CountryCode;
                    store.CountrySubdivisionCode = item.Store.Address.CountrySubdivisionCode;
                    store.DistanceFrom = item.Distance;
                    store.Latitude = item.Store.Coordinates.Latitude;
                    store.Longitude = item.Store.Coordinates.Longitude;
                    store.Name = item.Store.Name;
                    store.OwnershipType = item.Store.OwnershipTypeCode;
                    store.PhoneNumber = item.Store.PhoneNumber;
                    store.PostalCode = item.Store.Address.PostalCode;
                    store.QueriedZipCode = zip;
                    store.StarbucksStoreID = item.Store.Id;
                    store.StoreNumber = item.Store.StoreNumber;
                    store.Street1 = item.Store.Address.StreetAddressLine1;
                    store.Street2 = item.Store.Address.StreetAddressLine2;
                    store.Street3 = item.Store.Address.StreetAddressLine3;
                    store.TZID = item.Store.TimeZoneInfo.WindowsTimeZoneId;
                    store.TZOffset = item.Store.TimeZoneInfo.CurrentTimeOffset;
                    store.TZOlsonID = item.Store.TimeZoneInfo.OlsonTimeZoneId;

                    // now for the collections
                    foreach (var feature in item.Store.Features)
                    {
                        var f = db.Features.Create();
                        f.Code = feature.Code;
                        f.Name = feature.Name;
                        //f.StoreID = store.Id;
                        f.Store = store;

                        store.Features.Add(f);
                    }

                    if (item.Store.RegularHours != null)
                    {
                        foreach (var day in new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
                        {
                            StarbucksResultsItemsStoreRegularHour d = item.Store.RegularHours.GetType().GetProperty(day).GetValue(item.Store.RegularHours) as StarbucksResultsItemsStoreRegularHour;

                            var hour = db.RegularHours.Create();
                            hour.Day = day;
                            hour.CloseTime = d.CloseTime;
                            hour.Open = d.Open;
                            hour.Open24Hours = d.Open24Hours;
                            hour.OpenTime = d.OpenTime;
                            hour.Store = store;

                            store.RegularHours.Add(hour);
                        }
                    }

                    // insert the store to generate an ID
                    db.Stores.Add(store);

                    Console.WriteLine("Adding Store ID {0}", store.StarbucksStoreID);

                }

                try
                {
                    var wrote = db.SaveChanges();

                    Console.WriteLine("Wrote {0} items", wrote);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

        }


    }

    class ZipCode
    {
        public string Code;
        public string Latitude;
        public string Longitude;
    }

    class StarbucksResults
    {
        public StarbucksResultsPaging Paging { get; set; }
        public List<StarbucksResultsItems> Items { get; set; }
    }

    class StarbucksResultsPaging
    {
        public int Total { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public int Returned { get; set; }
    }

    class StarbucksResultsItems
    {
        public float Distance { get; set; }
        public StarbucksResultsItemsStore Store { get; set; }
    }

    class StarbucksResultsItemsStore
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BrandName { get; set; }
        public string StoreNumber { get; set; }
        public string PhoneNumber { get; set; }
        public string OwnershipTypeCode { get; set; }
        public StarbucksResultsItemsStoreAddress Address { get; set; }
        public StarbucksResultsItemsStoreCoordinates Coordinates { get; set; }
        public List<StarbucksResultsItemsStoreFeature> Features { get; set; }
        public StarbucksResultsItemsStoreTimeZoneInfo TimeZoneInfo { get; set; }
        public StarbucksResultsItemsStoreRegularHours RegularHours { get; set; }
    }

    class StarbucksResultsItemsStoreAddress
    {
        public string StreetAddressLine1 { get; set; }
        public string StreetAddressLine2 { get; set; }
        public string StreetAddressLine3 { get; set; }
        public string City { get; set; }
        public string CountrySubdivisionCode { get; set; }
        public string CountryCode { get; set; }
        public string PostalCode { get; set; }
    }

    class StarbucksResultsItemsStoreCoordinates
    {
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }

    class StarbucksResultsItemsStoreFeature
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    class StarbucksResultsItemsStoreTimeZoneInfo
    {
        public int CurrentTimeOffset { get; set; }
        public string WindowsTimeZoneId { get; set; }
        public string OlsonTimeZoneId { get; set; }
    }

    class StarbucksResultsItemsStoreRegularHours
    {
        public StarbucksResultsItemsStoreRegularHour Monday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Tuesday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Wednesday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Thursday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Friday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Saturday { get; set; }
        public StarbucksResultsItemsStoreRegularHour Sunday { get; set; }
        public bool Open24x7 { get; set; }
    }

    class StarbucksResultsItemsStoreRegularHour
    {
        public bool Open { get; set; }
        public bool Open24Hours { get; set; }
        public string OpenTime { get; set; }
        public string CloseTime { get; set; }
    }


}
