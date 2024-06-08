
using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using System.Net.Sockets;
using Foldda.DataAutomation.Framework;

namespace Foldda.DataAutomation.Util
{
    public class Emailer 
    {
        private SmtpClient _smtpClient = null;

        ILoggingProvider _logger;
        private void Log(string msg)
        {
            _logger.Log(msg);
        }

        private string SmtpClientId { get; } = string.Empty;

        public async Task Send(MailMessage message, string[] to, string[] cc, string[] bcc)
        {
            try
            {
                if (_smtpClient == null)
                {
                    throw new Exception("Email (SMTP) server is not configured.");
                }
                else if ((to == null || to.Length == 0) &&
                         (cc == null || cc.Length == 0) &&
                         (bcc == null || bcc.Length == 0))
                {
                    throw new Exception("No email recipient supplied.");
                }
                else if (message == null)
                {
                    throw new Exception("Message is NULL.");
                }
                else
                {
                    using (message)
                    {
                        Add(to, message.To);
                        Add(cc, message.CC);
                        Add(bcc, message.Bcc);

                        //send the email
                        await _smtpClient.SendMailAsync(message);
                        Log($"Email sent successfully.");
                    }
                }
            }
            catch (Exception e)
            {
                Log($"Email sending failed, error is [{e.Message}]");
            }
        }

        public Emailer(string smtpHost, int port, string login, string password, bool enableTLS, ILoggingProvider logger)
        {
            _logger = logger;
            SmtpClientId = $"{login}@{smtpHost}:{port}";

            try
            {
                //set up a SmtpClient instance using the provided parameters

                if (string.IsNullOrEmpty(smtpHost)) { throw new Exception("SMTP server not specified."); }

                int testResult = TestConnection(smtpHost, port);

                if (testResult == 0)
                {
                    _smtpClient = new SmtpClient()
                    {
                        Host = smtpHost,
                        Port = port,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        EnableSsl = enableTLS
                    };

                    _smtpClient.UseDefaultCredentials = string.IsNullOrEmpty(login);
                    if (!_smtpClient.UseDefaultCredentials)
                    {
                        _smtpClient.Credentials = new NetworkCredential(login, password);
                    }

                    Log($"SMTP client {SmtpClientId} testing OK.");
                }
                else
                {
                    throw new Exception($"SMTP server ({SmtpClientId}) HELO-testing failed (ERR={testResult}).");
                }
            }
            catch (Exception e)
            {
                Log($"Error setting up SMTP - {e.Message}");
            }
        }

        private static void Add(string[] addresses, MailAddressCollection addressCollection)
        {
            if (addresses?.Length > 0)
            {
                foreach (string address in addresses)
                {
                    if (address.IndexOf('@') > 0)
                    {
                        addressCollection.Add(address);
                    }
                    else
                    {
                        throw new Exception($"Email address '{address}' is invalid.");
                    }
                }
            }
        }

        /// <summary>
        /// test the smtp connection by sending a HELO command
        /// </summary>
        /// <param name="smtpServerAddress"></param>
        /// <param name="port"></param>
        private static int TestConnection(string smtpServerAddress, int port)
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(smtpServerAddress);
            IPEndPoint endPoint = new IPEndPoint(hostEntry.AddressList[0], port);
            using (Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                //try to connect and test the rsponse for code 220 = success
                socket.Connect(endPoint);
                int resp = CheckResponse(socket, 220);
                if (resp != 0)
                {
                    return resp;    //220 or 999
                }
                else
                {
                    // send HELO and test the response for code 250 = proper response
                    byte[] dataArray = Encoding.ASCII.GetBytes($"HELO {Dns.GetHostName()}\r\n");
                    socket.Send(dataArray, 0, dataArray.Length, SocketFlags.None);
                    return CheckResponse(socket, 250); //0, 250, or 999
                }
            }
        }
        private static int CheckResponse(Socket socket, int expectedCode)
        {
            int count = 0;
            while (socket.Available == 0)
            {
                Task.Delay(200).Wait();
                if (count++ > 25) { return 999; }
            }
            byte[] buffer = new byte[1024];
            socket.Receive(buffer, 0, socket.Available, SocketFlags.None);
            string received = Encoding.ASCII.GetString(buffer);
            int receivedCode = Convert.ToInt32(received.Substring(0, 3));
            return receivedCode == expectedCode ? 0 : expectedCode;
        }

    }
}

