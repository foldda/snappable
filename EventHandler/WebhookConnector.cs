using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using System.IO;
using System;
using Charian;
using Foldda.Automation.Util;
using System.Threading.Tasks;

namespace Foldda.Automation.EventHandler
{
    /**
     * WebHookConnector connects to a Web Hook end-point URL and POST a JSON string payload to the end point
     * 
     */
    public class WebhookConnector : BasicDataHandler
    {
        const string WEBHOOK_END_POINT_URL = "webhook-end-point-url";   //the url prefix

        //eg. FULL HTTP URL for Make.com (or Zapier) WebHook end-point
        //eg. https://hook.eu2.make.com/9jex8gv40xbuljk2bp9t0huamn8xd3ly

        private Webhook Connector { get; set; }

        public WebhookConnector(ILoggingProvider logger) : base(logger)
        {
        }

        public override void SetParameter(IConfigProvider config)
        {
            /*

             */
            string webhook_url = config.GetSettingValue(WEBHOOK_END_POINT_URL, string.Empty);

            if (string.IsNullOrEmpty(webhook_url))
            {
                throw new Exception($"Mandatory parameter MAKE_WEBHOOK_END_POINT_URL '{WEBHOOK_END_POINT_URL}' not found in config.");
            }
            else
            {
                Connector = new Webhook(webhook_url, Logger);
            }
        }

        protected override RecordContainer ProcessContainer(RecordContainer container, CancellationToken cancellationToken)
        {
            if (container.Records.Count > 0)
            {
                int recordsWritten = 0;
                try
                {
                    foreach (var record in container.Records)
                    {
                        string jsonPayload = record.ToRda().ScalarValue;
                        Connector.SendData(jsonPayload, cancellationToken);
                        Log($"Sending json '{jsonPayload}' to web-hook url {Connector.WebhookUrl} successful."); 
                        recordsWritten++;
                    }

                    OutputStorage.Receive(new HandlerEvent(Id, DateTime.Now));  //create a dummy event 
                }
                catch (Exception e)
                {
                    Log($"ERROR: Sending Json data elements to Make web-hook failed with exception: {e.Message}");
                    Deb(e.StackTrace);
                }

                Log($"Total {recordsWritten} records processed.");
            }

            return null;    //output container 
        }

        //a record to be processed by this handler
        public class Webhook 
        {
            HttpSender _http_sender;

            public string WebhookUrl { get; }


            public Webhook(string webhook_url, ILoggingProvider logger)
            {
                WebhookUrl = webhook_url;
                _http_sender = new HttpSender(logger);
            }

            public void SendData(string jsonPayload, CancellationToken cancel)
            {
                _http_sender.HttpPost(WebhookUrl, jsonPayload, "application/json", cancel);
            }
        }
    }
}