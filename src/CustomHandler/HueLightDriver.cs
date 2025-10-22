using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using System.IO;
using System;
using Charian;
using Foldda.Automation.Util;
using System.Threading.Tasks;

namespace Foldda.Custom.Handler
{
    /**
     * HueLightDriver converts targetted values from a Lookup reposite to a HttpSenderInput parcel 
     * 
     */
    public class HueLightDriver : BasicDataHandler
    {
        const string HUE_AUTH_ID = "hue-auth-id";
        const string HUE_HUB_END_POINT = "hue-api-end-point";   //the url prefix
        const string HUE_LIGHT_IDS = "hue-light-ids";   //comma-separated hue light Ids

        //FULL HTTTP URL => eg. {end-point}/{auth-id}/lights/{light-id}/state
        //eg. 192.168.1.5/api/Dwpk3cYfwUw7o4IKsKDrgv35myaTM6uLb1sXk2aD/lights/2/state

        private HueHubLink HueHub { get; set; }

        public HueLightDriver(ILoggingProvider logger) : base(logger)
        {
        }

        //sample Hue lights' IDs
        string[] HueLightIds { get; set; }

        public override void SetParameter(IConfigProvider config)
        {
            /*

             */
            string hue_auth_id = config.GetSettingValue(HUE_AUTH_ID, string.Empty);
            string hue_api_end_point = config.GetSettingValue(HUE_HUB_END_POINT, string.Empty); //end-point of the hue hub device
            string hue_light_ids = config.GetSettingValue(HUE_LIGHT_IDS, string.Empty);
            HueLightIds = hue_light_ids.Split(new char[] { ',', ';' });

            if (string.IsNullOrEmpty(hue_auth_id) || string.IsNullOrEmpty(hue_api_end_point))
            {
                throw new Exception($"Mandatory parameter HUE_HUB_END_POINT '{hue_api_end_point}' or HUE_AUTH_ID '{hue_auth_id}'not found in config.");
            }
            else
            {
                HueHub = new HueHubLink(hue_auth_id, hue_api_end_point, Logger);
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
                        ConsumeOutputRecord(record, cancellationToken);
                    }

                    OutputStorage.Receive(new HandlerEvent(Id, DateTime.Now));  //create a dummy event 
                }
                catch (Exception e)
                {
                    Log($"ERROR: Sending Hue command failed with exception: {e.Message}");
                    Deb(e.StackTrace);
                }

                Log($"Total {recordsWritten} records processed.");
            }

            return null;    //output container 
        }

        private void ConsumeOutputRecord(IRda record, CancellationToken cancellationToken)
        {
            try
            {
                //this a reposite of name-value pairs from upstream
                if(record is DictionaryRda httpRequestResult)
                {
                    foreach(var lightId in HueLightIds)
                    {
                        if(httpRequestResult.TryGetString(lightId, out string trueFalse))
                        {
                            HueHubLink.LIGHT_STATUS lightStatus = "true".Equals(trueFalse) ? HueHubLink.LIGHT_STATUS.ON : HueHubLink.LIGHT_STATUS.OFF;

                            //2. drive the light
                            Log($"Sending command '{lightStatus}' to light-id {lightId}");
                            HueHub.SwitchLight(lightId, lightStatus, cancellationToken);
                        }
                        else
                        {
                            Log($"No control instruction for light-id {lightId}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log($"TabularEmailSender.ProcessContainerData() is cancelled");
            }
        }

        //a record to be processed by this handler
        public class HueHubLink 
        {
            string _hue_auth_id;
            string _hue_api_end_point;
            HttpSender _http_sender;

            public enum LIGHT_STATUS : int { ON, OFF } // also "VALIDATION_RULES"

            public HueHubLink(string hue_auth_id, string hue_api_end_point, ILoggingProvider logger)
            {
                _hue_auth_id = hue_auth_id;
                _hue_api_end_point = hue_api_end_point;
                _http_sender = new HttpSender(logger);
            }

            public void SwitchLight(string lightId, LIGHT_STATUS newStatus, CancellationToken cancel)
            {
                string lightOn = newStatus == LIGHT_STATUS.OFF ? "false" : "true";
                string httpBody = $"{{\"on\":{lightOn}, \"bri\":254}}";
                string url = $"{_hue_api_end_point}/{_hue_auth_id}/lights/{lightId}/state";
                //_http_sender.HttpPost(url, httpBody, "text/plain", cancel);
                _http_sender.HttpPut(url, httpBody, "text/plain", cancel);
            }
        }

        //public override Task<IDataContainer> ProcessTask(IDataContainer container, CancellationToken cancellationCheck)
        //{
        //    //container.ReportCountsTo(Node);
        //    if (container?.InputRecords?.Count > 0)
        //    {
        //        StringBuilder newRecord = new StringBuilder();
        //        foreach (var record in container?.InputRecords)
        //        {
        //            string[] columns = (new string(record)).Split(TabularDataContainer.COLUMN_SEPARATOR);

        //            string light = columns[0]; //eg "1", "2"
        //            string lightOn = columns[1];    //eg "true" or "false"
        //            string httpBody = $"{{\"on\":{lightOn}, \"bri\":254}}";
        //            string url = $"{_hue_api_end_point}/{_hue_auth_id}/lights/{light}/state";

        //            newRecord.Append(url)  //http URL for used by HttpClient
        //                .Append(TabularDataContainer.COLUMN_SEPARATOR)
        //                .Append(httpBody);
        //            Log($"URL:{url}, Json:{httpBody}");

        //            container.AddOutputRecord(newRecord.ToString().ToCharArray(), Node);
        //            newRecord.Clear();
        //        }
        //    }

        //    return Task.FromResult(container);
        //}
    }
}