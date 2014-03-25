using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using RestSharp;
using StarbucksScraper;
using Newtonsoft.Json;

namespace SocrataUploader2
{
    class Program
    {

        private static string _host;
        private static string _datasetId;
        private static string _username;
        private static string _password;
        private static string _appToken;

        private static RestClient _client;

        static void Main(string[] args)
        {

            _host = ConfigurationManager.AppSettings["SocrataHost"];
            _datasetId = ConfigurationManager.AppSettings["SocrataDatasetID"];
            _username = ConfigurationManager.AppSettings["SocrataUsername"];
            _password = ConfigurationManager.AppSettings["SocrataPassword"];
            _appToken = ConfigurationManager.AppSettings["SocrataAppToken"];

            CreateClient();

            Upload();

            Console.WriteLine("Complete");
            Console.ReadKey();
        }

        private static string CreateWorkingCopy()
        {

            string resultId = "";

            var request = new RestRequest("views/{dataset}/publication.json", Method.POST);
            request.AddUrlSegment("dataset", _datasetId);
            request.AddParameter("method", "copy", ParameterType.QueryString);     // since it's a POST we have to make sure this gets used as a Query String, not POST body

            IRestResponse<DataSetMeta> response;
            var attempts = 1;
            var threshold = 30;     // sometimes this can take a VERY long time
            var waitTime = 5;
            do
            {
                response = _client.Execute<DataSetMeta>(request);

                if (response.Data.status == null)
                {
                    resultId = response.Data.id;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    // wait for a few seconds between requests so we don't max out attempts
                    Console.WriteLine("Working copy is {0}. Waiting for {1} seconds. Attempt {2} of {3}.", response.Data.status, waitTime, attempts, threshold);
                    System.Threading.Thread.Sleep(waitTime * 1000);
                }

                attempts++;
            }
            while (response.StatusCode == System.Net.HttpStatusCode.Accepted && response.ErrorMessage == null && attempts < threshold);

            if (attempts > threshold)
            {
                Console.WriteLine("Working copy could not be created in {0} attempts!", attempts);
            }

            if (resultId == "")
            {
                throw new CreateWorkingCopyException("Unable to retrieve working copy dataset ID");
            }

            return resultId;

        }

        public static bool TruncateWorkingCopy(string datasetId)
        {

            var request = new RestRequest("resource/{dataset}", Method.DELETE);
            request.AddUrlSegment("dataset", datasetId);

            var response = _client.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.ErrorMessage == null)
            {
                return true;
            }

            return false;
        }

        private static void Upload()
        {

            Console.Write("Creating Working Copy...");

            // wait on the long-running create operation and get back the new working copy dataset's id
            var datasetId = CreateWorkingCopy();

            Console.WriteLine(" {0}", datasetId);

            Console.WriteLine("Truncating Working Copy...");

            var truncate = TruncateWorkingCopy(datasetId);

            var batchSize = 2000;

            using (var db = new StoresEntities())
            {

                LinkedList<Dictionary<string, object>> rows = new LinkedList<Dictionary<string, object>>();

                var mostRecent = db.Stores.Select(s => s.LastSeen).Max(s => (DateTime?)s);

                var mostRecentStores = db.Stores.Where(s => s.LastSeen == mostRecent);

                Console.WriteLine("Got {0} stores to upload for {1} batch", mostRecentStores.Count(), mostRecent.ToString());

                foreach (var store in mostRecentStores)
                {
                    List<string> streetPieces = new List<string>();
                    string streetCombined;

                    if (store.Street1 != null && store.Street1.Trim() != "")
                    {
                        streetPieces.Add(store.Street1.Trim());
                    }

                    if (store.Street2 != null && store.Street2.Trim() != "")
                    {
                        streetPieces.Add(store.Street2.Trim());
                    }

                    if (store.Street3 != null && store.Street3.Trim() != "")
                    {
                        streetPieces.Add(store.Street3.Trim());
                    }

                    streetCombined = String.Join(", ", streetPieces);

                    var row = new Dictionary<string, object>();
                    row.Add("Store ID", store.StarbucksStoreID);
                    row.Add("Name", store.Name);
                    row.Add("Brand", store.BrandName);
                    row.Add("Store Number", store.StoreNumber);
                    row.Add("Phone Number", store.PhoneNumber);
                    row.Add("Ownership Type", store.OwnershipType);
                    row.Add("Street Combined", streetCombined);
                    row.Add("Street 1", store.Street1);
                    row.Add("Street 2", store.Street2);
                    row.Add("Street 3", store.Street3);
                    row.Add("City", store.City);
                    row.Add("Country Subdivision", store.CountrySubdivisionCode);
                    row.Add("Country", store.CountryCode);
                    row.Add("Postal Code", store.PostalCode);
                    row.Add("Latitude", store.Latitude);
                    row.Add("Longitude", store.Longitude);
                    row.Add("Timezone", store.TZID);
                    row.Add("Current Timezone Offset", store.TZOffset);
                    row.Add("Olson Timezone", store.TZOlsonID);
                    row.Add("First Seen", store.FirstSeen.ToUniversalTime());

                    // if we add the coordinates when we don't have them, the rows don't error, but magically disappear... fucking socrata
                    if (store.Latitude != null && store.Longitude != null)
                    {
                        row.Add("Coordinates", String.Format("({0}, {1})", store.Latitude, store.Longitude));
                    }

                    /*
                    row = new Dictionary<string, object>();
                    row.Add("ID", new System.Random().Next());
                    row.Add("Name", "Foo");
                    */

                    rows.AddLast(row);

                    //Console.WriteLine("Added store {0}", store.StarbucksStoreID);

                    if (rows.Count >= batchSize)
                    {
                        DateTime startTime = DateTime.UtcNow;
                        Console.Write("Upserting batch of {0}", rows.Count);
                        var result = Upsert(datasetId, rows);

                        Console.WriteLine(" {0} rows / second", Math.Round((rows.Count / (DateTime.UtcNow - startTime).TotalSeconds), 3));
                        rows.Clear();

                    }
                }

                if (rows.Count > 0)
                {
                    Console.WriteLine("Upserting last batch of {0}", rows.Count);
                    var result = Upsert(datasetId, rows);
                    rows.Clear();
                }

            }

            Console.WriteLine("Publishing working copy {0}", datasetId);
            PublishDataSet(datasetId);

        }

