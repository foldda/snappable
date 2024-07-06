using Foldda.Automation.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Util
{

    public static class CommonFileTasks
    {
        /// <summary>
        /// Returns a relative path string from a full path based on a base path
        /// provided.
        /// </summary>
        /// <param name="fullPath">The path to convert. Can be either a file or a directory</param>
        /// <param name="basePath">The base path on which relative processing is based. Should be a directory.</param>
        /// <returns>
        /// String of the relative path.
        /// 
        /// Examples of returned values:
        ///  test.txt, ..\test.txt, ..\..\..\test.txt, ., .., subdir\test.txt
        /// </returns>
        public static string GetRelativePath(string fullPath, string basePath)
        {
            // Require trailing backslash for path
            if (!basePath.EndsWith("\\")) { basePath += "\\"; }

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            // Uri's use forward slashes so convert back to backward slashes
            return relativeUri.ToString().Replace("/", "\\");

        }

        /// <summary>
        /// Generic file-moving method used by X..FileReader classes
        /// </summary>
        /// <param name="sourceFileNamePattern"></param>
        /// <param name="sourceFolderPath"></param>
        /// <param name="cancellationCheck"></param>
        /// <returns></returns>
        internal static async Task DirectoryScanTask(string sourceFileNamePattern, DirectoryInfo sourcePath, string targetFolderPath, Logger logger, CancellationToken cancellationCheck)
        {
            if (sourcePath == null || sourcePath.Exists == false)
            {
                logger.Log($"ERROR - Supplied source-path '{sourcePath.FullName}' does not exist.");

                return; // quit this task
            }

            await Task.Run(async () =>
            {
                //DirectoryInfo sourcePath = new DirectoryInfo(sourceFolderPath);

                //scan files from targeted source, if any matches, move to "home" folder
                try
                {
                    int movedCount = 0;
                    while (true)
                    {
                        foreach (var file in sourcePath.GetFiles(sourceFileNamePattern).OrderBy(f => f.LastWriteTime))
                        {
                            try
                            {
                                string fileName = file.Name;
                                file.LastWriteTime = DateTime.Now;  //touch the file to mark the processing time.
                                string targetFileName = $@"{targetFolderPath}\{fileName}";

                                await MultiRetryFileMoveTask(file, targetFileName);

                                movedCount++;
                                if (movedCount > 0)
                                {
                                    logger.Log($"Fetched {movedCount} files with name-pattern [{sourceFileNamePattern}], from path [{sourcePath.FullName}].");
                                }
                            }
                            catch
                            {
                                logger.Log($"Failed to move file '{file.FullName}' to target location '{targetFolderPath}'", LoggingLevel.Detailed);
                            }
                        }

                        //check for cancel/stop
                        cancellationCheck.ThrowIfCancellationRequested();   /* by node-stop or server-shutdown */
                        await Task.Delay(200);
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.Log($"Source-directory-scan task is stopped.");
                    //throw;
                }
                catch (Exception e)
                {
                    logger.Log(e);
                }
            });

            return;
            //... files in Home will be picked up by base.FetchDefault() task;
        }

        //this function is specially designated to avoid file-locking
        const int RETRY_LIMIT = 3;
        const int RETRY_DELAY_MS = 1000;
        static async Task MultiRetryFileMoveTask(FileInfo file, string targetFileName)
        {
            //https://stackoverflow.com/questions/26741191/ioexception-the-process-cannot-access-the-file-file-path-because-it-is-being
            int retry = 0;
            while (true)
            {
                try
                {
                    //if the same file exists, move it out of the way
                    if (File.Exists(targetFileName))
                    {
                        long timestamp = (long)DateTime.Now.ToUniversalTime().Subtract(new DateTime(2018, 1, 1)).TotalMilliseconds;

                        File.Move(targetFileName, $"{targetFileName}_{timestamp % Int32.MaxValue}");
                    }

                    file.MoveTo(targetFileName);    //may throw exception

                    break; // When done we can break loop
                }
                catch (IOException)
                {
                    if (retry++ > RETRY_LIMIT) { throw; }
                    await Task.Delay(RETRY_DELAY_MS);
                }
                catch
                {
                    throw new Exception($@"File [{file.Name}] cannot be moved to '{targetFileName}'.");
                }
            }
        }

    }

}
