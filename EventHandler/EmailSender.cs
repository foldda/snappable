using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using Charian;
using System.IO;
using System.Threading.Tasks;
using System;
using Foldda.Automation.Util;
using System.Net.Mail;

namespace Foldda.Automation.EventHandler
{
    /**
     * EmailSender - place holder
     * 
     */
    public class EmailSender : BasicDataHandler
    {
        Emailer Mailer { get; set; }

        EmailContent DefaultMailContent { get; set; }

        //SMTP must come from local config
        public const string SMTP_SERVER_ADDRESS = "smtp-server-address";    /* "smtp.company.com"*/
        public const string SMTP_SERVER_PORT = "smtp-server-port";    /* optional, default is 25 */
        public const string SMTP_ACCOUNT_LOGIN = "smtp-account-login";  /* optional, default is 'server default credential' */
        public const string SMTP_ACCOUNT_PASSWORD = "smtp-account-password";/* required is login is specified */
        public const string SMTP_TLS_ENABLED = "smtp-tls-enabled";   /* optional, defaut is YES */


        //Email record properties that needs to be modeled into an RDA. Can be default from local config
        public const string EMAIL_ADDRESS = "email-to";    /* "someone@mail.com;someone-else@mail.com"*/
        public const string EMAIL_CC_ADDRESS = "email-cc";    /* "someone@mail.com;someone-else@mail.com"*/
        public const string EMAIL_BCC_ADDRESS = "email-bcc";    /* "someone@mail.com;someone-else@mail.com"*/
        public const string EMAIL_BODY_FILE = "email-body-file";  /* "template-file.txt"*/
        public const string EMAIL_SUBJECT = "email-subject";/* "default subject" */

        public EmailSender(ILoggingProvider logger) : base(logger)
        {
        }

        static readonly char[] addressSepatatorChars = new char[] { ';', ',' };
        public override void SetParameter(IConfigProvider config)
        {

            //try get the default message content from the path in the config file
            string messageBody = string.Empty;
            var path = Path.Combine((new FileInfo(config.ConfigFileFullPath)).DirectoryName, config.GetSettingValue(EMAIL_BODY_FILE, string.Empty));
            if (File.Exists(path))
            {
                messageBody = File.ReadAllText(path);
            }
            else
            {
                Log($"ERROR: missing 'email body template file' config in the [{EMAIL_BODY_FILE}] parameter.");
            }

            //only used as default if an upstream record is not, or does not have, an EmailContent object
            DefaultMailContent = new EmailContent()
            {
                EmailAddress = config.GetSettingValue(EMAIL_ADDRESS, string.Empty).Split(addressSepatatorChars),
                EmailCcAddress = config.GetSettingValue(EMAIL_CC_ADDRESS, string.Empty).Split(addressSepatatorChars),
                EmailBccAddress = config.GetSettingValue(EMAIL_BCC_ADDRESS, string.Empty).Split(addressSepatatorChars),
                EmailBody = messageBody,
                EmailSubject = config.GetSettingValue(EMAIL_SUBJECT, string.Empty)
            };

            string smtpHost = config.GetSettingValue(SMTP_SERVER_ADDRESS, string.Empty);
            int port = config.GetSettingValue(SMTP_SERVER_PORT, 25); 
            string login = config.GetSettingValue(SMTP_ACCOUNT_LOGIN, string.Empty); 
            string password = config.GetSettingValue(SMTP_ACCOUNT_PASSWORD, string.Empty); 
            bool enableTLS = config.GetSettingValue(SMTP_TLS_ENABLED, "YES", true);

            Mailer = new Emailer(smtpHost, port, login, password, enableTLS, Logger);
        }


        const string SENDER = "no-reply@foldda.com";
        protected override async Task ProcessRecord(IRda record, RecordContainer inputContainer, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            //1. construct the mail-content object from record's Rda (or use the default from local config) 
            if(record is EmailContent email)
            {
                await SendEmail(email);
            }
            else
            {
                Log($"Record of type {record?.GetType().FullName} is not the expected 'EmailContent' type and is ignored.");
            }

        }

        protected override async Task ProcessHandlerEvent(HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            if (handlerEvent is OsCommander.OutputRecord output)
            {
                DefaultMailContent.EmailBody = output.ExecutionOutput;
                await SendEmail(DefaultMailContent);
            }
        }

        private async Task SendEmail(EmailContent email)
        {
            try
            {
                //2. send the email via SMTP mailer
                using (MailMessage mailMessage = new MailMessage()
                {
                    Subject = email.EmailSubject,
                    Body = email.EmailBody,
                    From = new MailAddress(SENDER)
                })
                {
                    //send the email
                    await Mailer.Send(mailMessage, email.EmailAddress, email.EmailCcAddress, email.EmailBccAddress);
                    Log($"Email '{mailMessage.Subject}' sent - successful.");
                }
            }
            catch (Exception e)
            {
                Log($"ERROR: error sending email - {e.Message}");
            }
        }

        //a record to be processed by this handler
        public class EmailContent : Rda
        {
            enum META_DATA : int { EMAIL_ADDRESS, EMAIL_CC_ADDRESS, EMAIL_BCC_ADDRESS, EMAIL_BODY, EMAIL_SUBJECT } // also "VALIDATION_RULES"

            public EmailContent(Rda sourceRda)
            {
                FromRda(sourceRda);
            }

            internal EmailContent() : base() { }

            public string[] EmailAddress   // 
            {
                get => this[(int)META_DATA.EMAIL_ADDRESS].ChildrenValueArray;
                set => this[(int)META_DATA.EMAIL_ADDRESS].ChildrenValueArray = value;
            }

            public string[] EmailCcAddress   // 
            {
                get => this[(int)META_DATA.EMAIL_CC_ADDRESS].ChildrenValueArray;
                set => this[(int)META_DATA.EMAIL_CC_ADDRESS].ChildrenValueArray = value;
            }

            public string[] EmailBccAddress   // 
            {
                get => this[(int)META_DATA.EMAIL_BCC_ADDRESS].ChildrenValueArray;
                set => this[(int)META_DATA.EMAIL_BCC_ADDRESS].ChildrenValueArray = value;
            }

            public string EmailBody   //
            {
                get => this[(int)META_DATA.EMAIL_BODY].ScalarValue;
                set => this[(int)META_DATA.EMAIL_BODY].ScalarValue = value;
            }

            public string EmailSubject   // 
            {
                get => this[(int)META_DATA.EMAIL_SUBJECT].ScalarValue;
                set => this[(int)META_DATA.EMAIL_SUBJECT].ScalarValue = value;
            }
        }
    }
}

