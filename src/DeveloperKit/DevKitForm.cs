﻿
using Foldda.Automation.Framework;
using Foldda.Automation.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Foldda.Automation.HandlerDevKit
{
    public partial class DevKitForm : Form
    {
        public ILoggingProvider Logger { get; }

        //OS-level shutdown request
        private CancellationTokenSource AppShutdownCancellationSource { get; } = new CancellationTokenSource();

        public void Log(string message)
        {
            Logger?.Log(message);
            MessageStatusLabel.Text = message;
        }

        internal Task ControllerTask { get; set; }

        public string AssemblyPath { get; } = AppEnvironment.AssemblyDirectory;

        //helper
        private HandlerController GetHandlerController(object eventSender)
        {
            Control ctrl = eventSender as Control;
            if (ctrl != null)
            {
                // Get the control name
                string[] tokens = ctrl.Name.Split('_');
                string lastToken = tokens[tokens.Length - 1];
                if(int.TryParse(lastToken, out int index) && index > 0 && index <= 3) 
                {
                    return Controllers[index - 1];
                }
            }

            return null;
        }

        //backgroud thread that calls the _form.DrawXX at the interval and when necessary
        public Task RefreshModelsView(CancellationToken hostShutdownCancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!hostShutdownCancellationToken.IsCancellationRequested)
                    {
                        foreach(var controller in Controllers)
                        {
                            controller.RePaint(this);
                        }

                        await Task.Delay(200); //wait extra
                    }
                }
                catch (OperationCanceledException)
                {
                    Deb("Controler loop is cancelled.");
                }
                catch (Exception e)
                {
                    Log((string)("Exception -" + e.Message));
                    Log((string)e.StackTrace);
                }

                Log((string)"Controller loop exited!");

            });
        }

        internal const string FOLDDA_LOG_FOLDER_NAME = "[log]";
        internal static readonly string[] CONFIG_PATHS = new string[] { "N1_CONFIG_PATH", "N2_CONFIG_PATH", "N3_CONFIG_PATH" };

        public DevKitForm()
        {
            string assemblyName = typeof(Program).Assembly.GetName().Name;

            //setup logging ...
            string logFolder = Path.Combine(AssemblyPath, FOLDDA_LOG_FOLDER_NAME);
            Directory.CreateDirectory(logFolder);
            string logFileName = $@"{logFolder}\{assemblyName}";
            Logger = new FileLogger(logFileName);


            try
            {
                InitializeComponent();

                //assign image lists for customize controls.
                ImageList nodeImageList = new ImageList() { ImageSize = new Size(16, 16) };
                nodeImageList.Images.Add(HandlerModel.STATES[HandlerModel.ENTITY_STATE.NODE_STARTED], Properties.Resources.node_started);
                nodeImageList.Images.Add(HandlerModel.STATES[HandlerModel.ENTITY_STATE.NODE_STOPPED], Properties.Resources.node_stopped);
                nodeImageList.Images.Add(HandlerView.IMAGE_CONFIG, Properties.Resources.config);
                nodeImageList.Images.Add(HandlerView.IMAGE_TIME, Properties.Resources.time);

                _setupControls(LiveLogBox_1, NodeSettingsListView_1, nodeImageList);
                _setupControls(LiveLogBox_2, NodeSettingsListView_2, nodeImageList);
                _setupControls(LiveLogBox_3, NodeSettingsListView_3, nodeImageList);
            }
            catch (InvalidConfigException ce)
            {
                MessageBox.Show(ce.Message);
            }
            catch (Exception e)
            {
                MessageBox.Show($"There is an error starting Foldda - {e.Message}");
                Log(e);
            }
        }

        //helper
        private void _setupControls(RichTextBox liveLogBox_1, ListView nodeSettingsListView_1, ImageList nodeImageList)
        {
            //https://stackoverflow.com/questions/87795/how-to-prevent-flickering-in-listview-when-updating-a-single-listviewitems-text
            liveLogBox_1.DoubleBuffered(true);
            liveLogBox_1.AddContextMenu();

            nodeSettingsListView_1.DoubleBuffered(true);
            nodeSettingsListView_1.SmallImageList = nodeImageList;
        }

        internal void DrawHandlerLogView(RichTextBox handlerLoggingTextBox, HandlerView.LoggingPanel logPainter)
        {
            lock (logPainter)
            {
                handlerLoggingTextBox.InvokeIfRequired(() =>
                {
                    if (logPainter.HandlerModel == HandlerModel.DUMMY)
                    {
                        handlerLoggingTextBox.Text = string.Empty;
                        handlerLoggingTextBox.Parent.Text = "Handler Log";
                    }
                    else
                    {
                        handlerLoggingTextBox.Parent.Text = $"Logging - {logPainter.HandlerModel.Handler}";

                        string newLogText = logPainter.LogText;

                        if (newLogText != null && handlerLoggingTextBox.Text.Equals(newLogText) == false)
                        {

                            handlerLoggingTextBox.Text = newLogText;

                            List<string> highlightPatterns = logPainter.LogTextHighlightPatterns;
                            if (highlightPatterns.Count > 0)
                            {
                                foreach (var pattern in highlightPatterns)
                                {
                                    Regex _filterRegex = new Regex(pattern, RegexOptions.Compiled);

                                    foreach (Match match in Regex.Matches(handlerLoggingTextBox.Text, pattern/*, RegexOptions.IgnoreCase*/))
                                    {
                                        handlerLoggingTextBox.HighlightText(match.Value, Color.Yellow);
                                    }
                                }
                            }
                        }
                    }


                    handlerLoggingTextBox.SelectionStart = handlerLoggingTextBox.Text.Length;
                    // scroll it automatically
                    handlerLoggingTextBox.ScrollToCaret();

                });
            }
        }

        internal void DrawHandlerSettingsListView(ListView handlerSettingsListView, HandlerView.HandlerConfigPanel configPainter)
        {
            handlerSettingsListView.InvokeIfRequired(() =>
            {
                handlerSettingsListView.BeginUpdate();
                if (configPainter.HandlerModel == HandlerModel.DUMMY)
                {
                    handlerSettingsListView.Clear();
                }
                else
                {
                    lock (configPainter)
                    {
                        try
                        {
                            handlerSettingsListView.View = View.Details;
                            handlerSettingsListView.Columns.Clear();
                            handlerSettingsListView.Columns.Add(new ColumnHeader() { Text = string.Empty, Width = 100 });
                            handlerSettingsListView.Columns.Add(new ColumnHeader() { Text = string.Empty });
                            handlerSettingsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

                            handlerSettingsListView.Items.Clear();
                            handlerSettingsListView.Groups.Clear();

                            handlerSettingsListView.Columns[0].ImageKey = configPainter.HandlerModel.ImageKey;
                            handlerSettingsListView.Columns[0].Text = configPainter.HandlerModel.HandlerShortName;
                            //handlerSettingsListView.Columns[1].Text = configPainter.HandlerModel.HandlerStateString;

                            //NodeStateGroup
                            handlerSettingsListView.Groups.Add(configPainter.HandlerInfoGroup);
                            foreach (ListViewItem item in configPainter.HandlerInfoListViewItems)
                            {
                                handlerSettingsListView.Items.Add(item);
                            }

                            //optional - HandlerParameterGroup
                            if (configPainter.HandlerParametersListViewItems.Count > 0)
                            {
                                handlerSettingsListView.Groups.Add(configPainter.HandlerParameterGroup);
                                foreach (ListViewItem item in configPainter.HandlerParametersListViewItems)
                                {
                                    handlerSettingsListView.Items.Add(item);
                                }
                            }

                            handlerSettingsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                            handlerSettingsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                        }
                        catch (Exception e)
                        {
                            Log(e.Message);
                        }
                    }
                }

                handlerSettingsListView.EndUpdate();
                handlerSettingsListView.Invalidate();
            });
        }

        internal void DrawHandlerButtons(Button loadButton, Button startButton, Button stopButton,  Button unloadButton, HandlerView.HandlerButtonsPanel handlerButtonsPanel)
        {
            var handlerModel = handlerButtonsPanel.HandlerModel;
            if(handlerModel == HandlerModel.DUMMY)
            {
                loadButton.InvokeIfRequired(() =>
                {
                    loadButton.Enabled = true;
                    loadButton.Text = "Load";
                });

                unloadButton.InvokeIfRequired(() =>
                {
                    unloadButton.Enabled = false;
                    unloadButton.Text = "UNLOADED";
                });

                startButton.InvokeIfRequired(() =>
                {
                    startButton.Enabled = false;
                    startButton.Text = "Start";
                });

                stopButton.InvokeIfRequired(() =>
                {
                    stopButton.Enabled = false;
                    stopButton.Text = "Stop";
                });
            }
            else
            {
                loadButton.InvokeIfRequired(() =>
                {
                    loadButton.Enabled = false;
                    loadButton.Text = "LOADED";
                });

                unloadButton.InvokeIfRequired(() =>
                {
                    unloadButton.Enabled = true;
                    unloadButton.Text = "Unload";
                });

                if (handlerModel.CurrentState == HandlerModel.ENTITY_STATE.NODE_STOPPED)
                {
                    startButton.InvokeIfRequired(() =>
                    {
                        startButton.Enabled = true;
                        startButton.Text = "Start";
                    });

                    stopButton.InvokeIfRequired(() =>
                    {
                        stopButton.Enabled = false;
                        stopButton.Text = "STOPPED";
                    });
                }
                else if (handlerModel.CurrentState == HandlerModel.ENTITY_STATE.NODE_STARTED)
                {
                    startButton.InvokeIfRequired(() =>
                    {
                        startButton.Enabled = false;
                        startButton.Text = "STARTED";
                    });

                    stopButton.InvokeIfRequired(() =>
                    {
                        stopButton.Enabled = true;
                        stopButton.Text = "Stop";
                    });
                }
            }

            this.ActiveControl = null;
        }


        //IDebug
        public void Log(Exception e)
        {
            this.Log(e.ToString());
        }

        private void LinkButton1_Click(object sender, EventArgs e)
        {
            LaunchURL("https://foldda.com");
        }

        private void LaunchURL(string url)
        {
            Process.Start(new ProcessStartInfo(url) 
            { 
                UseShellExecute = true 
            });
        }

        private void LinkButton2_Click(object sender, EventArgs e)
        {
            LaunchURL("https://github.com/foldda");
        }

        public void Deb(string message)
        {
#if DEBUG
            Log(message);
#endif
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            AppShutdownCancellationSource.Cancel();
        }

        internal List<HandlerController> Controllers { get; private set; } 

        private void MainForm_Load(object sender, EventArgs e)
        {
            int store_id = 0;
            DataStore DataStore_0 = new DataStore(store_id++);  //first inbound (not used)
            DataStore DataStore_1 = new DataStore(store_id++);
            DataStore DataStore_2 = new DataStore(store_id++);
            DataStore DataStore_3 = new DataStore(store_id++);  //last outbound (not used)
            DataStore EventStore_common = new DataStore(store_id++);  //shared event store

            //3 handler controllers, each gets assigned with input/output data stores and front-end views
            Controllers = new List<HandlerController>()
            {
                new HandlerController(this, DataStore_0, DataStore_1, EventStore_common, LiveLogBox_1, NodeSettingsListView_1, new Button[] { LoadButton_1, StartButton_1, StopButton_1, ClearButton_1 }, 0),
                new HandlerController(this, DataStore_1, DataStore_2, EventStore_common, LiveLogBox_2, NodeSettingsListView_2, new Button[] { LoadButton_2, StartButton_2, StopButton_2, ClearButton_2 }, 1),
                new HandlerController(this, DataStore_2, DataStore_3, EventStore_common, LiveLogBox_3, NodeSettingsListView_3, new Button[] { LoadButton_3, StartButton_3, StopButton_3, ClearButton_3 }, 2)
            };

            ControllerTask = RefreshModelsView(AppShutdownCancellationSource.Token);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {

            OpenFileDialog openFileDialog1 = new OpenFileDialog()
            {
                Filter = "Config files (*.config)|*.config",
                Title = "Select a handler-config file"
            };

            var controller = GetHandlerController(sender);
            var savedConfig = ConfigSettings.Get(CONFIG_PATHS[controller.Index0]);
            if(!string.IsNullOrEmpty(savedConfig))
            {
                FileInfo file_info = new FileInfo(savedConfig);
                openFileDialog1.FileName = file_info.Name;
                openFileDialog1.InitialDirectory = file_info.DirectoryName;
            }

            //file open dialog
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if(File.Exists(openFileDialog1.FileName))
                    {
                        controller.UpdateCurrentNode(openFileDialog1.FileName);
                    }
                    else
                    {
                        throw new Exception("Config file does not exist.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error constructing handler: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                GetHandlerController(sender).Start(AppShutdownCancellationSource.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting handler: {ex.Message}\n\n" +
                $"Details:\n\n{ex.StackTrace}");
            }
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            try
            {
                await GetHandlerController(sender).Stop(); ;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping handler: {ex.Message}\n\n" +
                $"Details:\n\n{ex.StackTrace}");
            }
        }

        //This is the "Unload" button
        private async Task ClearButton_ClickAsync(object sender, EventArgs e)
        {
            try
            {
                var handlerController = GetHandlerController(sender);
                await handlerController.Stop();
                handlerController.Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing handler: {ex.Message}\n\n" +
                $"Details:\n\n{ex.StackTrace}");
            }

        }

        private void NodeSettingsListView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Clicks == 2)
            {
                HandlerModel handlerModel = GetHandlerController(sender).HandlerModel;
                if (handlerModel != null && !(handlerModel == HandlerModel.DUMMY) && File.Exists(handlerModel.HandlerConfig.ConfigFileFullPath))
                {
                    Process.Start("notepad.exe", handlerModel.HandlerConfig.ConfigFileFullPath);
                }
            }
        }

        private void startAllButton_Click(object sender, EventArgs e)
        {
            foreach(var controller in Controllers)
            {
                try
                {
                    controller.Start(AppShutdownCancellationSource.Token);
                }
                catch(Exception ex) 
                {
                    Log($"Handler [{controller.Snappable.Name}] failed starting - {ex.Message}");
                }
            }
        }

        private async void stopAllButton_Click(object sender, EventArgs e)
        {
            foreach (var controller in Controllers)
            {
                try
                {
                    await controller.Stop();
                }
                catch (Exception ex)
                {
                    Log($"Handler [{controller.Snappable.Name}] failed stopping - {ex.Message}");
                }
            }
        }
    }
}
