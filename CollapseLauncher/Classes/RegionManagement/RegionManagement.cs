﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

using Hi3Helper.Preset;
using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.GameSettingsManagement;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        HttpClientTool httpClient;
        HttpClientTool httpClient4Img;
        CancellationTokenSource tokenSource;
        bool loadRegionComplete;
        Task loader = new Task(() => { });
        public async Task LoadRegion(int regionIndex = 0)
        {
            loadRegionComplete = false;
            CurrentRegion = ConfigStore.Config[regionIndex];
            previousTag = "launcher";
            LoadGameRegionFile();
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName}...");
            DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = "" );
            await HideLoadingPopup(false, "Loading Region", CurrentRegion.ZoneName);
            while (!await TryGetRegionResource())
            {
                DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = $"Loading Region {CurrentRegion.ZoneName} has timed-out (> 10 seconds). Retrying...");
                LogWriteLine($"Loading Region {CurrentRegion.ZoneName} has timed-out (> 10 seconds). Retrying...", Hi3Helper.LogType.Warning);
            }
            /*
            loader = Task.Run(() =>
            {
                ChangeBackgroundImageAsRegion(tokenSource.Token);
                FetchLauncherResourceAsRegion(tokenSource.Token);
            });
            TimeOutWatcher();
            await loader;
            */
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName} Done!");
            InitRegKey();
            await HideLoadingPopup(true, "Loading Region", CurrentRegion.ZoneName);
        }

        private async Task<bool> TryGetRegionResource()
        {
            try
            {
                await Task.Run(() =>
                {
                    tokenSource = new CancellationTokenSource();
                    RetryWatcher();

                    ChangeBackgroundImageAsRegion(tokenSource.Token);
                    FetchLauncherResourceAsRegion(tokenSource.Token);

                    tokenSource.Cancel();

                    loadRegionComplete = true;
                });
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return true;
        }

        private async void RetryWatcher()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(5000, tokenSource.Token);
                    DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = "It takes a bit longer than expected. Please ensure that your connection is stable.");
                    if (!loadRegionComplete)
                    {
                        await Task.Delay(5000, tokenSource.Token);
                        tokenSource.Cancel();
                    }
                    else
                        return;
                }
                catch
                {
                    return;
                }
            }
        }

        private async void TimeOutWatcher()
        {
            bool isLoading = true;
            while (isLoading)
            {
                if (loader.IsCompleted || loader.IsCompletedSuccessfully)
                {
                    LogWriteLine("Timeout completed!");
                    isLoading = false;
                }
                else
                {
                    await Task.Delay(5000);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!(loader.IsCompleted || loader.IsCompletedSuccessfully))
                        {
                            LoadingFooter.Text = "It takes a bit longer than expected (> 5 seconds). Please ensure that your connection is stable.";
                        }
                        else
                        {
                            LoadingFooter.Text = "";
                        }
                    });
                }
            }
        }

        private void LoadGameRegionFile()
        {
            gameIni = new GameIniStruct();

            gamePath = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
            DispatcherQueue.TryEnqueue(() => ComboBoxGameRegion.PlaceholderText = CurrentRegion.ZoneName);
            gameIni.ProfilePath = Path.Combine(gamePath, $"config.ini");

            if (!Directory.Exists(gamePath))
                Directory.CreateDirectory(gamePath);

            if (!File.Exists(gameIni.ProfilePath))
                PrepareInstallation();

            gameIni.Profile = new IniFile();
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Open, FileAccess.ReadWrite);
            gameIni.Profile.Load(gameIni.ProfileStream);

            // CurrentRegion.CheckExistingGame();
        }

        public async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            ChangeRegionBtn.IsEnabled = false;
            ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            appIni.Profile["app"]["CurrentRegion"] = ComboBoxGameRegion.SelectedIndex;
            SaveAppConfig();
            MainFrameChanger.ChangeMainFrame(typeof(Pages.BlankPage));
            await InvokeLoadRegion(ComboBoxGameRegion.SelectedIndex);
            if (ChangeRegionConfirmBtn.Flyout is Flyout f)
            {
                MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));
                // LauncherFrame.Navigate(typeof(Pages.HomePage), null, new DrillInNavigationTransitionInfo());
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];

                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                // Thread.Sleep(1000);
                f.Hide();
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionBtn.IsEnabled = true;
                LogWriteLine($"Region changed to {ComboBoxGameRegion.SelectedValue}", Hi3Helper.LogType.Scheme);
            }
        }

        private async Task InvokeLoadRegion(int index) => await LoadRegion(index);
    }
}