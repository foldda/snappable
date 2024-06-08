using System;
using System.IO;
using System.Threading.Tasks;
using Foldda.DataAutomation.Framework;
using System.Threading;
using System.Text;
using Charian;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Foldda.DataAutomation.HL7Handler
{
    public class HL7NetSender : BaseHL7Handler
    {
        const int CONNECTION_IDLE_TIME_LIMIT = 3000;
        const string SERVER_ADDRESS = "server-address";
        const string SERVER_PORT = "server-port";
        const string CONN_TIMEOUT = "connection-timeout-sec";
        const string ACK_TIMEOUT = "ack-timeout-sec";
        const string ENABLE_SSL = "enable-ssl";

        protected int ConnectionTimeout { get; private set; }
        protected int AckTimeout { get; set; }
        protected IPAddress ServerIpAddress { get; private set; }
        protected int ServerPort { get; private set; }
        protected bool EnableSSL { get; private set; }

        protected string ServerName { get; set; }

        //for re-use connection if it hasn't being idle
        TcpClient _tcpClient;
        Timer _idleTimer;

        public HL7NetSender(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override void SetParameters(IConfigProvider config)
        {
            ServerName = config.GetSettingValue(SERVER_ADDRESS, string.Empty); //Node.GetFirstConfigValue(SERVER_ADDRESS) ?? string.Empty;
            ServerPort = config.GetSettingValue(SERVER_PORT, 0);
            EnableSSL = config.GetSettingValue(ENABLE_SSL, YES_STRING, false);

            ConnectionTimeout = config.GetSettingValue(CONN_TIMEOUT, 10) * 1000;//default 10sec

            AckTimeout = config.GetSettingValue(ACK_TIMEOUT, 10) * 1000;//default 10sec

            ServerIpAddress = ParseIPAddress(ServerName);

            //it (timer handler) disconnects the connection when there is no more data for a while...
            //set the timer instance but don't start
            _idleTimer = new Timer(DisconnectWhenTimeUp, null, Timeout.Infinite, Timeout.Infinite);
        }

        static IPAddress ParseIPAddress(string serverNameOrFormattedIp)
        {
            IPAddress address = null;
            try
            {
                address = IPAddress.Parse(serverNameOrFormattedIp);
            }
            catch (FormatException)
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(serverNameOrFormattedIp);
                for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
                {
                    if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = ipHostInfo.AddressList[i];
                        break;
                    }
                }
            }

            if (address == null)
            {
                throw new Exception($"Unable to parse or lookup for an IPv4 address for server [{serverNameOrFormattedIp}]");
            }
            else
            {
                return address;
            }
        }

        public override Task OutputConsumingTask(IDataReceiver outputStorage, CancellationToken cancellationToken)
        {
            //stops connection idle-timer ticking,  
            //_idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _connInUse = true;

            var outputReceiced = outputStorage.CollectReceived();

            if (outputReceiced.Count > 0)
            {
                foreach (var container in outputReceiced)
                {
                    SendContainer(container, cancellationToken);
                }
            }

            return Task.Delay(100, cancellationToken); //avoid a busy loop
        }

        //idle times up, tear down the connection
        private void DisconnectWhenTimeUp(object sender)
        {
            if (!_connInUse) { Disconnect(); }
        }

        private void Disconnect()
        {
            if(_tcpClient!=null)
            {
                if (_tcpClient.Connected == true)
                {
                    _tcpClient.GetStream().Close();
                }
                _tcpClient.Close();
                _tcpClient = null;
            }
        }

        //private int _connTimeout;

        private MllpConnectionHandler _mllpHandler;
        private bool _connInUse = false;

        protected MllpConnectionHandler GetMllpConnectionHandler(int retry)
        {
            bool printLog = (retry % 10 == 0);
            
            //check if connection is re-usable
            if (_tcpClient == null || IsDisconnected(_tcpClient) == true || _mllpHandler == null ||
                _mllpHandler.HandlingState == MllpConnectionHandler.State.FAULTY)
            {
                string msg = $"Re-connecting {retry} times due to ";
                if (_tcpClient == null) { msg+="no re-usable connection."; }
                else if (IsDisconnected(_tcpClient) == true) { msg += "current connection is disconnected."; }
                else if (_mllpHandler == null) { msg += "MLLP handler is not initialized."; }
                else if (_mllpHandler.HandlingState == MllpConnectionHandler.State.FAULTY) { msg += "MLLP handler is State.FAULTY"; }
                if(printLog)
                {
                    Log(msg);
                }

                _tcpClient?.Close();    //clean-up

                //re-connect
                _tcpClient = new TcpClient() { SendTimeout = ConnectionTimeout, ReceiveTimeout = ConnectionTimeout };
                if (printLog) { Log($"Connecting to server [{ServerIpAddress}:{ServerPort}] ..."); }
                try
                {

                    var t = _tcpClient.ConnectAsync(ServerIpAddress, ServerPort);
                    if (!t.Wait(ConnectionTimeout))
                    {
                        throw new Exception("Timeout connecting to server.");
                    }
                    else if (t.IsFaulted)
                    {
                        throw new Exception("Client connection completed with (unknown) error.");
                    }

                    Stream streamToUse = _tcpClient.GetStream();
                    string serverHostname = ServerName;
                    var certCollection = new X509CertificateCollection();

                    if (EnableSSL)
                    {
                        SslStream sslStream = new SslStream(streamToUse, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                        if (certCollection.Count > 0)
                        {
                            // A client side certificate was added, authenticate with certificate
                            sslStream.AuthenticateAsClient(serverHostname, certCollection, System.Security.Authentication.SslProtocols.Default, true);
                        }
                        else
                        {
                            sslStream.AuthenticateAsClient(serverHostname);
                        }

                        streamToUse = sslStream;
                    }

                    _mllpHandler = new MllpConnectionHandler(streamToUse, Encoding.Default);
                    Log($"Connected successfully.");
                }
                catch (Exception e)
                {
                    if (printLog)
                    {
                        Log($"ERROR: connection failed - {e.Message}. Please check network configuration and status.");
                    }
                    _mllpHandler = null;
                }
            }

            // now we have got the connection connected
            //try get the messages from data, and send to server
            return _mllpHandler;
        }

        private static bool ValidateServerCertificate(object sender, 
            X509Certificate certificate,
            X509Chain chain, 
            SslPolicyErrors sslPolicyErrors)
        {
            // Accept all certificates
            return true;
        }

        private bool IsDisconnected (TcpClient tcp)
        {
            try
            {
                if (tcp.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (tcp.Client.Receive(buff, SocketFlags.Peek) == 0)
                    {
                        // Client disconnected
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                try { tcp?.Client?.Close(); } catch { }
                return true;
            }
        }


        private async void SendContainer(DataContainer container, CancellationToken token)
        { 
            try
            {
                int retry = 0;
                foreach (var hl7Record in container.Records)
                {
                    HL7Message hl7Msg = new HL7Message(hl7Record);
                    char[] hl7 = hl7Msg.ToChars();
                    Log($"Sending HL7 [{new string(hl7)}]");
                    MllpConnectionHandler mllp = GetMllpConnectionHandler(retry);
                    while (mllp == null)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(500);
                        mllp = GetMllpConnectionHandler(retry);
                        retry++;
                    }

                    //send and handle ACK
                    retry = 0;
                    while(true)
                    {
                        char[] ack = default;
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            ack = await mllp.HandleHl7MessageSendAsync(hl7, AckTimeout, token, this as ILoggingProvider);
                            Log($"Received ACK [{new string(ack)}]");
                            break;
                        }
                        catch(Exception e)
                        {
                            //TODO implement "pause" state when data-processing encounted error
                            Log(e);
                            Log($"Error transmitting data via MLLP, re-tried {retry++} times.");
                            await Task.Delay(100 + retry * 10); //pause a bit

                            //try reconnecting (in a forever-loop)
                            try
                            {
                                while (true)
                                {
                                    token.ThrowIfCancellationRequested();
                                    await Task.Delay(500);

                                    mllp = GetMllpConnectionHandler(retry);
                                    if (mllp != null) { break; }
                                }
                            }
                            catch 
                            {
                                Log("Could not re-establish connection.");
                            }

                            continue;
                        }
                    }

                }
            }
            catch(OperationCanceledException)
            {
                Log($"HL7NetSender.Process() is stopped.");
                throw;
            }
            catch
            {
                Disconnect();   //the connection cannot be reused, so we force a disconnect/clean-up
                Log($"Network connection disconnected.");
                //throw;
            }
            finally
            {
                _connInUse = false; //release this connection so it can be reused /or disconnected.
            }


            /// packet-send completed.
            /// restart/reset connection idle-timer ticking, that is, reuse this connection if more data arrive within the timeframe
            // connection will be tirered down once timer timer times out
            _idleTimer.Change(CONNECTION_IDLE_TIME_LIMIT, Timeout.Infinite);    //set off once only on due time, no repeat
        }
    }
}