using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentData;

namespace SqlToFileCopy
{
    public partial class Main : Form
    {
        private const string WebBasedFilePathMatcher = @"^(https?://)|(www\.)";

        public Main()
        {
            LogData = new BindingList<LogMessage>();
            InitializeComponent();
            var source = new BindingSource(LogData, null);
            LogTable.DataSource = source;
        }

        private BindingList<LogMessage> LogData { get; set; }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog
            {
                SelectedPath = DestinationFolderTextBox.Text,
                ShowNewFolderButton = true
            };

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                DestinationFolderTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private async void CopyFilesButton_Click(object sender, EventArgs e)
        {
            if (FormIsInvalid()) return;

            CopyFilesButton.Enabled = false;

            await Task.Factory.StartNew(() =>
            {
                var files = GetFileListFromDatabase();

                if (files != null)
                {
                    CopyFilesToDestination(files, DestinationFolderTextBox.Text);
                }

            });
            
            CopyFilesButton.Enabled = true;
        }

        private async void CopyFilesToDestination(ICollection<string> files, string destination)
        {
            var sucessCount = 0;
            foreach (var originalSourceFilePath in files.Where(x=> !string.IsNullOrEmpty(x)))
            {
                var sourceFilePath = await ProcessForHttpFiles(originalSourceFilePath);

                var destinationFilePath = GenerateDestinationPath(originalSourceFilePath, destination);

                if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                {
                    WriteLog("Error: Source file missing " + originalSourceFilePath);
                    continue;
                }

                if(CopyFile(sourceFilePath, destinationFilePath))
                    WriteLog(String.Format("File copied from {0} to {1}", originalSourceFilePath, destinationFilePath));

                sucessCount++;
            }

            if (sucessCount == files.Count())
                WriteLog("All files copied sucessfully");
            else
                WriteLog(string.Format("{0}/{1} files copied sucessfully.", sucessCount, files.Count()));
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
                WriteLog("Error downloading " + sourceFilePath + " - " + ex.ToString());
                return "";
            }
        }

        private bool CopyFile(string sourceFilePath, string destinationFilePath)
        {
            if (File.Exists(destinationFilePath)
                && File.GetLastWriteTimeUtc(destinationFilePath) == File.GetLastWriteTimeUtc(destinationFilePath)
                && new FileInfo(destinationFilePath).Length == new FileInfo(destinationFilePath).Length)
            {
                WriteLog("Existing file is the same as the source, skipping it:" + destinationFilePath);
                return false;
            }
            
            if (File.Exists(destinationFilePath))
                WriteLog("Note: Destination file already exists, will be overwritten: " + destinationFilePath);
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

        private ICollection<string> GetFileListFromDatabase()
        {
            var context = new DbContext()
                .ConnectionString(ConnectionStringTextBox.Text,
                    new SqlServerProvider());

            List<string> files = null;
            try
            {
                files = context.Sql(QueryTextBox.Text).QueryMany<string>();
                WriteLog("Completed query, found " + files.Count + " records");
            }
            catch (Exception ex)
            {
                WriteLog("Executing query failed with error: " + ex);
            }
            return files;
        }

        private bool FormIsInvalid()
        {
            if (String.IsNullOrEmpty(ConnectionStringTextBox.Text))
            {
                WriteLog("Missing connection string");
                return true;
            }
            if (String.IsNullOrEmpty(QueryTextBox.Text))
            {
                WriteLog("Missing query");
                return true;
            }
            if (String.IsNullOrEmpty(DestinationFolderTextBox.Text))
            {
                WriteLog("Missing destination");
                return true;
            }
            if (!Directory.Exists(DestinationFolderTextBox.Text))
            {
                WriteLog("Destination does not exist");
                return true;
            }
            return false;
        }

        private void WriteLog(string msg)
        {
            LogTable.BeginInvoke(new Action(() =>
            {
                LogData.Add(new LogMessage {Time = DateTime.Now.ToString("dd MMM HH:mm"), Message = msg});
                LogTable.Refresh();
                LogTable.FirstDisplayedScrollingRowIndex = LogTable.RowCount - 1;
            }));
        }
    }
}