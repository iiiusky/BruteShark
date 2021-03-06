﻿using BruteSharkDesktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BruteSharkDesktop
{
    public partial class MainForm : Form
    {
        private HashSet<string> _files;
        private PcapProcessor.Processor _processor;
        private PcapAnalyzer.Analyzer _analyzer;

        private GenericTableUserControl _passwordsUserControl;
        private HashesUserControl _hashesUserControl;
        private NetworkMapUserControl _networkMapUserControl;
        private SessionsExplorerUserControl _sessionsExplorerUserControl;
        private FilesUserControl _filesUserControl;


        public MainForm()
        {
            InitializeComponent();

            _files = new HashSet<string>();

            // Create the DAL and BLL objects.
            _processor = new PcapProcessor.Processor();
            _analyzer = new PcapAnalyzer.Analyzer();
            _processor.BuildTcpSessions = true;

            // Create the user controls. 
            _networkMapUserControl = new NetworkMapUserControl();
            _networkMapUserControl.Dock = DockStyle.Fill;
            _sessionsExplorerUserControl = new SessionsExplorerUserControl();
            _sessionsExplorerUserControl.Dock = DockStyle.Fill;
            _hashesUserControl = new HashesUserControl();
            _hashesUserControl.Dock = DockStyle.Fill;
            _passwordsUserControl = new GenericTableUserControl();
            _passwordsUserControl.Dock = DockStyle.Fill;
            _filesUserControl = new FilesUserControl();
            _filesUserControl.Dock = DockStyle.Fill;

            // Contract the events.
            _processor.UdpPacketArived += (s, e) => _analyzer.Analyze(Casting.CastProcessorUdpPacketToAnalyzerUdpPacket(e.Packet));
            _processor.TcpPacketArived += (s, e) => _analyzer.Analyze(Casting.CastProcessorTcpPacketToAnalyzerTcpPacket(e.Packet));
            _processor.TcpSessionArived += (s, e) => _analyzer.Analyze(Casting.CastProcessorTcpSessionToAnalyzerTcpSession(e.TcpSession));
            _processor.FileProcessingStarted += (s, e) => SwitchToMainThreadContext(() => OnFileProcessStart(s, e));
            _processor.FileProcessingEnded += (s, e) => SwitchToMainThreadContext(() => OnFileProcessEnd(s, e));
            _processor.ProcessingPrecentsChanged += (s, e) => SwitchToMainThreadContext(() => OnProcessingPrecentsChanged(s, e));
            _analyzer.ParsedItemDetected += (s, e) => SwitchToMainThreadContext(() => OnParsedItemDetected(s, e));
            _processor.TcpSessionArived += (s, e) => SwitchToMainThreadContext(() => OnSessionArived(Casting.CastProcessorTcpSessionToBruteSharkDesktopTcpSession(e.TcpSession)));
            _processor.ProcessingFinished += (s, e) => SwitchToMainThreadContext(() => OnProcessingFinished(s, e));

            InitilizeFilesIconsList();
            InitilizeModulesCheckedListBox();
            this.modulesTreeView.ExpandAll();
        }

        private void InitilizeModulesCheckedListBox()
        {
            foreach (var module_name in _analyzer.AvailableModulesNames)
            {
                this.modulesCheckedListBox.Items.Add(module_name, isChecked: false);
            }
        }

        private void OnProcessingFinished(object sender, EventArgs e)
        {
            this.progressBar.Value = this.progressBar.Maximum;
        }

        private void OnSessionArived(TcpSession tcpSession)
        {
            _sessionsExplorerUserControl.AddSession(tcpSession);
            this.modulesTreeView.Nodes["NetworkNode"].Nodes["SessionsNode"].Text = $"Sessions ({_sessionsExplorerUserControl.SessionsCount})";
        }

        private void SwitchToMainThreadContext(Action func)
        {
            // Thread-Safe mechanizm:
            // Check if we are currently runing in a different thread than the one that 
            // control was created on, if so we invoke a call to our function again, but because 
            // we used the invoke method again from our form the caller this time will be the 
            // the thread that created the form.
            // For more detailes: 
            // https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
            if (InvokeRequired)
            {
                Invoke(func);
                return;
            } 

            Invoke(func);
        }

        private void OnProcessingPrecentsChanged(object sender, PcapProcessor.ProcessingPrecentsChangedEventArgs e)
        {
            if (e.Precents <= 90)
            {
                this.progressBar.Value = e.Precents;
            }
        }

        private void OnFileProcessEnd(object sender, PcapProcessor.FileProcessingEndedEventArgs e)
        {
            var currentFileListViewItem = this.filesListView.FindItemWithText(
                Path.GetFileName(e.FilePath),
                true,
                0,
                false);

            currentFileListViewItem.ForeColor = Color.Blue;
            currentFileListViewItem.SubItems[2].Text = "Analyzed";
        }

        private void OnFileProcessStart(object sender, PcapProcessor.FileProcessingStartedEventArgs e)
        {
            var currentFileListViewItem = this.filesListView.FindItemWithText(
                Path.GetFileName(e.FilePath),
                true,
                0,
                false);

            currentFileListViewItem.ForeColor = Color.Red;
            currentFileListViewItem.SubItems[2].Text = "On Process..";
        }

        private void OnFileProcessException(string filePath)
        {
            var currentFileListViewItem = this.filesListView.FindItemWithText(
                Path.GetFileName(filePath),
                true,
                0,
                false);

            currentFileListViewItem.ForeColor = Color.Purple;
            currentFileListViewItem.SubItems[2].Text = "Failed";
        }

        private void InitilizeFilesIconsList()
        {
            this.filesListView.SmallImageList = new ImageList();
            ImageList imgList = new ImageList();
            imgList.ImageSize = new Size(22, 22);
            imgList.Images.Add(Properties.Resources.Wireshark_Icon);
            this.filesListView.SmallImageList = imgList;
        }

        private void OnParsedItemDetected(object sender, PcapAnalyzer.ParsedItemDetectedEventArgs e)
        {
            if (e.ParsedItem is PcapAnalyzer.NetworkPassword)
            {
                var password = e.ParsedItem as PcapAnalyzer.NetworkPassword;
                _passwordsUserControl.AddDataToTable(password);
                this.modulesTreeView.Nodes["CredentialsNode"].Nodes["PasswordsNode"].Text = $"Passwords ({_passwordsUserControl.ItemsCount})";
                _networkMapUserControl.HandlePassword(password);
            }
            else if (e.ParsedItem is PcapAnalyzer.NetworkHash)
            {
                var hash = e.ParsedItem as PcapAnalyzer.NetworkHash;
                _hashesUserControl.AddHash(hash);
                this.modulesTreeView.Nodes["CredentialsNode"].Nodes["HashesNode"].Text = $"Hashes ({_hashesUserControl.HashesCount})";
                _networkMapUserControl.HandleHash(hash);
            }
            else if (e.ParsedItem is PcapAnalyzer.NetworkConnection)
            {
                var connection = e.ParsedItem as PcapAnalyzer.NetworkConnection;
                _networkMapUserControl.AddEdge(connection.Source, connection.Destination);
                this.modulesTreeView.Nodes["NetworkNode"].Nodes["NetworkMapNode"].Text = $"Network Map ({_networkMapUserControl.NodesCount})";
            }
            else if (e.ParsedItem is PcapAnalyzer.NetworkFile)
            {
                var fileObject = e.ParsedItem as PcapAnalyzer.NetworkFile;
                _filesUserControl.AddFile(fileObject);
                this.modulesTreeView.Nodes["DataNode"].Nodes["FilesNode"].Text = $"Files ({_filesUserControl.FilesCount})";
            }
        }

        private void addFilesButton_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    addFile(filePath);
                }
            }
        }

        private void addFile(string filePath)
        {
            _files.Add(filePath);

            var listViewRow = new ListViewItem(
                new string[]
                {
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length.ToString(),
                    "Wait"
                }
                , 0);

            // TODO: think of binding
            listViewRow.Tag = filePath;
            this.filesListView.Items.Add(listViewRow);
        }

        private void runButton_Click(object sender, EventArgs e)
        {
            new Thread(() => _processor.ProcessPcaps(this._files)).Start();
        }

        private void modulesTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            this.modulesSplitContainer.Panel2.Controls.Clear();

            switch (e.Node.Name)
            {
                case "PasswordsNode":
                    this.modulesSplitContainer.Panel2.Controls.Add(_passwordsUserControl);
                    break;
                case "HashesNode":
                    this.modulesSplitContainer.Panel2.Controls.Add(_hashesUserControl);
                    break;
                case "NetworkMapNode":
                    this.modulesSplitContainer.Panel2.Controls.Add(_networkMapUserControl);
                    break;
                case "SessionsNode":
                    this.modulesSplitContainer.Panel2.Controls.Add(_sessionsExplorerUserControl);
                    break;
                case "FilesNode":
                    this.modulesSplitContainer.Panel2.Controls.Add(_filesUserControl);
                    break;
                default:
                    break;
            }
        }

        private void removeFilesButton_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in filesListView.SelectedItems)
            {
                _files.Remove(item.Tag.ToString());
                item.Remove();
            } 
        }

        private void modulesCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var module_name = ((CheckedListBox)sender).Text;

            if (e.NewValue == CheckState.Checked)
            {
                _analyzer.AddModule(module_name);
            }
            else
            {
                _analyzer.RemoveModule(module_name);
            }
        }

    }
}
    