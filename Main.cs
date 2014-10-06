using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentData;

namespace SqlToFileCopy
{
    public partial class Main : Form
    {
        public Main()
        {
            LogData = new List<object>();
            InitializeComponent();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            // New FolderBrowserDialog instance
            FolderBrowserDialog Fld = new FolderBrowserDialog();

            // Set initial selected folder
            Fld.SelectedPath = DestinationFolderTextBox.Text;

            // Show the "Make new folder" button
            Fld.ShowNewFolderButton = true;

            if (Fld.ShowDialog() == DialogResult.OK)
            {
                // Select successful
                DestinationFolderTextBox.Text = Fld.SelectedPath;
            }
        }

        private void CopyFilesButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(ConnectionStringTextBox.Text))
            {
                WriteLog("Missing connection string");
                return;
            }
            if (String.IsNullOrEmpty(QueryTextBox.Text))
            {
                WriteLog("Missing query");
                return;
            }
            if (String.IsNullOrEmpty(DestinationFolderTextBox.Text))
            {
                WriteLog("Missing destination");
                return;
            }

            var destination = new DirectoryInfo(DestinationFolderTextBox.Text);
            if (!destination.Exists)
            {
                WriteLog("Destination does not exist");
                return;
            }

            IDbContext context = new DbContext()
                .ConnectionString(ConnectionStringTextBox.Text,
                new SqlServerProvider());

            List<string> files;
            try
            {
                files = context.Sql(QueryTextBox.Text).QueryMany<string>();
                WriteLog("Completed query, found " + files.Count + " records");
            }
            catch (Exception ex)
            {
                WriteLog("Executing query failed with error: " + ex);
            }

        }

        private void WriteLog(string msg)
        {
            LogData.Add(new{ Time = DateTime.Now.ToString("dd MMM HH:mm"), Message =  msg});
            LogTable.DataSource = LogData;
        }

        public List<object> LogData
        { get; set; }
    }
}
