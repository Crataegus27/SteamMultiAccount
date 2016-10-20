﻿using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using SteamKit2;
using System.Threading.Tasks;

namespace SteamMultiAccount
{
    public partial class SMAForm : Form
    {
        internal const string ConfigDirectory = "config";
        internal const string DebugDirectory = "debug";
        internal const string ServerList = ConfigDirectory + "/servers.bin";
        internal const string BotsData = ConfigDirectory + "/botData";
        private bool bWantClose = false;
        private FormWindowState _LastState = FormWindowState.Normal;
        public SMAForm()
        {
            InitializeComponent();
            StatusLabel.Text = string.Empty;
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
            if (Directory.Exists(DebugDirectory)) {
                Directory.Delete(DebugDirectory, true);
                Thread.Sleep(1000); // Dirty workaround giving Windows some time to sync
            }
            if (!Directory.Exists(DebugDirectory))
                Directory.CreateDirectory(DebugDirectory);
            if (!Directory.Exists(BotsData))
                Directory.CreateDirectory(BotsData);

            DebugLog.AddListener(new Listener(null));
            DebugLog.Enabled = true;

            textBox1.AutoCompleteCustomSource.AddRange(Bot.CommandsKeys);
            notifyIconMain.Icon = System.Drawing.SystemIcons.Application;

            // TODO: Getting game from gleam.io
        }

