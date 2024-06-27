using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System;
using System.Linq;
using System.IO;
using System.Text.Json.Nodes;
using Windows.UI;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PowerToys_Run_Plugin_Updater
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    public sealed partial class MainWindow : Window
    {

        public MainWindow()
        {
            this.InitializeComponent();

            SystemBackdrop = new MicaBackdrop()
            { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
            // populate the changelog
            PopulateChangelog();
            ExtendsContentIntoTitleBar = true;

            Window window = this;
            window.ExtendsContentIntoTitleBar = true;  // enable custom titlebar
            window.SetTitleBar(AppTitleBar);

            // Set the window icon using the bundled icon in the exe file because the app is published as a single file
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Logo.ico");

            window.SetIcon(iconPath);

            // Set the window always on top using WinUIEx library
            this.SetIsAlwaysOnTop(true);
            this.SetWindowSize(700, 600);
            this.CenterOnScreen();
            this.SetIsResizable(false);
            this.SetIsMaximizable(false);
            this.SetIsMinimizable(false);
            this.SetIsShownInSwitchers(true);

            // Set the Don't ask again checkbox value to the value in the settings.json file
            string settingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "PowerToys Run");
            string settingPath = Path.Combine(settingDirectory, "settings.json");

            // Read the settings.json file
            string settingsJson = File.ReadAllText(settingPath);
            JsonNode settings = JsonNode.Parse(settingsJson);
            var plugins = settings["plugins"].AsArray();
            foreach (var plugin in plugins)
            {
                if (plugin["Id"].ToString() == "64861420-a0ca-442d-ae1c-35054e15a4b7")
                {
                    System.Diagnostics.Debug.WriteLine(plugin["Id"].ToString());
                    var additionalOptions = plugin["AdditionalOptions"].AsArray();
                    foreach (var additionalOption in additionalOptions)
                    {
                        if (additionalOption["Key"].ToString() == "UpdatePluginSetting")
                        {
                            bool isChecked = additionalOption["Value"].ToString().ToLower() == "true";
                            //If the checkbox is checked, the don't ask again checkbox should'nt be checked
                            isChecked = !isChecked;
                            // write debug log
                            System.Diagnostics.Debug.WriteLine(isChecked);
                            DontAskAgain.IsChecked = isChecked;
                            break;
                        }
                    }
                    break;
                }
            }

        }

        // Cancel button click event
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        // Don't ask again checkbox click event
        private void DontAskAgain_Click(object sender, RoutedEventArgs e)
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("PowerToys"))
            {
                process.Kill();
            }

            // Checkbox value
            bool isChecked = DontAskAgain.IsChecked.Value;


            string settingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "PowerToys Run");
            string settingPath = Path.Combine(settingDirectory, "settings.json");

            // Change This value in the settings.json (should select the right Id in the plugins list and the right option in the AdditionalOptions list) {"plugins":[{"Id":"64861420-a0ca-442d-ae1c-35054e15a4b7","AdditionalOptions":[{"Key":"Updates","Value":"true"}]}]}
            // Edit the file
            string settingsJson = File.ReadAllText(settingPath);
            JsonNode settings = JsonNode.Parse(settingsJson);

            // Find the plugin with the specified Id and update the "Updates" option
            var plugins = settings["plugins"].AsArray();
            foreach (var plugin in plugins)
            {
                if (plugin["Id"].ToString() == "64861420-a0ca-442d-ae1c-35054e15a4b7")
                {
                    var additionalOptions = plugin["AdditionalOptions"].AsArray();
                    foreach (var additionalOption in additionalOptions)
                    {
                        if (additionalOption["Key"].ToString() == "UpdatePluginSetting")
                        {
                            isChecked = !isChecked;
                            additionalOption["Value"] = isChecked;
                            break;
                        }
                    }
                    break;
                }
            }

            // Save the updated settings back to the file
            System.Diagnostics.Debug.WriteLine(settings.ToJsonString());
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingPath, settings.ToJsonString(options));
            System.Diagnostics.Process.Start("C:\\Program Files\\PowerToys\\PowerToys.exe");
        }


        // Update button click event
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Collapsed;
            Progressbar.Visibility = Visibility.Visible;
            LogText.Visibility = Visibility.Visible;
            //Check the architecture of the system (x64 or arm64)
            string architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (architecture.Equals("AMD64", StringComparison.OrdinalIgnoreCase))
            {
                architecture = "x64";
            }
            else if (architecture.Equals("ARM64", StringComparison.OrdinalIgnoreCase))
            {
                architecture = "arm64";
            }
            LogText.Text = "Detecting system architecture...";
            string downloadUrl = string.Empty;

            ///Make a call to the github api to get the latest release
            ///https://api.github.com/repos/Fefedu973/PowerToys-Run-Google-Search-Suggestions-Plugin/releases/latest
            ///

            // Let's use system proxy
            using var proxyClientHandler = new HttpClientHandler
            {
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                Proxy = WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true,
            };

            using var getReleaseInfoClient = new HttpClient(proxyClientHandler);

            // GitHub APIs require sending an user agent
            // https://docs.github.com/rest/overview/resources-in-the-rest-api#user-agent-required
            getReleaseInfoClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");
            string releaseInfo = await getReleaseInfoClient.GetStringAsync("https://api.github.com/repos/Fefedu973/PowerToys-Run-Google-Search-Suggestions-Plugin/releases/latest");

            System.Diagnostics.Debug.WriteLine(architecture);

            var assets = JsonSerializer.Deserialize<JsonElement>(releaseInfo).GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString().Contains(architecture))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }

            }

            LogText.Text = "Downloading plugin from " + downloadUrl + "...";

            if (string.IsNullOrEmpty(downloadUrl))
            {
                // No download url found for the current architecture
                // Show an error message
                Progressbar.ShowError = true;
            }

            try
            {
                using var downloadClient = new HttpClient(proxyClientHandler);
                var response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    Progressbar.IsIndeterminate = !canReportProgress;
                    Progressbar.Value = 0;

                    string pluginDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "PowerToys Run", "Plugins");
                    string pluginPath = System.IO.Path.Combine(pluginDirectory, "PowerToys-Run-Google-Search-Suggestions-Plugin.zip");

                    using var downloadStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(pluginPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            var progressValue = ((double)totalRead / totalBytes) * 100;
                            var progressValueInt = (int)progressValue;
                            Progressbar.Value = progressValueInt;
                            LogText.Text = "Downloading... " + progressValueInt + "%";
                            System.Diagnostics.Debug.WriteLine(((double)totalRead / totalBytes) * 100);
                        }
                    }
                }
                else
                {
                    // Handle the error
                    Progressbar.ShowError = true;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception
                Progressbar.ShowError = true;
            }

            //Stop powertoys process
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("PowerToys"))
            {
                process.Kill();
                process.WaitForExit();
                //Wait 1 second to make sure the process is killed
                System.Threading.Thread.Sleep(1000);
            }

            LogText.Text = "Plugin downloaded successfully!";

            // Delete a file in the plugin directory
            string pluginDirectory2 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "PowerToys Run", "Plugins");
            string pluginPath2 = System.IO.Path.Combine(pluginDirectory2, "GoogleSearchSuggestions");
            if (System.IO.Directory.Exists(pluginPath2))
            {
                LogText.Text = "Stopping PowerToys process...";
                System.IO.Directory.Delete(pluginPath2, true);


            }

            LogText.Text = "Deleting old plugin...";

            string pluginDirectory3 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "PowerToys Run", "Plugins");
            string pluginPath3 = System.IO.Path.Combine(pluginDirectory3, "PowerToys-Run-Google-Search-Suggestions-Plugin.zip");

            LogText.Text = "Extracting plugin...";

            // Extract the downloaded file to the plugin directory here
            System.IO.Compression.ZipFile.ExtractToDirectory(pluginPath3, pluginDirectory3);

            LogText.Text = "Restarting PowerToys process...";

            // Restart the PowerToys process
            System.Diagnostics.Process.Start("C:\\Program Files\\PowerToys\\PowerToys.exe");

            // Delete the downloaded file


            if (System.IO.File.Exists(pluginPath3))
            {
                System.IO.File.Delete(pluginPath3);
                LogText.Text = "Cleaning up...";
            }
            LogText.Text = "Plugin updated successfully!";

            // I defined a color brush in Progrssbar.Resources in the xaml file < ProgressBar.Resources >< SolidColorBrush x: Key = "green" Color = "#58d68e" /></ ProgressBar.Resources >
            var bgcolor = Color.FromArgb(0xFF, 0x58, 0xd6, 0x8e); //
            Progressbar.Foreground = new SolidColorBrush(bgcolor);
            SuccesButton.Visibility = Visibility.Visible;

        }

        private async void PopulateChangelog()
        {
            string releaseNotesMarkdown = await GetReleaseNotesMarkdown();
            ReleaseNotesMarkdown.Text = releaseNotesMarkdown;
            Ring.Visibility = Visibility.Collapsed;
            ReleaseNotesMarkdown.Visibility = Visibility.Visible;
        }

        private sealed class PluginReleaseInfo
        {
            [JsonPropertyName("published_at")]
            public DateTimeOffset PublishedDate { get; set; }

            [JsonPropertyName("body")]
            public string ReleaseNotes { get; set; }
        }

        // Get changelog from github https://api.github.com/repos/Fefedu973/PowerToys-Run-Google-Search-Suggestions-Plugin/releases/latest
        private static async Task<string> GetReleaseNotesMarkdown()
        {
            string releaseNotesJSON = string.Empty;

            // Let's use system proxy
            using var proxyClientHandler = new HttpClientHandler
            {
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                Proxy = WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true,
            };

            using var getReleaseInfoClient = new HttpClient(proxyClientHandler);

            // GitHub APIs require sending an user agent
            // https://docs.github.com/rest/overview/resources-in-the-rest-api#user-agent-required
            getReleaseInfoClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");
            releaseNotesJSON = await getReleaseInfoClient.GetStringAsync("https://api.github.com/repos/Fefedu973/PowerToys-Run-Google-Search-Suggestions-Plugin/releases");
            IList<PluginReleaseInfo> releases = JsonSerializer.Deserialize<IList<PluginReleaseInfo>>(releaseNotesJSON);

            // Get the latest releases
            var latestReleases = releases.OrderByDescending(release => release.PublishedDate).Take(5);

            StringBuilder releaseNotesHtmlBuilder = new StringBuilder(string.Empty);

            foreach (var release in latestReleases)
            {
                releaseNotesHtmlBuilder.AppendLine(release.ReleaseNotes);
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
            }

            //Artificial wait load
            //await Task.Delay(1000);

            return releaseNotesHtmlBuilder.ToString();
        }

        private void SuccesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            //End the process
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
    }
}