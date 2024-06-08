using System.Collections.Generic;
using Foldda.DataAutomation.Framework;
using System.Threading;
using System.IO;
using System;
using Charian;
using Foldda.DataAutomation.Util;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.MiscHandler
{
    /**
     * HueLightDriver converts targetted values from a Lookup reposite to a HttpSenderInput parcel 
     * 
     */
    public class HueLightDriver : AbstractDataHandler
    {
        const string HUE_AUTH_ID = "hue-auth-id";
        const string HUE_HUB_END_POINT = "hue-api-end-point";   //the url prefix
        const string HUE_LIGHT_IDS = "hue-light-ids";   //comma-separated hue light Ids

        //FULL HTTTP URL => eg. {end-point}/{auth-id}/lights/{light-id}/state
        //eg. 192.168.1.5/api/Dwpk3cYfwUw7o4IKsKDrgv35myaTM6uLb1sXk2aD/lights/2/state

        private HueLight hueLight { get; set; }

        public HueLightDriver(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath)
        {
        }

        //sample Hue lights' IDs
        string[] lightIds { get; set; }

        public override void SetParameters(IConfigProvider config)
        {
            /*

             */
            string hue_auth_id = config.GetSettingValue(HUE_AUTH_ID, string.Empty);
            string hue_api_end_point = config.GetSettingValue(HUE_HUB_END_POINT, string.Empty);
            string hue_light_ids = config.GetSettingValue(HUE_LIGHT_IDS, string.Empty);
            lightIds = hue_light_ids.Split(new char[] { ',', ';' });

            if (string.IsNullOrEmpty(hue_auth_id) || string.IsNullOrEmpty(hue_api_end_point))
            {
                throw new Exception($"Mandatory parameter HUE_HUB_END_POINT '{hue_api_end_point}' or HUE_AUTH_ID '{hue_auth_id}'not found in config.");
            }
            else
            {
                hueLight = new HueLight(hue_auth_id, hue_api_end_point, new HttpSender(Logger));
            }
        }

        public override Task OutputConsumingTask(IDataReceiver outputStorage, CancellationToken cancellationToken)
        {

            var outputReceiced = outputStorage.CollectReceived();

            if (outputReceiced.Count > 0)
            {
                foreach (var container in outputReceiced)
                {
                    foreach (var record in container.Records)
                    {
                        ConsumeOutputRecord(record, cancellationToken);
                    }
                }
            }

            return Task.Delay(100); //avoid a busy loop
        }

        private void ConsumeOutputRecord(Rda record, CancellationToken cancellationToken)
        {
            try
            {
                //this a reposite of name-value pairs from upstream
                LookupRda httpRequestResult = new LookupRda(record);

                foreach(var lightId in lightIds)
                {
                    HueLight.LIGHT_STATUS lightStatus = 
                        "true".Equals(httpRequestResult.Store[lightId]) ? HueLight.LIGHT_STATUS.ON : HueLight.LIGHT_STATUS.OFF;

                    //2. drive the light
                    hueLight.Switch(lightId, lightStatus, cancellationToken);
                }


            }
            catch (OperationCanceledException)
            {
                Log($"TabularEmailSender.ProcessContainerData() is cancelled");
            }
        }

        //a record to be processed by this handler
        public class HueLight 
        {
            string _hue_auth_id;
            string _hue_api_end_point;
            HttpSender _http_sender;

            public enum LIGHT_STATUS : int { ON, OFF } // also "VALIDATION_RULES"

            public HueLight(string hue_auth_id, string hue_api_end_point, HttpSender http_sender)
            {
                _hue_auth_id = hue_auth_id;
                _hue_api_end_point = hue_api_end_point;
                _http_sender = http_sender;
            }

            public void Switch(string light_id, LIGHT_STATUS newStatus, CancellationToken cancel)
            {
                string lightOn = newStatus == LIGHT_STATUS.OFF ? "false" : "true";
                string httpBody = $"{{\"on\":{lightOn}, \"bri\":254}}";
                string url = $"{_hue_api_end_point}/{_hue_auth_id}/lights/{light_id}/state";
                _http_sender.HttpPost(url, httpBody, "text/plain", cancel);
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