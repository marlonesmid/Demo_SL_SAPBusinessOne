using code_queries_sl.Model;
using code_queries_sl.Model.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;

namespace code_queries_sl
{
    internal class Program
    {
        private static string baseUrl = "http://localhost:50001/b1s/v1"; // Replace with your SAP Service Layer base URL


        static void Main(string[] args)
        {
            var sessionId = Login();

            if (!string.IsNullOrEmpty(sessionId))
            {
                CreateQuery(sessionId, "GetCardCodeData", "select cardcode, cardname, balance from ocrd where cardcode = :cardcode"); // :cardcode is your parameter name
                CallQuery(sessionId, "GetCardCodeData", "cardcode", "'C20000'");
                RemoveQuery(sessionId, "GetCardCodeData");
                Logout(sessionId);
            }
            else
            {
                Console.WriteLine("Login failed.");
            }

            Console.ReadLine();
        }

        private static string Login()
        {
            // Request details
            string url = $"{baseUrl}/Login";
            LoginRequest loginRequest = new LoginRequest()
            {
                UserName = "userName",
                Password = "password",
                CompanyDB = "SBODemoUS"
            };

            // Serialize request body to JSON
            string jsonRequestBody = JsonConvert.SerializeObject(loginRequest);

            // Make the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.KeepAlive = true;
            httpWebRequest.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            httpWebRequest.ServicePoint.Expect100Continue = false;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(jsonRequestBody);
            }

            try
            {
                // Call Service Layer
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    // Deserialize success response
                    var responseInstance = JsonConvert.DeserializeObject<LoginResponse>(result);

                    Console.WriteLine("Logged in successfully.");

                    return responseInstance.SessionId;
                }
            }
            catch (Exception ex)
            {
                // Unauthorized, etc.
                Console.WriteLine("Unexpected: " + ex.Message);
            }

            return null;
        }

        private static void CreateQuery(string sessionId, string queryId, string queryText)
        {
            string postUrl = $"{baseUrl}/SQLQueries";

            // Create a JSON payload for the new Query
            JObject queryData = new JObject
            {
                { "SqlCode", queryId }, // Provide a unique query code
                { "SqlName", "whatever" },
                { "SqlText", queryText }
            };

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(postUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(queryData.ToString());
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    var result = "";
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        result = streamReader.ReadToEnd();
                    }

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        Console.WriteLine("Query created successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create Query. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while creating the Query: " + ex.Message);
            }
        }

        private static void CallQuery(string sessionId, string queryId, params string[] parameters)
        {
            string callUrl = $"{baseUrl}/SQLQueries('{queryId}')/List";

            // Add the parameters if any
            if (parameters != null
                && parameters.Length > 0)
            {
                callUrl += "?";
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex += 2)
                {
                    callUrl += parameters[parameterIndex] + '=' + (parameterIndex + 1 < parameters.Length ? parameters[parameterIndex + 1] : "");

                    if (parameterIndex < parameters.Length
                        && parameterIndex > 1)
                    {
                        callUrl += "&";
                    }
                }
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(callUrl);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    var result = "";
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        result = streamReader.ReadToEnd();
                    }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine("Query called successfully.");

                        JObject jsonObject = JObject.Parse(result);
                        var queryResults = (JArray)jsonObject["value"];

                        foreach (var queryResult in queryResults)
                        {
                            ListNodesAndValues(queryResult);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to call the Query. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while callig the Query: " + ex.Message);
            }
        }

        private static void ListNodesAndValues(JToken token, string path = "")
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var prop in token.Children<JProperty>())
                    {
                        ListNodesAndValues(prop.Value, path + "/" + prop.Name);
                    }
                    break;
                case JTokenType.Array:
                    foreach (var child in token.Children())
                    {
                        ListNodesAndValues(child, path + "/[]");
                    }
                    break;
                default:
                    Console.WriteLine($"{path}: {token}");
                    break;
            }
        }

        private static void RemoveQuery(string sessionId, string queryId)
        {
            string callUrl = $"{baseUrl}/SQLQueries('{queryId}')";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(callUrl);
                request.Method = "DELETE";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        Console.WriteLine("Query removed successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to remove the Query. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while removing the Query: " + ex.Message);
            }
        }

        private static void Logout(string sessionId)
        {
            string logoutUrl = $"{baseUrl}/Logout";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(logoutUrl);
                request.Method = "POST";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.NoContent
                        || response.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine("Logged out successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Logout failed. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred during logout: " + ex.Message);
            }
        }
    }
}
