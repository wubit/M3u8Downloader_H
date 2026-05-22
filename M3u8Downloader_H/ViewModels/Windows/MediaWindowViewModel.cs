using System;
using System.Text.RegularExpressions;
using Caliburn.Micro;
using M3u8Downloader_H.Models;
using M3u8Downloader_H.Services;
using M3u8Downloader_H.Utils;
using MaterialDesignThemes.Wpf;
using M3u8Downloader_H.Abstractions.Common;
using M3u8Downloader_H.Common.DownloadPrams;
using M3u8Downloader_H.Exceptions;

namespace M3u8Downloader_H.ViewModels.Windows
{
    public class MediaWindowViewModel: Screen
    {
        private readonly SettingsService settingsService;
        private readonly ISnackbarMessageQueue notifications;

        public MediaDownloadInfo MediaDownloadInfo { get; } = new MediaDownloadInfo();
        public bool IsBusy { get; private set; }
        public Action<DownloadViewModel> EnqueueDownloadAction { get; set; } = default!;

        public MediaWindowViewModel(SettingsService settingsService, ISnackbarMessageQueue Notifications)
        {
            MediaDownloadInfo.NormalProcessDownloadAction = ProcessMediaDownload;
            MediaDownloadInfo.BatchProcessAction = HandleBatchLines;
            this.settingsService = settingsService;
            notifications = Notifications;
        }


        public bool CanProcessMediaDownload => !IsBusy;
        public void ProcessMediaDownload(MediaDownloadInfo mediaDownloadInfo)
        {
            IsBusy = true;
            try
            {
                mediaDownloadInfo.DoProcess(settingsService);

                mediaDownloadInfo.Reset(settingsService.IsResetAddress, settingsService.IsResetName);
            }
            catch (Exception e)
            {
                notifications.Enqueue(e.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 批量处理多行输入，每行一个下载任务
        /// 自动从每行提取URL（http/https开头），剩余部分作为视频名称
        /// </summary>
        private void HandleBatchLines(string videoUrl, SettingsService settings, int streamIndex)
        {
            string[] lines = videoUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                try
                {
                    var (url, name) = ExtractUrlAndName(trimmedLine);
                    if (url == null)
                    {
                        notifications.Enqueue($"未能从该行提取到有效地址: {trimmedLine}");
                        continue;
                    }

                    MediaDownloadParams mediaDownloadParams = new(
                        settings.SavePath,
                        new Uri(url),
                        null,
                        string.IsNullOrWhiteSpace(name) ? null : name,
                        settings.Headers
                    )
                    {
                        IsVideoStream = streamIndex == 0
                    };
                    ProcessMediaDownload(mediaDownloadParams);
                }
                catch (UriFormatException)
                {
                    notifications.Enqueue($"地址格式不正确: {trimmedLine}");
                    break;
                }
                catch (FileExistsException e)
                {
                    notifications.Enqueue(e);
                    break;
                }
            }
        }

        /// <summary>
        /// 从一行文本中提取URL和名称
        /// </summary>
        private static (string? url, string? name) ExtractUrlAndName(string line)
        {
            var match = Regex.Match(line, @"(https?://\S+)");
            if (!match.Success)
                return (null, null);

            string url = match.Groups[1].Value;
            string name = line.Replace(match.Value, "").Trim(' ', ',', '\t', '，', '|', '-');
            return (url, name);
        }

        public void ProcessMediaDownload(IMediaDownloadParam mediaDownloadParams)
        {
            FileEx.EnsureFileNotExist(mediaDownloadParams.VideoFullName);

            DownloadViewModel download = DownloadViewModel.CreateDownloadViewModel(mediaDownloadParams);
            if (download is null) return;

            EnqueueDownloadAction(download);
        }

    }
}
