// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;

namespace TrafficFunction
{
    public static class Function1
    {

        public class JsonObject
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Published { get; set; }
            public string Location { get; set; }
            public string Email { get; set; }
        }


        public static List<string> ConnectToSql(string city)
        {
            var emails = new List<string>();
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = "trafficserverswe.database.windows.net";
                builder.UserID = "TeamSWE";
                builder.Password = "Qwerty123!";
                builder.InitialCatalog = "TrafficInformation";

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {

                    String sql = $"SELECT AspNetUsers.Email,Locations.Name FROM UserLocation JOIN AspNetUsers ON UserLocation.UserId = AspNetUsers.Id JOIN Locations ON UserLocation.LocationId = Locations.Id WHERE Locations.Name = '{city}';";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine(reader.GetString(0));
                                emails.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }
            return emails;

        }




        [FunctionName("Function1")]
        public static async Task RunAsync([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {


            var emails = ConnectToSql(eventGridEvent.Subject);

            JsonObject jsonO = new JsonObject()
            {
                Id = eventGridEvent.Id,
                Title = eventGridEvent.EventType,
                Summary = eventGridEvent.Data.ToString(),
                Published = eventGridEvent.EventTime.ToString(),
                Location = eventGridEvent.Subject
            };

            foreach (var email in emails)
            {
                var opt = new JsonSerializerOptions() { WriteIndented = true };
                string strJson = JsonSerializer.Serialize<JsonObject>(jsonO, opt);


                jsonO.Email = email;
                using (var client = new HttpClient())
                {
                    var content = new StringContent(strJson);
                    content.Headers.ContentType.CharSet = string.Empty;
                    content.Headers.ContentType.MediaType = "application/json";
                    var response = await client.PostAsync("https://prod-245.westeurope.logic.azure.com:443/workflows/7d519f0b2bb64b7085e4ca049bd5fa92/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=sjwqnnOW4g8_THo8MR1yL9uthb8yXorbDzAc0hK0HhE", content);
                }
                log.LogInformation(eventGridEvent.Data.ToString());
                Thread.Sleep(2000);
            }
        }
    }
}
