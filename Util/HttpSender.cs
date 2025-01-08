using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.IO.Compression;
using System.Collections.Concurrent;
using System.Text;
using System.Net.Mail;
using System.Net.Http;
using System.Net;

using Charian;


namespace Foldda.Automation.Util
{
    /**
     * HttpSender sends http content to a Http receiver. 
     */
    public class HttpSender
    {
        ILoggingProvider _logger;
        HttpClient _httpClient = new HttpClient();

        public HttpSender(ILoggingProvider logger)
        {
            _logger = logger;
        }

        public void HttpPost(string url, string content, string mimeType, CancellationToken cancellationToken)
        {
            SendHttp(url, content, true, mimeType, cancellationToken);
        }

        public void HttpPut(string url, string content, string mimeType, CancellationToken cancellationToken)
        {
            SendHttp(url, content, false, mimeType, cancellationToken);
        }

        private async void SendHttp(string url, string httpPayload, bool methodIsPost, string mimeType, CancellationToken cancellationToken)
        {
            try
            {
                _logger?.Log($"Sending HTTP data '{httpPayload}' to {url} ...");

                using (var content = new StringContent(httpPayload, System.Text.Encoding.UTF8, mimeType))
                {
                    HttpResponseMessage result = null;
                    if (methodIsPost)
                    {
                        result = await _httpClient.PostAsync(url, content, cancellationToken);
                    }
                    else
                    {
                        result = await _httpClient.PutAsync(url, content, cancellationToken);
                    }

                    //check the result
                    if (result == null || result.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        //if reach here, then it's an error
                        throw new Exception($"Failed to send HTTP data: ({result?.StatusCode})");
                    }
                    else
                    {
                        string returnValue = result.Content.ReadAsStringAsync().Result;
                        _logger?.Log(returnValue);
                    }
                }

                _logger?.Log($"Done.");
            }
            catch (Exception e)
            {
                _logger?.Log(e.ToString());
            }
        }
    }
}