using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentData;

namespace SqlToFileCopy
{
    public partial class Main : Form
    {
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

        private void CopyFilesButton_Click(object sender, EventArgs e)
        {
            if (FormIsInvalid()) return;

            Task.Factory.StartNew(() =>
            {
                var files = GetFileListFromDatabase();

                if (files != null)
                {
                    CopyFilesToDestination(files, DestinationFolderTextBox.Text);
                }
            });
        }

        private void CopyFilesToDestination(ICollection<string> files, string destination)
        {
            var sucessCount = 0;
            foreach (var sourceFilePath in files)
            {
                var destinationFilePath = GenerateDestinationPath(sourceFilePath, destination);

                if (!File.Exists(sourceFilePath))
                {
                    WriteLog("Error: Source file missing " + sourceFilePath);
                    continue;
                }

                CopyFile(destinationFilePath, sourceFilePath);
                sucessCount++;
            }

            if (sucessCount == files.Count())
                WriteLog("All files copied sucessfully");
            else
                WriteLog(string.Format("{0}/{1} files copied sucessfully.", sucessCount, files.Count()));
        }

        private void CopyFile(string destinationFilePath, string sourceFilePath)
        {
            if (File.Exists(destinationFilePath)
                && File.GetLastWriteTimeUtc(destinationFilePath) == File.GetLastWriteTimeUtc(destinationFilePath)
                && new FileInfo(destinationFilePath).Length == new FileInfo(destinationFilePath).Length)
            {
                WriteLog("Existing file is the same as the source, skipping it:" + destinationFilePath);
            }
            else
            {
                if (File.Exists(destinationFilePath))
                    WriteLog("Note: Destination file already exists, will be overwritten: " + destinationFilePath);
                else
                    CreateMissingFolders(destinationFilePath);

                File.Copy(sourceFilePath, destinationFilePath, true);
                WriteLog(String.Format("File copied from {0} to {1}", sourceFilePath, destinationFilePath));
            }
        }

        private static string GenerateDestinationPath(string file, string destination)
        {
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