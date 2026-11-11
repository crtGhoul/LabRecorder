using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LabRecorder
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string ExeDownloadUrl { get; set; } = string.Empty;
        public long ExeSizeBytes { get; set; }
    }

    public static class UpdateService
    {
        public const string CurrentVersion = "v1.3.0";
        private const string RepoOwner = "crtGhoul";
        private const string RepoName = "LabRecorder";
        private const string ReleasesApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        public static void CleanOldExecutables()
        {
            try
            {
                string? currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe)) return;
                string dir = Path.GetDirectoryName(currentExe) ?? "";
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                foreach (var oldExe in Directory.GetFiles(dir, "*.old.exe"))
                {
                    try { File.Delete(oldExe); } catch {}
                }
            }
            catch {}
        }

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var info = new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = CurrentVersion,
                HasUpdate = false
            };

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LabRecorder-App");
                client.Timeout = TimeSpan.FromSeconds(10);

                string json = await client.GetStringAsync(ReleasesApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("tag_name", out var tagElem))
                {
                    info.LatestVersion = tagElem.GetString() ?? CurrentVersion;
                }

                if (root.TryGetProperty("html_url", out var htmlElem))
                {
                    info.HtmlUrl = htmlElem.GetString() ?? $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";
                }

                if (root.TryGetProperty("body", out var bodyElem))
                {
                    info.ReleaseNotes = bodyElem.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var nameElem) &&
                            string.Equals(nameElem.GetString(), "LabRecorder.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var dlElem))
                            {
                                info.ExeDownloadUrl = dlElem.GetString() ?? string.Empty;
                            }
                            if (asset.TryGetProperty("size", out var sizeElem))
                            {
                                info.ExeSizeBytes = sizeElem.GetInt64();
                            }
                            break;
                        }
                    }
                }

                info.HasUpdate = IsNewer(info.LatestVersion, CurrentVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CheckForUpdateAsync exception: " + ex.Message);
            }

            return info;
        }

        public static bool IsNewer(string latestTag, string currentTag)
        {
            try
            {
                var vLatest = ParseVersion(latestTag);
                var vCurrent = ParseVersion(currentTag);
                return vLatest > vCurrent;
            }
            catch
            {
                return !string.Equals(latestTag.Trim(), currentTag.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static Version ParseVersion(string tag)
        {
            string clean = tag.Trim().TrimStart('v', 'V');
            int dashIdx = clean.IndexOf('-');
            if (dashIdx >= 0) clean = clean.Substring(0, dashIdx);

            string[] parts = clean.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int mj) ? mj : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int mn) ? mn : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int bd) ? bd : 0;

            return new Version(major, minor, build);
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                string? currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                {
                    return false;
                }

                string tempNewExe = $"{currentExe}.new.exe";
                string oldExe = $"{currentExe}.old.exe";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LabRecorder-App");
                client.Timeout = TimeSpan.FromMinutes(3);

                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempNewExe, FileMode.Create, FileAccess.Write, FileShare.None);

                    byte[] buffer = new byte[65536];
                    long readTotal = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        readTotal += bytesRead;
                        if (totalBytes.HasValue && totalBytes.Value > 0 && progress != null)
                        {
                            int pct = (int)((readTotal * 100) / totalBytes.Value);
                            progress.Report(pct);
                        }
                    }
                }

                if (!File.Exists(tempNewExe) || new FileInfo(tempNewExe).Length < 10000000)
                {
                    try { File.Delete(tempNewExe); } catch {}
                    return false;
                }

                // In-place atomic binary swap
                if (File.Exists(oldExe)) File.Delete(oldExe);
                File.Move(currentExe, oldExe);
                File.Move(tempNewExe, currentExe);

                // Spawn updated executable
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExe,
                    UseShellExecute = true
                });

                // Exit current process
                Environment.Exit(0);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DownloadAndApplyUpdateAsync failed: " + ex.Message);
                return false;
            }
        }
    }
}
