using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentData;

namespace SqlToFileCopy
{
    public class Engine
    {
        private const string WebBasedFilePathMatcher = @"^(https?://)|(www\.)";

        private readonly Action<string> logger;

        public Engine(Action<string> logger)
        {
            this.logger = logger;
        }

        public Task Start(string connectionString, string query, string destinationPath)
        {
            return Task.Factory.StartNew(() =>
            {
                var files = GetFileListFromDatabase(connectionString, query);

                if (files != null)
                {
                    CopyFilesToDestination(files, destinationPath);
                }
            });
        }


        private async void CopyFilesToDestination(ICollection<string> files, string destination)
        {
            var sucessCount = 0;
            foreach (var originalSourceFilePath in files.Where(x => !String.IsNullOrEmpty(x)))
            {
                var sourceFilePath = await ProcessForHttpFiles(originalSourceFilePath);

                var destinationFilePath = GenerateDestinationPath(originalSourceFilePath, destination);

                if (String.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                {
                    logger("Error: Source file missing " + originalSourceFilePath);
                    continue;
                }

                if (CopyFile(sourceFilePath, destinationFilePath))
                    logger(String.Format("File copied from {0} to {1}", originalSourceFilePath, destinationFilePath));

                sucessCount++;
            }

            if (sucessCount == files.Count())
                logger("All files copied sucessfully");
            else
                logger(String.Format("{0}/{1} files copied sucessfully.", sucessCount, files.Count()));
        }

        private async Task<string> ProcessForHttpFiles(string sourceFilePath)
        {
            if (Regex.IsMatch(sourceFilePath, WebBasedFilePathMatcher, RegexOptions.IgnoreCase))
            {
                return await DownloadWebFile(sourceFilePath);
            }

            return sourceFilePath;
        }

        private async Task<string> DownloadWebFile(string sourceFilePath)
        {
            var client = new HttpClient();
            try
            {
                return await client.GetByteArrayAsync(sourceFilePath)
                    .ContinueWith(x =>
                    {
                        var tmp = Path.GetTempFileName();
                        File.WriteAllBytes(tmp, x.Result);
                        return tmp;
                    });
            }
            catch (Exception ex)
            {
                logger("Error downloading " + sourceFilePath + " - " + ex);
                return "";
            }
        }

        private bool CopyFile(string sourceFilePath, string destinationFilePath)
        {
            if (File.Exists(destinationFilePath)
                && File.GetLastWriteTimeUtc(destinationFilePath) == File.GetLastWriteTimeUtc(destinationFilePath)
                && new FileInfo(destinationFilePath).Length == new FileInfo(destinationFilePath).Length)
            {
                logger("Existing file is the same as the source, skipping it:" + destinationFilePath);
                return false;
            }

            if (File.Exists(destinationFilePath))
                logger("Note: Destination file already exists, will be overwritten: " + destinationFilePath);
            else
                CreateMissingFolders(destinationFilePath);

            File.Copy(sourceFilePath, destinationFilePath, true);
            return true;
        }

        private static string GenerateDestinationPath(string file, string destination)
        {
            if (Regex.IsMatch(file, WebBasedFilePathMatcher, RegexOptions.IgnoreCase))
            {
                file = @"\\server\" + file.Replace("http://", "")
                    .Replace("https://", "")
                    .Replace("/", "\\");
            }

            var driveReplacer = new Regex(@"[A-Za-z]:\\");
            var uncReplacer = new Regex(@"\\\\[A-Za-z0-9.\-_]+\\");

            var destinationFilePathEnding = uncReplacer.Replace(driveReplacer.Replace(file, ""), "");
            return Path.Combine(destination, destinationFilePathEnding);
        }

        private static void CreateMissingFolders(string file)
        {
            for (var i = 3; i < file.Length; i++)
            {
                if (file[i] == '\\')
                {
                    var folder = file.Substring(0, i + 1);
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                }
            }
        }

        private ICollection<string> GetFileListFromDatabase(string connectionString, string query)
        {
            var context = new DbContext()
                .ConnectionString(connectionString, new SqlServerProvider());

            List<string> files = null;
            try
            {
                files = context.Sql(query).QueryMany<string>();
                logger("Completed query, found " + files.Count + " records");
            }
            catch (Exception ex)
            {
                logger("Executing query failed with error: " + ex);
            }
            return files;
        }
    }
}