        private static bool PublishDataSet(string datasetId)
        {

            var request = new RestRequest("views/{dataset}/publication.json", Method.POST);
            request.AddUrlSegment("dataset", datasetId);

            var response = _client.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.ErrorMessage == null)
            {
                return true;
            }

            return false;

        }

        private static bool Upsert(string datasetId, object rows)
        {

            var request = new RestRequest("resource/{dataset}.json", Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddUrlSegment("dataset", datasetId);

            // add our post body
            request.AddBody(rows);

            var response = _client.Execute<Dictionary<string,int>>(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.ErrorMessage == null)
            {
                return true;
            }
            else if (response.ErrorMessage != null && response.ErrorMessage != ""){
                throw new SocrataException(String.Format("Unable to upsert batch: {0}", response.ErrorMessage), response.ErrorException);
            }
            else if (response.Data["Errors"] > 0)
            {
                throw new SocrataException(String.Format("Unable to Upsert Batch. {0} record(s) threw an error.", response.Data["Errors"]));
            }
            else if (response.Data["Rows Created"] != (rows as LinkedList<Dictionary<string, object>>).Count)
            {
                throw new SocrataException("There were fewer rows created than we sent!");
            }
            else
            {
                throw new SocrataException(String.Format("Unable to Upsert batch: {0}", response.ErrorMessage), response.ErrorException);
            }

            return false;

        }

        private static void CreateClient()
        {

            _client = new RestClient("https://opendata.socrata.com/api/");
            _client.AddHandler("application/javascript", new RestSharp.Deserializers.JsonDeserializer());     // specify that we want our application/javascript deserialized as json

            // add all our headers
            _client.AddDefaultHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(_username + ":" + _password)));
            _client.AddDefaultHeader("X-App-Token", _appToken);

        }
    }

    public class DataSetMeta
    {
        public string id { get; set; }
        public string name { get; set; }
        public int averageRating { get; set; }
        public int createdAt { get; set; }
        public string displayType { get; set; }
        public bool newBackend { get; set; }
        public int numberOfComments { get; set; }
        public int oid { get; set; }
        public bool publicationAppendEnabled { get; set; }
        public int publicationGroup { get; set; }
        public string publicationStage { get; set; }
        public int rowsUpdatedAt { get; set; }
        public string rowsUpdatedBy { get; set; }
        public bool signed { get; set; }
        public int tableId { get; set; }
        public int totalTimesRated { get; set; }
        public int viewLastModified { get; set; }
        public string viewType { get; set; }

        public MetaData metadata { get; set; }
        public User owner { get; set; }
        public List<string> rights { get; set; }
        public User tableAUthor { get; set; }

        public List<string> flags { get; set; }

        public string status { get; set; }
        public string uid { get; set; }
    }

    public class MetaData
    {
        public RenderTypeConfig renderTypeConfig { get; set; }
        public List<string> availableDisplayTypes { get; set; }
    }

    public class RenderTypeConfig
    {
        public Visible visible { get; set; }
    }

    public class Visible
    {
        public bool table { get; set; }
    }

    public class User
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public bool emailUnsubscribed { get; set; }
        public string profileImageUrlLarge { get; set; }
        public string profileImageUrlMedium { get; set; }
        public string profileImageUrlSmall { get; set; }
        public int profileLastModified { get; set; }
        public string screenName { get; set; }
    }

    [Serializable]
    public class SocrataException : Exception
    {
        public SocrataException(string message) : base(message) { }
        public SocrataException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        public SocrataException(string message, Exception innerException) : base(message, innerException) { }

    }
    [Serializable]
    public class CreateWorkingCopyException : SocrataException
    {
        public CreateWorkingCopyException(string message) : base(message) { }
        public CreateWorkingCopyException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        public CreateWorkingCopyException(string message, Exception innerException) : base(message, innerException) { }
    }

}
