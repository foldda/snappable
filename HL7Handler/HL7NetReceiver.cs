using System;
using System.Threading.Tasks;
using Foldda.Automation.Framework;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Charian;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;

namespace Foldda.Automation.HL7Handler
{
    public class HL7NetReceiver : BaseHL7Handler
    {
        const string LISTENING_PORT = "server-port";
        const int MINIMAL_CONTAINER_INACTIVE_AGE_SEC = 1;

        protected int Port { get; private set; }
        protected string HostName { get; private set; }

        public HL7NetReceiver(ILoggingProvider logger) : base(logger) { }

        public override void SetParameter(IConfigProvider config)
        {
            Port = config.GetSettingValue(LISTENING_PORT, -1);
            HostName = Dns.GetHostName();
        }

        public override Task ProcessData(CancellationToken cancellationToken)
        {
            AckManager ackProducer = new AckManager(this.Logger);

            return Task.Run(() =>
            {
                try
                {
                    Task listnerTask = ListenAsync(ackProducer, cancellationToken);

                    Task dataCollectorTask = Task.Run(async () =>
                    {
                        try
                        {
                            do
                            {
                                //we use a buffer to introduce a delay to the collection of consectively received records, so 
                                //they are packed in the same container
                                ackProducer.CollectedBufferredContainers(OutputStorage, MINIMAL_CONTAINER_INACTIVE_AGE_SEC);

                                await Task.Delay(100);
                            } while (cancellationToken.IsCancellationRequested == false);
                        }
                        catch(Exception e)
                        {
                            Log(e);
                        }

                    });

                    Task.WaitAll(listnerTask, dataCollectorTask);
                    //Task.WaitAll(inputProducingTask, inputToOutputProcessingTask, outputConsumingTask);
                }
                catch (Exception e)
                {
                    Log($"\nHandler operation is stopped due to exception - {e.Message}.");
                }
                finally
                {
                    //Node.LogEvent(Constant.NodeEventType.LastStop);
                    //don't set STATE here, let command and state-table to drive state 
                    Log($"Node handler '{this.GetType().Name}' tasks stopped.");
                }

            });
        }

        private async Task ListenAsync(AckManager ackManager, CancellationToken token)
        {
            //example TCP/IP client server https://github.com/Luaancz/Networking

            //https://stackoverflow.com/questions/43140949/tcplistener-send-heartbeat-every-5-seconds-and-read-message-from-client-async
            // this is more clear multi clients--   
            //https://bsmadhu.wordpress.com/2012/09/29/simplify-asynchronous-programming-with-c-5-asyncawait/
            //https://msdn.microsoft.com/en-us/library/jj155756(v=vs.110).aspx
            //https://stackoverflow.com/questions/19220957/tcplistener-how-to-stop-listening-while-awainting-accepttcpclientasync
            List<Task> connectionHandlingTasks = new List<Task>();
            TcpListener listener = TcpListener.Create(this.Port);
            //https://stackoverflow.com/questions/51515197/c-sharp-tcplistener-keep-listening-after-application-shutdown-only-for-the-first
            //listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            //listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            try
            {
                listener.Start();
                Log($"Started listening on port {Port}");

                using (token.Register(listener.Stop))
                {
                    while (true)
                    {
                        var connection = await listener.AcceptTcpClientAsync().ConfigureAwait(false);   //block

                        string connectionId = ((IPEndPoint)connection.Client.RemoteEndPoint).Address + ":" + ((IPEndPoint)connection.Client.RemoteEndPoint).Port;
                        MllpConnectionHandler mllp = new MllpConnectionHandler(connection.GetStream(), Encoding.Default, connectionId);
                        Log($"Got connection from [{connectionId}].");
                        connectionHandlingTasks.Add(
                            mllp.HandleHl7MessageReceiveAsync(ackManager, token, this.Logger));

                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested) 
                { 
                    Log("Listening cancelled."); 
                }
                else
                {
                    throw e;
                }
            }
            finally
            {
                listener.Stop();
                //then wait for all connection to stop..
                await Task.WhenAll(connectionHandlingTasks.ToArray());
            }

            Log("ListenAsync task stopped.");
        }

        //client to extend this class for customized ACK generation
        public class AckManager
        {
            ILoggingProvider Logger { get; }

            //for accumulate data received from a same network client for a short time-span
            ConcurrentDictionary<string, RecordContainer> TempPerClientContainers { get; } = new ConcurrentDictionary<string, RecordContainer>();

            public AckManager(ILoggingProvider logger)
            {
                Logger = logger;
            }

            //Override this method if it requires a specific received-data processing
            internal virtual char[] ProcessNetReceived(char[] received, string clientId)
            {
                lock(this)
                {
                    try
                    {
                        //parsing and constructing the HL7 record from the received network bytes
                        HL7Message incoming = new HL7Message(received, Encoding.Default);

                        //do something with the received HL7, in our case, save received message to outbound storage queue
                        Logger.Log($"Processing received HL7 message in which has MSH as '{incoming.MSH}'.");

                        //create a temp storage container if not exists
                        if (!TempPerClientContainers.TryGetValue(clientId, out RecordContainer clientContainer))
                        {
                            clientContainer = new RecordContainer()
                            {
                                MetaData = new HandlerEvent(clientId, DateTime.Now),
                                RecordEncoding = HL7Message.HL7MessageEncoding.Default
                            };
                            TempPerClientContainers.TryAdd(clientId, clientContainer);
                        }
                        else
                        {
                            (clientContainer.MetaData as HandlerEvent).EventTime = DateTime.Now;    //reset the timestamp of the container
                        }

                        //store the received record
                        clientContainer.Records.Add(incoming);

                        //respond ACK to sender
                        return RespondAck(incoming);    
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.ToString());
                        throw;
                    }
                }

            }

            //if container is due to be sent ....
            internal virtual int CollectedBufferredContainers(IDataStore inputStorage, int minimalTimeLapBetweenPackets)
            {
                lock(this)
                {
                    int count = 0;
                    foreach(string key in TempPerClientContainers.Keys)
                    {
                        if(TempPerClientContainers.TryGetValue(key, out RecordContainer clientContainer))
                        {
                            HandlerEvent containerLastActive = clientContainer.MetaData as HandlerEvent;
                            if (containerLastActive == null || (DateTime.Now - containerLastActive.EventTime).TotalSeconds > minimalTimeLapBetweenPackets)
                            {
                                if (TempPerClientContainers.TryRemove(key, out RecordContainer removedContainer) && 
                                    removedContainer.Records.Count > 0)
                                {
                                    inputStorage.Receive(removedContainer);
                                    count++;
                                }
                            }
                        }
                    }

                    //total containers collected
                    return count; 
                }

            }

            //Override this method if it requires a customized ack.
            internal char[] RespondAck(HL7Message incoming)
            {
                var MSH_10 = incoming.MSH.Fields[9];
                string msh10 = MSH_10?.Value ?? string.Empty;

                DateTime now = DateTime.UtcNow.ToLocalTime();
                return $"MSH|^~\\&|Foldda|Receiver|Foldda|Automation|{now:yyyyMMddHHmmss}||ACK|{msh10}|P|2.4|\rMSA|AA|{msh10}".ToCharArray();
            }

        }
    }
}