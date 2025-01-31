using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;

using System.Threading.Tasks;
using System.Net;
using System;
using System.Text;
using System.IO;
using Foldda.SmartHL7.API;
using System.Linq;
using System.Collections.Specialized;
using System.Web;
using System.Text.Json;

namespace Foldda.Custom.Handler
{
    /**
     * HttpServer serves HTML content to a web broswer client, and handles Http inputs from the client. 
     */
    public class SmartHL7ApiServer : BasicDataHandler
    {
        const string LISTENING_PORT = "server-port";
        const string VALID_API_KEYS = "valid-api-keys";
        const string QUERY_API_KEY = "api_key";
        const string QUERY_HL7 = "hl7";


        //public static HttpListener listener;
        public static int pageViews = 0;
        public static int requestCount = 0;

        protected string URI { get; private set; }
        protected string HostName { get; private set; }

        protected string[] ValidApiKeys { get; private set; }
        const string ERROR_DETAILS = "ERROR_DETAILS";
        static readonly string ERROR_PAGE = $@"
<html>
  <head>
    <title>Error</title>
  </head>
  <body>There is an error processing your request - {ERROR_DETAILS}.</body>
</html>";


        public SmartHL7ApiServer(ILoggingProvider logger) : base(logger) { }

        public override void SetParameter(IConfigProvider config)
        {
            int port = config.GetSettingValue(LISTENING_PORT, 80);
            URI = $"http://localhost:{port}/";

            ValidApiKeys = config.GetSettingValue(VALID_API_KEYS, string.Empty).Split(new char[] {',', ';'});
        }

        public override async Task ProcessData(CancellationToken cancellationToken) //=> Listen(URI, cancellationToken);
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8081/");
            listener.Start();
            Console.WriteLine("Listening for incoming requests...");

            var listeningTask = HandleRequestsAsync(listener, cancellationToken);

            await listeningTask;
            listener.Close();
        }

