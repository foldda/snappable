using System.Collections.Generic;
using Foldda.DataAutomation.Framework;
using System.Threading;
using Charian;
using System;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace Foldda.DataAutomation.MiscHandler
{
    /**
     * Driven by trigger events (e.g. a timer), FtpDownloader attempts to download available files from 
     * a designated FTP host, using supplied parameters including host's network details, login and 
     * password, targeted files path and naming pattern, etc.
     * 
     * These parameters can also come from a inbound HandlerEvent record, i.e. like driving commands
     * 
     */
    public class FtpDownloader : AbstractDataHandler
    {
        public class FtpDownloadConfig : Rda
        {
            //these constants are used by getting config settings to construct a FtpDownloaderInput record
            public const string FTP_SERVER = "ftp-server";
            public const string PORT = "port";
            public const string LOGIN = "login";
            public const string PASSWORD = "password";
            public const string REMOTE_SOURCE_PATH = "remote-source-path";
            public const string TARGET_FILE_NAME_PATTERN = "target-file-name-pattern";
            public const string LOCAL_DESTINATION_PATH = "local-destination-path";
            public const string BINARY_MODE = "binary-mode";
            public const string DELETE_SOURCE_FILE_AFTER_DOWNLOAD = "delete-source-file-after-download";

            public enum RDA_INDEX { FTP_SERVER, PORT, LOGIN, PASSWORD, REMOTE_SOURCE_PATH, TARGET_FILE_NAME_PATTERN, LOCAL_DESTINATION_PATH, BINARY_MODE, DELETE_SOURCE_FILE_AFTER_DOWNLOAD }

            public string FtpServer
            {
                get => this[(int)RDA_INDEX.FTP_SERVER].ScalarValue;
                set => this[(int)RDA_INDEX.FTP_SERVER].ScalarValue = value.ToString();
            }
            public int Port
            {
                get => int.TryParse(this[(int)RDA_INDEX.PORT].ScalarValue, out int result) ? result : -1;
                set => this[(int)RDA_INDEX.PORT].ScalarValue = value.ToString();
            }
            public string Login
            {
                get => this[(int)RDA_INDEX.LOGIN].ScalarValue;
                set => this[(int)RDA_INDEX.LOGIN].ScalarValue = value.ToString();
            }
            internal string Password
            {
                get => this[(int)RDA_INDEX.PASSWORD].ScalarValue;
                set => this[(int)RDA_INDEX.PASSWORD].ScalarValue = value.ToString();
            }

            public string RemoteSourcePath
            {
                get => this[(int)RDA_INDEX.REMOTE_SOURCE_PATH].ScalarValue;
                set => this[(int)RDA_INDEX.REMOTE_SOURCE_PATH].ScalarValue = value.ToString();
            }
            public string TargetFileNamePattern
            {
                get => this[(int)RDA_INDEX.TARGET_FILE_NAME_PATTERN].ScalarValue;
                set => this[(int)RDA_INDEX.TARGET_FILE_NAME_PATTERN].ScalarValue = value.ToString();
            }
            public string LocalDestinationPath
            {
                get => this[(int)RDA_INDEX.LOCAL_DESTINATION_PATH].ScalarValue;
                set => this[(int)RDA_INDEX.LOCAL_DESTINATION_PATH].ScalarValue = value.ToString();
            }
            public bool BinaryMode
            {
                get => YES.Equals(this[(int)RDA_INDEX.BINARY_MODE].ScalarValue, StringComparison.OrdinalIgnoreCase);
                set => this[(int)RDA_INDEX.BINARY_MODE].ScalarValue = value.ToString();
            }
            public bool DeleteSourceFileAfterDownload
            {
                get => YES.Equals(this[(int)RDA_INDEX.BINARY_MODE].ScalarValue, StringComparison.OrdinalIgnoreCase);
                set => this[(int)RDA_INDEX.DELETE_SOURCE_FILE_AFTER_DOWNLOAD].ScalarValue = value ? YES : string.Empty;
            }

            public const string YES = "yes";

            public FtpDownloadConfig(Rda originalRda) : base(originalRda)
            {
            }

            public FtpDownloadConfig()
            {
            }
        }

        //this data record tells the downstream handler (eg a Csv-Reader where to pick up the resulted data)
        public class OutputRecord : HandlerEvent
        {
            public OutputRecord(string sourceId, DateTime time) : base(sourceId, time)
            {
               //
            }

            public string OutputFilePath   //
            {
                get => EventContextRda.ScalarValue;
                set => EventContextRda.ScalarValue = value;
            }
        }

        internal FtpDownloadConfig LocalConfig { get; private set; }

        List<string> DownloadUriExclusionList { get; } = new List<string>();    //full-path

        public FtpDownloader(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath)
        {
        }

        //dummy implementation is required
        public override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return null;    // throw new NotImplementedException();
        }

        public override void SetParameters(IConfigProvider config)
        {
            LocalConfig = new FtpDownloadConfig()
            {
                RemoteSourcePath = config.GetSettingValue(FtpDownloadConfig.REMOTE_SOURCE_PATH, string.Empty),
                TargetFileNamePattern = config.GetSettingValue(FtpDownloadConfig.TARGET_FILE_NAME_PATTERN, string.Empty),
                LocalDestinationPath = config.GetSettingValue(FtpDownloadConfig.LOCAL_DESTINATION_PATH, string.Empty),
                BinaryMode = config.GetSettingValue(FtpDownloadConfig.BINARY_MODE, "Y", false),
                DeleteSourceFileAfterDownload = config.GetSettingValue(FtpDownloadConfig.DELETE_SOURCE_FILE_AFTER_DOWNLOAD, "Y", true),

                FtpServer = config.GetSettingValue(FtpDownloadConfig.FTP_SERVER, string.Empty),
                Port = config.GetSettingValue(FtpDownloadConfig.PORT, 24),
                Login = config.GetSettingValue(FtpDownloadConfig.LOGIN, string.Empty),
                Password = config.GetSettingValue(FtpDownloadConfig.PASSWORD, string.Empty)
            };
        }

        public override void ProcessRecord(Rda eventTriggerRecord, Rda processingContext, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {

                FtpDownloadConfig downloadConfig;
                //testing if the trigger contains 'download instructions' in its context,
                try
                {
                    Log($"Downloading triggered by {outputContainer.ProcessingContext}");
                    downloadConfig = new FtpDownloadConfig(outputContainer.ProcessingContext);
                }
                catch
                {
                    //if not, use the handler's local settings
                    Log($"Container has no file-download instrcution, local (FTP) config settings are used.");
                    downloadConfig = LocalConfig;
                }

                try
                {
                    HandlerEvent trigger = new HandlerEvent(eventTriggerRecord);
                    Log($"Download triggered on {trigger.EventTime}.");
                }
                catch
                {
                    //if not, use the handler's local settings
                    Log($"Trigger event is invalid, default trigger is used.");
                }

                List<OutputRecord> recordsRead = RunFtpDownloadSession(downloadConfig, cancellationToken);

                Log($"Downloaded {recordsRead.Count} files.");
            }
            catch (Exception e)
            {
                Log(e);
                throw e;
            }
        }

        //returns output records each has a path of one of the files downloaded.
        internal virtual List<OutputRecord> RunFtpDownloadSession(FtpDownloadConfig downloadConfig, CancellationToken cancellationToken)
        {
            
            if (string.IsNullOrEmpty(downloadConfig.RemoteSourcePath) ||
                string.IsNullOrEmpty(downloadConfig.TargetFileNamePattern) ||
                string.IsNullOrEmpty(downloadConfig.LocalDestinationPath))
            {
                return null;
            }

            string ftpRemotePath = downloadConfig.RemoteSourcePath;
            string localFilePath = downloadConfig.LocalDestinationPath;
            string user = downloadConfig.Login;
            string password = downloadConfig.Password;

            var uri = $"ftp://{downloadConfig.FtpServer}:{downloadConfig.Port}{ftpRemotePath}";   //eg. "ftp://ftp.example.com/remote/path/file.zip"

            try
            {
                List<string> remoteMatchedFiles = new List<string>();

                Log($"Downloading ... '{localFilePath}' << '{uri}' (for pattern '{downloadConfig.TargetFileNamePattern}') ");
                FtpWebRequest listRequest = (FtpWebRequest)WebRequest.Create(uri);
                listRequest.Method = WebRequestMethods.Ftp.ListDirectory;

                listRequest.Credentials = new NetworkCredential(user, password);

                //1. get the list of files from remote path
                using (FtpWebResponse listResponse = (FtpWebResponse)listRequest.GetResponse())
                using (Stream responseStream = listResponse.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    //Log($"Directory List Complete, status {listResponse.StatusDescription}");
                    string listingInfo = reader.ReadToEnd();
                    if (string.IsNullOrEmpty(listingInfo))
                    {
                        return null;
                    }
                    //loop thru the listing for matching files
                    remoteMatchedFiles = GetRemoteFileNameList(listingInfo.Trim(), downloadConfig.TargetFileNamePattern);

                    //2. for each remote file, check if it matches the target name-pattern
                    if (remoteMatchedFiles.Count > 0)
                    {
                        return FetchFiles(downloadConfig, remoteMatchedFiles, uri, cancellationToken);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Log(e.Message + "\n" + e.StackTrace);
                throw;  //closes connection/program.
            }
        }

        private List<OutputRecord> FetchFiles(FtpDownloadConfig downloadConfig, List<string> matchingRemoteFiles, string uri, CancellationToken cancellationToken)
        {
            List<OutputRecord> result = new List<OutputRecord>();

            string localFilePath = downloadConfig.LocalDestinationPath;

            if (!Directory.Exists(localFilePath))
            {
                try
                {
                    Directory.CreateDirectory(localFilePath);
                    Log($"Destination path '{localFilePath}' is created.");
                }
                catch
                {
                    Log($"ERROR - Download destination '{localFilePath}' does not exist or is inaccessible.");
                    throw;
                }
            }

            string user = downloadConfig.Login;
            string password = downloadConfig.Password;
            int count = 0;

            if (matchingRemoteFiles.Count > 0)
            {
                foreach (var remoteFile in matchingRemoteFiles)
                {
                    string remoteFileUri = uri + "/" + remoteFile;

                    //exclude the file if it's in the exclusion list.
                    if (DownloadUriExclusionList.Contains(remoteFileUri)) { continue; }

                    FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create(remoteFileUri);
                    ftpRequest.Credentials = new NetworkCredential(user, password);
                    ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                    ftpRequest.UseBinary = downloadConfig.BinaryMode;

                    string localFileFullPath = Path.Combine(localFilePath, remoteFile);         //path+name
                    string localTempFileFullPath = localFileFullPath + "_camino_tmp_";         //temp path+name

                    try
                    {
                        //2.1 do download
                        using (FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
                        using (Stream ftpStream = ftpResponse.GetResponseStream())
                        using (Stream localFileStream = File.OpenWrite(localTempFileFullPath))
                        {
                            ftpStream.CopyTo(localFileStream);
                        }

                        // clear the designated download location
                        if (File.Exists(localFileFullPath))
                        {
                            File.Delete(localFileFullPath);
                            Log($"WARNING - existing local file '{remoteFile}' is overwritten by the downloaded remote file.");
                        }

                        //move the completed file to the correct file name
                        File.Move(localTempFileFullPath, localFileFullPath);

                        OutputRecord output = new OutputRecord(remoteFileUri, DateTime.Now) 
                        { 
                            OutputFilePath = localFileFullPath 
                        };

                        //2.2 clean-up the remote file after download
                        if (downloadConfig.DeleteSourceFileAfterDownload)
                        {
                            FtpWebRequest deleteSourceRequest = (FtpWebRequest)WebRequest.Create(remoteFileUri);
                            deleteSourceRequest.Credentials = new NetworkCredential(user, password);
                            deleteSourceRequest.Method = WebRequestMethods.Ftp.DeleteFile;
                            using (FtpWebResponse deleteSourceResponse = (FtpWebResponse)deleteSourceRequest.GetResponse())
                            {
                                Log($"   Source file '{remoteFile}' is deleted.");
                            }
                        }
                        else
                        {
                            //add to exclusion list so it won't be downloaded again.
                            DownloadUriExclusionList.Add(remoteFileUri);
                            Log($"Source file '{remoteFile}' is not deleted, but is excluded from future download.");
                        }

                        //3.done
                        result.Add(output);
                        Log($"   ... '{remoteFile}' downloaded.");

                        count++;

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException)
                    {
                        Log("WARNING: Downloading is interrupted by request.");
                        break;
                    }
                    catch (Exception e)
                    {
                        string errMsg = e.Message;
                        if (e is WebException we)
                        {
                            errMsg += "\r\nFTP response: " + ((FtpWebResponse)we.Response).StatusDescription;
                        }
                        Log($"Downloading file '{remoteFile}' failed with error - {errMsg}");

                        //clean-up if there was an error
                        try { File.Delete(localTempFileFullPath); } catch { }
                    }

                }   //end of "for-each remote file .. download" loop
            }

            return result;
        }

        /// <summary>
        /// break file-listing result (a string) into trimmed tokens, before matching them to the given regex pattern.
        /// </summary>
        /// <param name="filesListing">The file listing string.</param>
        private List<string> GetRemoteFileNameList(string filesListing, string pattern)
        {
            var input = filesListing.Split(new char[] { '\r', '\n' });
            Regex regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            List<string> files = new List<string>();
            foreach (var fileName in input)
            {
                var trim = fileName.Trim();
                if (regex.IsMatch(trim))
                {
                    files.Add(trim);
                }
            }
            return files;
        }
    }
}