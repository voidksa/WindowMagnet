using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Reflection;

namespace WindowMagnet.Helpers
{
    public class UpdateChecker
    {
        private const string RepoOwner = "voidksa";
        private const string RepoName = "WindowMagnet";

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                // Format to x.y.z to match GitHub tags usually
                string currentVersionStr = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WindowMagnet", currentVersionStr));
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var tagName = doc.RootElement.GetProperty("tag_name").GetString();
                            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString();

                            if (IsNewerVersion(tagName, currentVersion))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var result = MessageBox.Show(
                                        $"A new version ({tagName}) of WindowMagnet is available!\n\nWould you like to download it now?",
                                        "Update Available",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Information);

                                    if (result == MessageBoxResult.Yes)
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo(htmlUrl) { UseShellExecute = true });
                                        }
                                        catch { }
                                    }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private static bool IsNewerVersion(string latestTag, Version currentVersion)
        {
            if (string.IsNullOrEmpty(latestTag)) return false;

            // Remove 'v' prefix if present
            latestTag = latestTag.TrimStart('v');

            if (Version.TryParse(latestTag, out Version vLatest))
            {
                // Compare only Major.Minor.Build
                if (vLatest.Major > currentVersion.Major) return true;
                if (vLatest.Major == currentVersion.Major && vLatest.Minor > currentVersion.Minor) return true;
                if (vLatest.Major == currentVersion.Major && vLatest.Minor == currentVersion.Minor && vLatest.Build > currentVersion.Build) return true;
            }
            return false;
        }
    }
}