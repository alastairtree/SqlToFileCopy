using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

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

        private async void CopyFilesButton_Click(object sender, EventArgs e)
        {
            if (FormIsInvalid()) return;

            CopyFilesButton.Enabled = false;

            var engine = new Engine(WriteLog);

            await engine.Start(ConnectionStringTextBox.Text, QueryTextBox.Text, DestinationFolderTextBox.Text);

            CopyFilesButton.Enabled = true;
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