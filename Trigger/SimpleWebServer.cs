using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;

using Charian;
using System.Threading.Tasks;
using System.Net;
using System;
using System.Text;
using System.IO;
using Foldda.Automation.Util;
using System.Linq;

namespace Foldda.Automation.Trigger
{
    /**
     * HttpServer serves HTML content to a web broswer client, and handles Http inputs from the client. 
     */
    public class SimpleWebServer : BasicDataHandler
    {
        const string LISTENING_PORT = "server-port";
        const string WEB_PAGE_SOURCE = "webpage-source";

        //public static HttpListener listener;
        public static int pageViews = 0;
        public static int requestCount = 0;

        protected string URI { get; private set; }
        protected string HostName { get; private set; }

        protected string PagePath { get; private set; }
        static readonly string ERROR_PAGE = @"
<html>
  <head>
    <title>Error</title>
  </head>
  <body>Webpage source file in config is missing or empty.</body>
</html>";

        static readonly string FORM_COMPLETE_PAGE = @"
<html>
  <head>
    <title>Submitted</title>
  </head>
  <body>
    Submitted data is captured.<br> <button onclick=""history.back()"">Go Back</button>
  </body>
</html>";

        public SimpleWebServer(ILoggingProvider logger) : base(logger) { }

        public override void SetParameter(IConfigProvider config)
        {
            int port = config.GetSettingValue(LISTENING_PORT, 80);
            URI = $"http://localhost:{port}/";

            PagePath = Path.Combine((new FileInfo(config.ConfigFileFullPath)).DirectoryName, config.GetSettingValue(WEB_PAGE_SOURCE, string.Empty));
            if (!File.Exists(PagePath))
            {
                Log($"WARNING: missing 'homepage source file' config as the [{WEB_PAGE_SOURCE}] parameter.");
            }
        }

        public override Task ProcessData(CancellationToken cancellationToken) => Listen(URI, cancellationToken);

        private const int ChunkSize = 1024;

        public Task Listen(string prefix, CancellationToken token)
        {
            HttpListener listener = new HttpListener();

            return Task.Run(async () =>
            {
                listener.Prefixes.Add(prefix);
                listener.Start();
                listener.BeginGetContext(ListenerRequestReceivedCallback, listener);

                //task loop
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                //task is cancelled
                if (listener.IsListening)
                {
                    listener.Stop();
                }

            }, token);

        }

        public class RequestHandle
        {
            public HttpListenerContext HttpListenerContext { get; set; }
            public Stream RequestInputStream => HttpListenerContext.Request.InputStream;
            public byte[] Buffer { get; set; }
            public readonly List<byte[]> RequestInputResult = new List<byte[]>();
            public HttpListenerResponse Response => HttpListenerContext.Response;
        }

        private void InputStreamReadingCallback(IAsyncResult ar)
        {
            var handle = (RequestHandle)ar.AsyncState;

            var bytesRead = handle.RequestInputStream.EndRead(ar);
            if (bytesRead > 0)
            {
                var buffer = new byte[bytesRead];
                Buffer.BlockCopy(handle.Buffer, 0, buffer, 0, bytesRead);
                handle.RequestInputResult.Add(buffer);
                handle.RequestInputStream.BeginRead(handle.Buffer, 0, handle.Buffer.Length, InputStreamReadingCallback, handle);
            }
            else
            {
                handle.RequestInputStream.Dispose();

                /** your http repsonse-handling logic **/
                string responseValue = ProcessResponse(handle);

                //write the response back to client
                var responseBytes = Encoding.UTF8.GetBytes(responseValue);
                handle.Response.ContentType = "text/html";
                handle.Response.StatusCode = (int)HttpStatusCode.OK;
                handle.Response.ContentLength64 = responseBytes.Length;
                handle.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                handle.Response.OutputStream.Close();
                return;
            }
        }

        //dummy, override this
        protected string ProcessResponse(RequestHandle responseHandle)
        {
            try
            {
                var req = responseHandle.HttpListenerContext.Request;
                if (req.HttpMethod.Equals("GET") && req.QueryString.Keys.Count > 0)
                {
                    RecordContainer container = new RecordContainer();

                    //store all the query data into a LookupRda object, a client would look into these values to get what it wants
                    DictionaryRda lookup = new DictionaryRda();
                    foreach (var key in req.QueryString.AllKeys)
                    {
                        lookup.SetString(key, req.QueryString[key]);
                    }

                    container.Add(lookup);

                    //send to down-stream handlers
                    OutputStorage.Receive(container);

                    return FORM_COMPLETE_PAGE;
                }
                else if (req.HttpMethod.Equals("POST"))
                {
                    //get the HTTP post raw content 
                    byte[] requestInputData = responseHandle.RequestInputResult.SelectMany(byteArr => byteArr).ToArray();
                    string message = $"Received posted data as below\n{System.Text.UTF8Encoding.Default.GetString(requestInputData)}";

                    //... and parse and handle form elements and values
                    Log(message);
                    return message;
                }
                else
                {
                    return File.ReadAllText(PagePath);      //dummy              
                }
            }
            catch (Exception e)
            {
                Log($"Request-handling error - {e.Message}");
                return ERROR_PAGE;
            }
        }

        //this is the HttpListener's callback 
        private void ListenerRequestReceivedCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;

            try
            {
                //If we are not listening this line throws a ObjectDisposedException.
                HttpListenerContext context = listener.EndGetContext(result);

                listener.BeginGetContext(ListenerRequestReceivedCallback, listener);

                //start reading (and processing) the input content..
                var responseHandle = new RequestHandle { HttpListenerContext = context, Buffer = new byte[ChunkSize] };
                context.Request.InputStream.BeginRead(responseHandle.Buffer, 0, responseHandle.Buffer.Length, InputStreamReadingCallback, responseHandle);
            }
            catch (ObjectDisposedException)
            {
                //Intentionally not doing anything with the exception.
            }
            catch (InvalidOperationException)
            {
                //??
            }
            catch (HttpListenerException)
            {
                listener.Stop();
            }
        }
    }
}