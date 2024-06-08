using System.Collections.Generic;
using Foldda.DataAutomation.Framework;
using System.Threading;

using Charian;
using System.Threading.Tasks;
using System.Net;
using System;
using System.Text;
using System.IO;
using Foldda.DataAutomation.Util;

namespace Foldda.DataAutomation.MiscHandler
{
    /**
     * HttpServer serves HTML content to a web broswer client, and handles Http inputs from the client. 
     * 
     */
    public class SimpleWebServer : AbstractDataHandler
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
  <body>Webpage source file is missing or empty.</body>
</html>";

        protected SimpleWebServer(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override void SetParameters(IConfigProvider config)
        {
            int port = config.GetSettingValue(LISTENING_PORT, 80);
            URI = $"http://localhost:{port}/";

            PagePath = config.GetSettingValue(WEB_PAGE_SOURCE, string.Empty);
        }

        public override Task InputProducingTask(IDataReceiver inputStorage, CancellationToken cancellationToken)
        {
            return Listen(URI, inputStorage, cancellationToken);// ListenAsync(ackProducer, cancellationToken);
        }

        const int MAX_CONCURRENT_CONNECTION = 10;

        public async Task Listen(string prefix, IDataReceiver inputStorage, CancellationToken token)
        {
            HttpListener listener = new HttpListener();
            var requests = new HashSet<Task>();
            try
            {
                listener.Prefixes.Add(prefix);
                listener.Start();

                for (int i = 0; i < MAX_CONCURRENT_CONNECTION; i++)
                {
                    var t = listener.GetContextAsync(); //creating concurrent-handling threads pool
                    requests.Add(t);
                }

                int timeout = 100;
                while (true)
                {
                    requests.Add(Task.Delay(timeout));  //adding a timeout task, so we can check the token-cancellation

                    Task t = await Task.WhenAny(requests);  //wait for having received a client-request (or time-out)
                    requests.Remove(t); //this thread is completed with result

                    if (t is Task<HttpListenerContext>)
                    {
                        //get the context from request completed listening-thread's result
                        var context = (t as Task<HttpListenerContext>).Result;
                        //create a thread for responding to the request 
                        requests.Add(ProcessRequestAsync(context, inputStorage, token));

                        //add a new request-listening thread back to the pool
                        requests.Add(listener.GetContextAsync());
                    }
                    else
                    {   //task is the timeout 
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception ex)
            {
                //if Token was canceled - swap the exception
                if (token.IsCancellationRequested && ex is ObjectDisposedException)
                {
                    throw new OperationCanceledException("Listening cancelled.");
                };
                throw ex;
            }
            finally
            {
                listener.Stop();
                //then wait for all connection to stop..
                await Task.WhenAll(requests);
                Log("ListenAsync task stopped.");
            }


        }

        static int i = 0;
        public async Task ProcessRequestAsync(HttpListenerContext ctx, IDataReceiver inputStorage, CancellationToken token)
        {
            //bool runServer = true;

            // keep on handling requests
            while (!token.IsCancellationRequested)
            {
                // Will wait here until we hear from a connection
                //HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;

                // Print out some info about the request
                //check box and radio https://stackoverflow.com/questions/11424037/do-checkbox-inputs-only-post-data-if-theyre-checked
                Log($"Request #: {++requestCount}");
                Log(req.Url.ToString());
                Log(req.HttpMethod);
                Log(req.UserHostName);
                Log(req.UserAgent);

                //var postParams = GetRequestPostData(req);

                //this Webserver dump all received data into a Lookup collection, and send to down stream.
                if (req.QueryString.Keys.Count > 0)
                {
                    try
                    {
                        DataContainer container = new DataContainer();

                        //store all the query data in the LookupRda object, a client would look into these values to get what it wants
                        LookupRda lookup = new LookupRda();
                        foreach (var key in req.QueryString.AllKeys)
                        {
                            lookup.Store.Add(key, req.QueryString[key]);
                        }   
                        
                        container.Add(lookup.ToRda());
                        
                        inputStorage.Receive(container);
                    }
                    catch (Exception e)
                    {
                        Log($"No container constructed - error - {e.Message}");
                    }
                }

                // Make sure we don't increment the page views counter if `favicon.ico` is requested
                if (req.Url.AbsolutePath != "/favicon.ico")
                    pageViews += 1;

                string pageSource = null;
                string filePath = null;
                try
                {
                    filePath = Path.Combine(HomePath.FullName, PagePath);
                    pageSource = File.ReadAllText(filePath);
                }
                catch
                {
                    Log($"Error reading web page source file: {filePath}");
                }

                if (string.IsNullOrEmpty(pageSource))
                {
                    pageSource = ERROR_PAGE;
                }

                // Write the response info
                HttpListenerResponse resp = ctx.Response;

                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                byte[] data = Encoding.UTF8.GetBytes(String.Format(pageSource, pageViews, string.Empty));
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length, token);
                resp.Close();
            }
        }
    }
}