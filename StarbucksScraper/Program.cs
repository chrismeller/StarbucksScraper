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

        private static int _limit = 50;
        private static DateTime _lastSeen;

        static void Main(string[] args)
        {

            // we're going to mark all the records in the DB as being last seen as of today to keep track of historical values -- round it to today to account for incomplete runs on the same day
            _lastSeen = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc);

            // figure out where to start
            using (var db = new StoresEntities())
            {

                var currentEntries = db.Stores.Select(s => s.LastSeen).Where(s => s == _lastSeen).Count();
                var offset = 0;

                if (currentEntries == 0)
                {
                    Console.WriteLine("No existing data, starting from scratch.");
                }
                else
                {
                    // let's add a little overlap there, just to be safe.
                    offset = currentEntries - _limit;
                    Console.WriteLine("Existing data present, starting with {0} current stores, offset {1}.", currentEntries, offset);
                }

                // create our rest client
                var client = new RestClient("https://openapi.starbucks.com/");
                client.AddHandler("application/javascript", new RestSharp.Deserializers.JsonDeserializer());     // specify that we want our application/javascript deserialized as json

                client.Timeout = (10 * 1000);   // 10 seconds


                IRestResponse<StarbucksResults> response;
                do
                {
                    Console.WriteLine("Fetching page at offset {0}", offset);

                    var request = new RestRequest("location/v1/stores", Method.GET);
                    request.AddParameter("apikey", "7b35m595vccu6spuuzu2rjh4");
                    request.AddParameter("callback", "");
                    request.AddParameter("limit", _limit);
                    request.AddParameter("ignore", "HoursNext7Days,today,extendedHours");

                    if (offset > 0)
                    {
                        request.AddParameter("offset", offset);
                    }

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
                    while (response != null && response.Data != null && response.StatusCode != System.Net.HttpStatusCode.OK && attempts < threshold && response.ErrorMessage != "");

                    if (response == null || response.Data == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        throw new Exception("Response was null, we couldn't decode the result, or status code was invalid and we're out of retries.");
                    }

                    Console.WriteLine("Got {0} in response.", response.Data.Paging.Returned);

                    ProcessResults(response);

                    // sleep for half a second to avoid what may be rate throttling on the api
                    System.Threading.Thread.Sleep(500);

                    offset = offset + response.Data.Paging.Limit;
                }
                while ((response.Data.Paging.Offset == 0 && response.Data.Paging.Total > response.Data.Paging.Limit) || (response.Data.Paging.Offset > 0 && (response.Data.Paging.Total > (response.Data.Paging.Offset + response.Data.Paging.Limit))));

                Console.Write("Complete");
                Console.ReadKey();

            }

        }

        static void ProcessResults(IRestResponse<StarbucksResults> response)
        {
            using (var db = new StoresEntities())
            {
                foreach (var item in response.Data.Items)
                {

                    // check to see if the store we're looking for already exists
                    var existing = db.Stores.Where(s => s.StarbucksStoreID == item.Id).FirstOrDefault();
                    Store store;

                    // if it exists, we want to update the existing object, rather than creating a new one
                    if (existing != null)
                    {
                        store = existing;
                    }
                    else
                    {
                        // otherwise, we want to create a new store to insert
                        store = db.Stores.Create();
                    }

                    store.BrandName = item.BrandName;
                    store.City = item.Address.City;
                    store.CountryCode = item.Address.CountryCode;
                    store.CountrySubdivisionCode = item.Address.CountrySubdivisionCode;
                    store.Name = item.Name;
                    store.OwnershipType = item.OwnershipTypeCode;
                    store.PhoneNumber = item.PhoneNumber;
                    store.PostalCode = item.Address.PostalCode;
                    store.StarbucksStoreID = item.Id;
                    store.StoreNumber = item.StoreNumber;
                    store.Street1 = item.Address.StreetAddressLine1;
                    store.Street2 = item.Address.StreetAddressLine2;
                    store.Street3 = item.Address.StreetAddressLine3;
                    store.TZID = item.TimeZoneInfo.WindowsTimeZoneId;
                    store.TZOffset = item.TimeZoneInfo.CurrentTimeOffset;
                    store.TZOlsonID = item.TimeZoneInfo.OlsonTimeZoneId;
                    store.LastSeen = _lastSeen;
                    
                    // only include the first seen date if this is a new entry
                    if (existing == null)
                    {
                        store.FirstSeen = _lastSeen;
                    }

                    // for a handful of stores (1 so far) the coordinates may actually be null
                    if (item.Coordinates != null)
                    {
                        store.Latitude = item.Coordinates.Latitude;
                        store.Longitude = item.Coordinates.Longitude;
                    }

                    // we don't have the feature and hours updating down yet, so just ignore those updates if we got an existing store
                    if (existing == null)
                    {

                        // now for the collections
                        foreach (var feature in item.Features)
                        {
                            var f = db.Features.Create();
                            f.Code = feature.Code;
                            f.Name = feature.Name;
                            //f.StoreID = store.Id;
                            f.Store = store;

                            store.Features.Add(f);
                        }

                        if (item.RegularHours != null)
                        {
                            foreach (var day in new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
                            {
                                StarbucksResultsItemsStoreRegularHour d = item.RegularHours.GetType().GetProperty(day).GetValue(item.RegularHours) as StarbucksResultsItemsStoreRegularHour;

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

                    }

                    // we only need to add the store as a new element if this is not an update
                    if (existing == null)
                    {
                        // insert the store to generate an ID
                        db.Stores.Add(store);

                        Console.WriteLine("Adding Store ID {0}", store.StarbucksStoreID);
                    }
                    else
                    {
                        Console.WriteLine("Updating Store ID {0}", store.StarbucksStoreID);
                    }

                }

                try
                {
                    var wrote = db.SaveChanges();

                    Console.WriteLine("Wrote {0} items", wrote);
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException e_validation)
                {
                    foreach (var error in e_validation.EntityValidationErrors)
                    {
                        Console.Write("Validation Errors:");
                        foreach (var validation_error in error.ValidationErrors)
                        {
                            Console.WriteLine(validation_error.ErrorMessage);
                        }

                        Console.WriteLine("Entity:");
                        foreach (var key in error.Entry.CurrentValues.PropertyNames)
                        {
                            Console.WriteLine(key + ": " + error.Entry.CurrentValues[key]);
                        }
                    }
                    throw;
                }
                catch (Exception e)
                {
                    throw;
                    Console.WriteLine(e.ToString());
                }
            }

        }


    }

    class StarbucksResults
    {
        public StarbucksResultsPaging Paging { get; set; }
        public List<StarbucksResultsItemsStore> Items { get; set; }
    }

    class StarbucksResultsPaging
    {
        public int Total { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public int Returned { get; set; }
    }

    // this class used to represent an item, which was a combination of distance from the queried point and a store object. without searching for a certain point and radius, the structure changes
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
