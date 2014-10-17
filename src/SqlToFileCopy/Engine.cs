using System;
using System.Collections.Generic;
using System.Data;
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
        private readonly Lazy<HttpClient> client;

        public Engine(Action<string> logger)
        {
            this.logger = logger;
            client = new Lazy<HttpClient>(() => new HttpClient());
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

        public Task StartWithDestinationsInQuery(string connectionString, string query)
        {
            return Task.Factory.StartNew(() =>
            {
                var fileMapping = GetFileMappingFromDatabase(connectionString, query);

                if (fileMapping != null)
                {
                    CopyFilesToDestination(fileMapping.ToArray());
                }
            });
        }


        private async void CopyFilesToDestination(ICollection<string> files, string destination)
        {
            var sucessCount = 0;
            foreach (var originalSourceFilePath in files.Where(x => !String.IsNullOrEmpty(x)))
            {
                if (await ProcessFileCopyRequest(originalSourceFilePath, destination, true))
                    sucessCount++;
            }

            if (sucessCount == files.Count())
                logger("All files copied sucessfully");
            else
                logger(String.Format("{0}/{1} files copied sucessfully.", sucessCount, files.Count()));
        }

        private async void CopyFilesToDestination(ICollection<Tuple<string,string>> filesSourceDestinationMap)
        {
            var sucessCount = 0;
            foreach (var originalSourceFilePath in filesSourceDestinationMap.Where(x => !String.IsNullOrEmpty(x.Item1)))
            {
                if (await ProcessFileCopyRequest(originalSourceFilePath.Item1, originalSourceFilePath.Item2))
                    sucessCount++;
            }

            if (sucessCount == filesSourceDestinationMap.Count())
                logger("All files copied sucessfully");
            else
                logger(String.Format("{0}/{1} files copied sucessfully.", sucessCount, filesSourceDestinationMap.Count()));
        }

        private async Task<bool> ProcessFileCopyRequest(string sourcePath, string destinationPath, bool maintainSourceFolders = false)
        {
            var sourceFilePath = await ProcessForHttpFiles(sourcePath);

            var destinationFilePath = maintainSourceFolders ? GenerateDestinationPath(sourcePath, destinationPath) : destinationPath;

            if (String.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                logger("Error: Source file missing " + sourcePath);
                return false;
            }

            if (ExecuteFileCopy(sourceFilePath, destinationFilePath))
                logger(String.Format("File copied from {0} to {1}", sourcePath, destinationFilePath));

            return true;
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
            try
            {
                return await client.Value.GetByteArrayAsync(sourceFilePath)
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

        private bool ExecuteFileCopy(string sourceFilePath, string destinationFilePath)
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

        private IEnumerable<Tuple<string,string>> GetFileMappingFromDatabase(string connectionString, string query)
        {
            var context = new DbContext()
                .ConnectionString(connectionString, new SqlServerProvider())
                .CommandTimeout(300);

            IEnumerable<Tuple<string, string>> items = null;
            try
            {
                var table = context.Sql(query).QuerySingle<DataTable>();
                logger("Completed query, found " + table.Rows.Count + " records");
                items = ExtractSourceDestinationFromDataTable(table);
            }
            catch (Exception ex)
            {
                logger("Executing query failed with error: " + ex);
            }
            return items;
        }

        private IEnumerable<Tuple<string, string>> ExtractSourceDestinationFromDataTable(DataTable table)
        {
            if (table.Columns.Count < 2)
            {
                logger("Need 2 columns, source and destination, to use alternative destination");
                return null;
            }

            var items = new Tuple<string, string>[table.Rows.Count];
            for (var i = 0; i < table.Rows.Count; i++)
                items[i] = Tuple.Create(table.Rows[i][0].ToString(), table.Rows[i][1].ToString());

            return items;
        }
    }
}