        async Task HandleRequestsAsync(HttpListener listener, CancellationToken token)
        {
            try
            {
                while (listener.IsListening && !token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();

                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, token));
                    if (completedTask == contextTask)
                    {
                        var context = await contextTask;
                        _ = Task.Run(() => HandleRequest(context), token);
                    }
                    else
                    {
                        // Cancellation was requested
                        break;
                    }
                }
            }
            catch (HttpListenerException) when (!listener.IsListening)
            {
                // Listener was stopped, exit the loop
            }
            catch (OperationCanceledException)
            {
                // Operation was canceled, exit the loop
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.HttpMethod == "GET")
                {
                    HandleGetRequest(request,response);
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePostRequest(request, response);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    WriteResponse(response, HttpStatusCode.BadRequest, "Method Not Allowed");
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                WriteResponse(response, HttpStatusCode.BadRequest, $"Internal Server Error: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
        }

        private void HandleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.QueryString.Keys.Count > 0)
            {
                //store all the query data into a DictionaryRda object, a client would look into these values to get what it wants
                DictionaryRda lookup = new DictionaryRda();
                foreach (var key in request.QueryString.AllKeys)
                {
                    lookup.SetString(key, request.QueryString[key]);
                }

                //no need to send to down-stream handlers
                //RecordContainer container = new RecordContainer();
                //container.Add(lookup);
                //OutputStorage.Receive(container);

                (HttpStatusCode httpStatusCode, string message) result =  ProcessConvertion(lookup);
                WriteResponse(response, result.httpStatusCode, result.message);
            }
            else
            {
                WriteErrorResponse(response, $"No parameters in query string");
            }
        }

        private void WriteErrorResponse(HttpListenerResponse response, string errorMessage)
        {
            Log($"Sending error response - {errorMessage}");
            WriteResponse(response, HttpStatusCode.BadRequest, ERROR_PAGE.Replace(ERROR_DETAILS, errorMessage));
        }

        private void HandlePostRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!request.HasEntityBody)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                WriteResponse(response, HttpStatusCode.BadRequest, "Request body is required.");
                return;
            }

            using (var bodyStream = request.InputStream)
            using (var reader = new System.IO.StreamReader(bodyStream, Encoding.UTF8))
            {
                string requestBody = reader.ReadToEnd();
                Log($"Received POST data: {requestBody}");

                try
                {
                    // Determine the content type and parse accordingly
                    if (request.ContentType.Contains("application/x-www-form-urlencoded"))
                    {
                        //store all the query data into a DictionaryRda object, a client would look into these values to get what it wants
                        DictionaryRda lookup = new DictionaryRda();
                        // Parse form data
                        NameValueCollection formData = HttpUtility.ParseQueryString(requestBody, Encoding.Default);
                        foreach (string key in formData.AllKeys)
                        {
                            lookup.SetString(key, formData[key]);
                            Log($"Query contains Key: {key}, Value: {formData[key]}.");
                        }

                        //send to down-stream handlers
                        //RecordContainer container = new RecordContainer();
                        //container.Add(lookup);
                        //OutputStorage.Receive(container);

                        (HttpStatusCode httpStatusCode, string message) result = ProcessConvertion(lookup);
                        WriteResponse(response, result.httpStatusCode, result.message);
                    }
                    else if (request.ContentType.Contains("application/json"))
                    {
                        // Parse JSON data - not tested.
                        // Assuming you have a method to parse JSON into a dictionary
                        string json1 = WebUtility.UrlDecode(requestBody);
                        DictionaryRda lookup = ParseJsonToDictionaryRda(json1);

                        (HttpStatusCode httpStatusCode, string message) result = ProcessConvertion(lookup);
                        WriteResponse(response, result.httpStatusCode, result.message);
                    }
                    else
                    {
                        WriteErrorResponse(response, $"Unsupported content type - {request.ContentType}");
                    }
                }
                catch (Exception e)
                {
                    WriteErrorResponse(response, $"HandlePostRequest() ERROR - {e.Message}");
                }

            }
        }

        private void WriteResponse(HttpListenerResponse response, HttpStatusCode httpStatusCode, string content)
        {
            response.StatusCode = (int)httpStatusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = httpStatusCode == HttpStatusCode.OK ? "application/json" : "text/html";
            using (var output = response.OutputStream)
            {
                output.Write(buffer, 0, buffer.Length);
            }            
        }

        private (HttpStatusCode, string) ProcessConvertion(DictionaryRda lookup)
        {
            try
            {
                if (lookup.TryGetString(QUERY_API_KEY, out string apiKey) && lookup.TryGetString("hl7", out string hl7))
                {
                    if (!ValidApiKeys.Contains(apiKey))
                    {
                        throw new Exception($"Supplied '{QUERY_API_KEY}' value [{apiKey}] is invalid.");
                    }
                    else
                    {
                        char[] chars = hl7.ToCharArray();
                        var hl7Message = HL7Message.Parse(chars, Encoding.Default);
                        if(hl7Message != null)
                        {
                            var json = hl7Message.ToJson();
                            Log($"Convertion output: {json}");
                            return (HttpStatusCode.OK, json);
                        }
                        else
                        {
                            return (HttpStatusCode.BadRequest, $"Parsing HL7 unsuccessul, the received HL7 - '{hl7}'");
                        }
                    }
                }

                throw new Exception("Missing values for parameter 'api-kay' and 'hl7' in query. These values are required for hl7-to-json convertion.");
            }
            catch(Exception e)
            {
                return (HttpStatusCode.BadRequest, e.Message);
            }
        }

        private DictionaryRda ParseJsonToDictionaryRda(string jsonString)
        {
            ApiQuery apiQuery = JsonSerializer.Deserialize<ApiQuery>(jsonString);
            DictionaryRda result = new DictionaryRda();
            result.SetString(QUERY_API_KEY, apiQuery.api_key);
            result.SetString(QUERY_HL7, apiQuery.hl7);

            return result; 
        }

        class ApiQuery
        {
            public string api_key { get; set; }
            public string hl7 { get; set; }
        }
    }
}