        private async Task StartBots()
        {
            if (!Directory.Exists(ConfigDirectory))
                return;
            if(Directory.GetFiles(ConfigDirectory,"*.json").Length>0)
            {
                foreach (var configFile in Directory.EnumerateFiles(ConfigDirectory, "*.json"))
                {
                    string botName = Path.GetFileNameWithoutExtension(configFile);
                    switch (botName)
                    {
                        case "Program":
                            continue;
                    }
                    if (botName == null)
                        return;
                    Bot bot = new Bot(botName,this);
                    BotList.BeginUpdate();
                    try
                    {
                        BotList.Invoke(new MethodInvoker(delegate
                        {
                            BotList.Items.Add(botName);
                        }));
                    } catch(Exception e)
                    {
                        Logging.LogToFile("Cant add bot to bot list: " + e);
                    }
                    BotList.EndUpdate();
                    if(bot.BotConfig.Enabled)
                        await Task.Delay(5000); // Wait 5 sec before start next bot
                }
            }
        }
        /*
         * 
         * Events
         * 
         */
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (!bWantClose)
                {
                    e.Cancel = true; // Cancel if user click on X button
                    this.Hide();
                    _LastState = this.WindowState;
                }
            }
            base.OnFormClosing(e);
        }
        private async void SMAForm_Shown(object sender, EventArgs e)
        {
            await StartBots();
        }
        private void LogBox_Click(object sender, EventArgs e)
        {
            if (LogBox.SelectionLength > 0)
                return;
            textBox1.Select();
        }
        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BotSettings SettingsForm = new BotSettings(this);
            if(!SettingsForm.wantclose)
            SettingsForm.Show();
        }
        private void BotList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int y = e.Y / ((ListBox) sender).ItemHeight;
                if (y < ((ListBox) sender).Items.Count)
                { 
                    ((ListBox) sender).SelectedIndex = y;
                    contextMenuStripMain.Items[1].Visible = true;
                    contextMenuStripMain.Items[2].Visible = true;
                }
                else
                { 
                    ((ListBox) sender).SelectedIndex = -1;
                    contextMenuStripMain.Items[1].Visible = false;
                    contextMenuStripMain.Items[2].Visible = false;
                }
            }
        }
        private void changeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BotSettings SettingForm = new BotSettings(this,BotList.SelectedItem.ToString());
            SettingForm.Show();
        }
        private void BotList_SelectedIndexChanged(object sender, EventArgs e)
        {
            Bot bot;
            if ((sender as ListBox).SelectedIndex == -1)
                bot = null;
            else            
            Bot.Bots.TryGetValue((sender as ListBox).SelectedItem.ToString(), out bot);

            UpdateAll(bot);
        }
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (string.IsNullOrEmpty(textBox1.Text))
                    return;
                Bot bot;
                if (!Bot.Bots.TryGetValue(BotList.SelectedItem.ToString(), out bot))
                    return;
                bot.Log(textBox1.Text, LogType.User);
                bot.Response(textBox1.Text);
                UpdateLogBox(bot);
                textBox1.Text = string.Empty;
            }
        }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Bot bot;
            if (!Bot.Bots.TryGetValue(BotList.SelectedItem.ToString(), out bot))
                return;
            bot.Delete();
            bot = null;
            BotList.Items.Remove(BotList.SelectedItem);
            if (BotList.Items.Count > 0)
                BotList.SelectedIndex = 0;
            else
                BotList.SelectedIndex = -1;
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (BotList.SelectedItem == null)
                return;

            Bot bot;
            if (!Bot.Bots.TryGetValue(BotList.SelectedItem.ToString(), out bot))
                return;

            UpdateAll(bot);
        }
        /*
        *
        * Services
        *
        */
        internal void UpdateLogBox(Bot bot)
        {
            if (bot == null)
            {
                LogBox.Clear();
                return;
            }
            string text = bot.getLogBoxText();
            if (LogBox.Rtf == text)
                return;
            LogBox.Rtf = text;
            LogBox.SelectionStart = LogBox.Text.Length;
            LogBox.ScrollToCaret();
        }
        internal void UpdateStatus(Bot bot)
        {
            if (bot == null)
            { 
                StatusLabel.Text = string.Empty;
                return;
            }
            if(bot.Status == StatusEnum.Farming)
            { 
                string text = $"Farming cards {bot.CurrentFarming.Count} games left";
                if (StatusLabel.Text == text)
                    return;
                StatusLabel.Text = text;
                return;
            }
            if (StatusLabel.Text == Bot.StatusString[(int)bot.Status])
                return;
            StatusLabel.Text = Bot.StatusString[(int)bot.Status];
        }
        internal void UpdateWallet(Bot bot)
        {
            if (bot == null || bot.Status == StatusEnum.Disabled || bot.Status == StatusEnum.Connecting || !bot.initialized)
            {
                labelWallet.Text = string.Empty;
                return;
            }
            string walletInfo;
            if (!bot.Wallet.HasWallet)
                walletInfo = "Wallet: dont have";
            else
                walletInfo = "Wallet: " + (float)bot.Wallet.Balance/100 + " " + bot.Wallet.Curency;
            if (labelWallet.Text != walletInfo)
                labelWallet.Text = walletInfo;
        }
        internal void CheckButtonsStatus(Bot bot)
        {
            if (bot == null)
            {
                buttonConnect.Visible = false;
                buttonFarm.Visible = false;
                return;
            }
            bool bReady = bot.Status != StatusEnum.Connecting && bot.initialized;

            buttonConnect.Visible = true;

            buttonConnect.Enabled = bReady;
            buttonFarm.Enabled = bot.Status != StatusEnum.RefreshGamesToFarm;

            if (bot.CurrentFarming != null && bot.CurrentFarming.Any())
                buttonFarm.Text = "Stop farm";
            else
                buttonFarm.Text = "Farm";

            if (bot.Status != StatusEnum.Disabled && bReady && bot.steamClient.IsConnected )
                buttonFarm.Visible = true;
            else
                buttonFarm.Visible = false;

            if (bot.isRunning && bot.steamClient.SteamID != null &&
                bot.steamClient.SteamID.AccountType == EAccountType.Individual)
            {
                buttonConnect.Text = "Disconnect";
            }
            else
            {
                buttonConnect.Text = "Connect";
            }
        }
        public void BotListAdd(string botName)
        {
            if (string.IsNullOrEmpty(botName))
                return;
            BotList.Items.Add(botName);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Bot bot;
            if (!Bot.Bots.TryGetValue(BotList.SelectedItem.ToString(),out bot))
            {
                return;
            }

            bot.PauseResumeFarm();
        }
        private void OnOffButton_Click(object sender, EventArgs e)
        {
            Bot bot;
            if (!Bot.Bots.TryGetValue(BotList.SelectedItem.ToString(), out bot))
                return;
            bot.PauseResume();
        }
        private void UpdateAll(Bot bot)
        {
            UpdateLogBox(bot);
            UpdateStatus(bot);
            UpdateWallet(bot);
            CheckButtonsStatus(bot);
        }

        private void closeToolStripMenuItemClose_Click(object sender, EventArgs e)
        {
            bWantClose = true;
            this.Close();
        }
        private void notifyIconMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.WindowState = _LastState;
            this.Activate();
            this.Show();
        }


    }

    class Listener : IDebugListener
    {
        internal static bool NetHookAlreadyInitialized { get; set; } = false;
        private string FilePath;
        internal Listener(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
            FilePath = filePath;
        }
        public void WriteLine(string category, string message)
        {
            Logging.DebugLogToFile(DateTime.Now + " [" + category + "]: " + message);
        }
    }
}