using code_get_patch_sl.Model;
using code_get_patch_sl.Model.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;

namespace code_get_patch_sl
{
    internal class Program
    {
        private static string baseUrl = "https://sapcloud.discordoba.com:50000/b1s/v1"; // Replace with your SAP Service Layer base URL


        static void Main(string[] args)
        {
            var sessionId = Login();

            if (!string.IsNullOrEmpty(sessionId))
            {
                GetInvoice(sessionId, "1620951");
                PatchBpContact(sessionId, "C20000");
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
                UserName = "manager",
                Password = "Dis2023*!",
                CompanyDB = "PRUEBAS_SEP_2025"
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

        private static void GetInvoice(string sessionId, string docEntry)
        {
            string getUrl = $"{baseUrl}/Invoices({docEntry})";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(getUrl);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");
                request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    var result = "";
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        result = streamReader.ReadToEnd();
                    }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        JObject jsonObject = JObject.Parse(result);

                        // Extract the meaningful values from HEADER
                        var readDocEntry = (int)jsonObject["DocEntry"];
                        var readDocNum = (int)jsonObject["DocNum"];
                        var readCardCode = (string)jsonObject["CardCode"];

                        /* Read more fields if you need to */

                        Console.WriteLine(string.Format("AR Invoice retrieved successfully with DocEntry = {0} and DocNum = {1} for CardCode = {2}", readDocEntry, readDocNum, readCardCode));

                        // Extract the meaningful values from LINES
                        foreach (var readLine in (JArray)jsonObject["DocumentLines"])
                        {
                            Console.WriteLine(string.Format("Line Number: {0} | Item Code: {1} | Quantity: {2} | Price: {3}", readLine["LineNum"], readLine["ItemCode"], readLine["Quantity"], readLine["Price"]));

                            /* Read more fields if you need to */
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to retrieve AR Invoice. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while retrieving the AR Invoice: " + ex.Message);
            }
        }

        private static void PatchBpContact(string sessionId, string cardCode)
        {
            string businessPartnerUrl = $"{baseUrl}/BusinessPartners('{cardCode}')";

            // Create a JSON payload for the new Business Partner
            JObject businessPartnerData = new JObject
            {
                { "CardCode", cardCode }, // Provide a unique Business Partner code
                {
                    "ContactEmployees", new JArray
                    {
                        new JObject
                        {
                            { "InternalCode", 31 },                            
                            { "Name", "Adrian X" },
                            { "Position", "Cookie Seller" },
                            { "Address", "London" },
                            { "Phone1", "07999999999" }
                            // Add more fields as required
                        }
                    }
                }
            };


            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(businessPartnerUrl);
                request.Method = "PATCH";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(businessPartnerData.ToString());
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        Console.WriteLine("Business Partner Contact updated successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to update Business Partner Contact. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while updating the Business Partner Contact: " + ex.Message);
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
