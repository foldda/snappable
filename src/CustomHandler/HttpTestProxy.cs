using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;

using Charian;
using System.Threading.Tasks;
using System.Net;
using System;
using System.Text;
using System.IO;
using Foldda.SmartHL7.API;
using System.Linq;
using System.Collections.Specialized;
using System.Web;
using System.Net.Mail;
using System.Text.Json;
using System.ComponentModel;
using System.Net.Http;

namespace Foldda.Custom.Handler
{
    /**
     * HttpTestProxy serves an HTML form to a web broswer client, and forward Http inputs from the client to a destination http server, and relay the http server's response back to the client. In the HTML form, the client can specify the http-method and the mime-type of the request that would be forwarded to the target server.
     */
    public class HttpTestProxy : BasicDataHandler
    {
        const string LISTENING_PORT = "server-port";

        //public static HttpListener listener;
        public static int pageViews = 0;
        public static int requestCount = 0;

        protected string URI { get; private set; }

        public HttpTestProxy(ILoggingProvider logger) : base(logger) { }

        public override void SetParameter(IConfigProvider config)
        {
            int port = config.GetSettingValue(LISTENING_PORT, 80);
            URI = $"http://localhost:{port}/";
        }

        public override Task ProcessData(CancellationToken cancellationToken) => Listen(URI, cancellationToken);

        public Task Listen(string prefix, CancellationToken token)
        {
            HttpListener listener = new HttpListener();
            //using (HttpListener listener = new HttpListener())
            {
                return Task.Run(async () =>
                {
                    listener.Prefixes.Add(prefix);
                    listener.Start();
                    Log("Listening for incoming requests...");

                    while (listener.IsListening && !token.IsCancellationRequested)
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested();

                            var context = await listener.GetContextAsync();
                            _ = Task.Run(() => HandleRequest(context));
                        }
                        catch (OperationCanceledException)
                        {
                            //task is cancelled
                            listener.Stop();
                            listener.Close();
                        }
                        catch (HttpListenerException) when (!listener.IsListening)
                        {
                            // Listener was stopped, exit the loop
                            break;
                        }
                    }

                }, token);
            }
        }

        private readonly HttpClient _httpClient = new HttpClient();

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod != "POST")
                {
                    //context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    //byte[] errorBytes = Encoding.UTF8.GetBytes("Only POST method is allowed for this proxy.");
                    //context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                    ServeStaticHtml(context, URI);
                    return;
                }

                // Read the form data
                using (StreamReader reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    string formData = await reader.ReadToEndAsync();

                    // If no data, serve a static HTML page
                    if (string.IsNullOrWhiteSpace(formData))
                    {
                        ServeStaticHtml(context, URI);
                        return;
                    }

                    // Parse the form data
                    var parsedFormData = System.Web.HttpUtility.ParseQueryString(formData);

                    string targetServer = parsedFormData["targetServer"];
                    string httpMethod = parsedFormData["httpMethod"];
                    string mimeType = parsedFormData["mimeType"];

                    // Build the new URL and form body
                    string targetUrl = targetServer;
                    var forwardedFormData = new System.Collections.Specialized.NameValueCollection();
                    foreach (string key in parsedFormData.AllKeys)
                    {
                        if (key != "httpMethod" && key != "mimeType" && key != "targetServer")
                        {
                            forwardedFormData[key] = parsedFormData[key];
                        }
                    }

                    // Create the forwarded request
                    using (HttpRequestMessage forwardRequest = new HttpRequestMessage(new HttpMethod(httpMethod), targetUrl))
                    {
                        if (httpMethod == "POST" || httpMethod == "PUT")
                        {
                            string body = string.Join("&", Array.ConvertAll(forwardedFormData.AllKeys, key => $"{key}={Uri.EscapeDataString(forwardedFormData[key])}"));
                            forwardRequest.Content = new StringContent(body, Encoding.UTF8, mimeType);
                        }

                        // Forward headers
                        foreach (string headerKey in context.Request.Headers.AllKeys)
                        {
                            forwardRequest.Headers.TryAddWithoutValidation(headerKey, context.Request.Headers[headerKey]);
                        }

                        // Send the request to the secondary server
                        using (HttpResponseMessage forwardResponse = await _httpClient.SendAsync(forwardRequest))
                        {
                            // Check the response MIME type and modify if it's 'text/plain'
                            string responseBody = await forwardResponse.Content.ReadAsStringAsync();
                            responseBody = WrapInHtml(responseBody, forwardResponse.Content.Headers.ContentType?.ToString() ?? string.Empty);
                            context.Response.ContentType = "text/html";

                            // Prepare the response for the client
                            context.Response.StatusCode = (int)forwardResponse.StatusCode;
                            foreach (var header in forwardResponse.Headers)
                            {
                                context.Response.Headers[header.Key] = string.Join(",", header.Value);
                            }
                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBody);
                            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                byte[] errorBytes = Encoding.UTF8.GetBytes("Internal Server Error");
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static void ServeStaticHtml(HttpListenerContext context, string url)
        {
            string htmlContent = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Proxy Form</title>
</head>
<body>
    <h1>SmartHL7 API Test Proxy Form</h1>
    <form action=""{url}"" method=""POST"">
        <label for=""httpMethod"">HTTP Method:</label>
        <select id=""httpMethod"" name=""httpMethod"">
            <option value=""POST"">POST</option>
            <option value=""GET"">GET</option>
            <option value=""PUT"">PUT</option>
            <option value=""DELETE"">DELETE</option>
        </select>
        <br><br>

        <label for=""mimeType"">MIME Type:</label>
        <select id=""mimeType"" name=""mimeType"">
            <option value=""application/json"">application/json</option>
            <option value=""text/plain"">text/plain</option>
            <option value=""application/xml"">application/xml</option>
            <option value=""text/html"">text/html</option>
        </select>
        <br><br>

        <label for=""targetServer"">Target Server URL:</label>
        <input type=""text"" id=""targetServer"" name=""targetServer"" value=""http://localhost:8081/"" required>
        <br><br>

        <label for=""api_key"">API Key:</label>
        <input type=""text"" id=""api_key"" name=""api_key"" value=""12345"">
        <br><br>

        <label for=""hl7"">HL7 Data:</label>
        <textarea id=""hl7"" name=""hl7"" rows=""5"" cols=""40"" placeholder=""You HL7 data here ...""></textarea>
        <br><br>

        <button type=""submit"">Submit</button>
    </form>
</body>
</html>";
            byte[] responseBytes = Encoding.UTF8.GetBytes(htmlContent);
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }

        private static string WrapInHtml(string plainText, string mime)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Wrapped Server Response</title>
</head>
<body>
    <h1>API Server Response (mime-type:{mime})</h1>
    <textarea style=""width:100%; height:300px;"" readonly>{System.Web.HttpUtility.HtmlEncode(plainText)}</textarea>
    <button onclick=""history.back()"">Go Back</button>
</body>
</html>";
        }
    }
}

