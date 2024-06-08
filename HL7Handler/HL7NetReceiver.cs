using System;
using System.Threading.Tasks;
using Foldda.DataAutomation.Framework;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Charian;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Foldda.DataAutomation.HL7Handler
{
    public class HL7NetReceiver : BaseHL7Handler
    {
        const string LISTENING_PORT = "server-port";

        protected int Port { get; private set; }
        protected string HostName { get; private set; }

        public HL7NetReceiver(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override void SetParameters(IConfigProvider config)
        {
            Port = config.GetSettingValue(LISTENING_PORT, -1);
            HostName = Dns.GetHostName();
        }

        public override Task InputProducingTask(IDataReceiver inputStorage, CancellationToken cancellationToken)
        {
            AckManager ackProducer = new AckManager(inputStorage, this.Logger);
            return ListenAsync(ackProducer, cancellationToken);
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
                        var connection = await listener.AcceptTcpClientAsync().ConfigureAwait(false);//block
                        Log($"Got connection from [{((IPEndPoint)connection.Client.RemoteEndPoint).Address}].");
                        MllpConnectionHandler mllp = new MllpConnectionHandler(connection.GetStream(), Encoding.Default);
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
            IDataReceiver InputStorage { get; }

            ILoggingProvider Logger { get; }

            public AckManager(IDataReceiver inputStorage, ILoggingProvider logger)
            {
                InputStorage = inputStorage;
                Logger = logger;
            }

            //Override this method if it requires a specific received-data processing
            internal virtual char[] ProcessNetReceived(char[] received)
            {
                try
                {
                    //parsing and constructing the HL7 record from the received network bytes
                    HL7Message incoming = new HL7Message(received, Encoding.Default);

                    //do something with the received HL7, in our case, save received message to outbound storage queue
                    Logger.Log($"Processing received HL7 message in which MSH is '{incoming.MSH}'.");
                    InputStorage.Receive(incoming.ToRda());

                    //respond ACK to sender
                    return RespondAck(incoming);    
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                    throw;
                }
            }

            //Override this method if it requires a customized ack.
            internal char[] RespondAck(HL7Message incoming)
            {
                var MSH_10 = incoming.MSH.Fields[9];
                string msh10 = MSH_10?.Value ?? string.Empty;

                DateTime now = DateTime.UtcNow.ToLocalTime();
                return $"MSH|^~\\&|Foldda|Camino|Foldda|Camino|{now:yyyyMMddHHmmss}||ACK|{msh10}|P|2.4|\rMSA|AA|{msh10}".ToCharArray();
            }

        }
    }
}