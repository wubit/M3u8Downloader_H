using System;
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
        /// 格式: 视频地址----名称 (名称可选)
        /// </summary>
        private void HandleBatchLines(string videoUrl, SettingsService settings, int streamIndex)
        {
            string[] lines = videoUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                string[] result = trimmedLine.Split(settings.Separator, 2);
                try
                {
                    string url = result[0].Trim();
                    string? name = result.Length > 1 ? result[1].Trim() : null;

                    MediaDownloadParams mediaDownloadParams = new(
                        settings.SavePath,
                        new Uri(url),
                        null,
                        name,
                        settings.Headers
                    )
                    {
                        IsVideoStream = streamIndex == 0
                    };
                    ProcessMediaDownload(mediaDownloadParams);
                }
                catch (UriFormatException)
                {
                    notifications.Enqueue($"{result[0]} 不是正确的地址");
                    break;
                }
                catch (FileExistsException e)
                {
                    notifications.Enqueue(e);
                    break;
                }
            }
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
