using System;
using Caliburn.Micro;
using M3u8Downloader_H.Abstractions.Common;
using M3u8Downloader_H.Extensions;
using M3u8Downloader_H.Models;
using M3u8Downloader_H.Services;
using M3u8Downloader_H.Utils;
using MaterialDesignThemes.Wpf;
using M3u8Downloader_H.Exceptions;
using System.IO;
using M3u8Downloader_H.Abstractions.M3u8;
using M3u8Downloader_H.Common.DownloadPrams;

namespace M3u8Downloader_H.ViewModels.Windows
{
    public class M3u8WindowViewModel  : Screen
    {
        private readonly SettingsService settingsService;
        private readonly PluginService pluginService;
        private readonly ISnackbarMessageQueue notifications;

        public M3u8DownloadInfo VideoDownloadInfo { get; } = new M3u8DownloadInfo();
        public bool IsBusy { get; private set; }

        public Action<DownloadViewModel> EnqueueDownloadAction { get; set; } = default!;

        public M3u8WindowViewModel(SettingsService settingsService, PluginService pluginService, ISnackbarMessageQueue Notifications)
        {
            VideoDownloadInfo.HandleTextAction = HandleTxt;
            VideoDownloadInfo.NormalProcessDownloadAction = ProcessM3u8Download;
            VideoDownloadInfo.BatchProcessAction = HandleBatchLines;
            this.settingsService = settingsService;
            this.pluginService = pluginService;
            notifications = Notifications;
        }



        public bool CanProcessM3u8Download => !IsBusy;
        public void ProcessM3u8Download(M3u8DownloadInfo obj)
        {
            IsBusy = true;
            try
            {
                obj.DoProcess(settingsService);

                //只有操作成功才会清空
                obj.Reset(settingsService.IsResetAddress, settingsService.IsResetName);
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
        /// 格式: 地址----名称 (名称可选)
        /// </summary>
        private void HandleBatchLines(string requestUrl, SettingsService settings)
        {
            string[] lines = requestUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                string[] result = trimmedLine.Split(settings.Separator, 2);
                try
                {
                    M3u8DownloadParams m3U8DownloadParams = new(
                        new Uri(result[0].Trim(), UriKind.Absolute),
                        result.Length > 1 ? result[1].Trim() : null,
                        settings.SavePath,
                        settings.SelectedFormat,
                        settings.Headers,
                        VideoDownloadInfo.Method,
                        VideoDownloadInfo.Key,
                        VideoDownloadInfo.Iv
                    );
                    ProcessM3u8Download(m3U8DownloadParams);
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


        private void HandleTxt(Uri uri)
        {
            foreach (var item in File.ReadLines(uri.OriginalString))
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                string[] result = item.Trim().Split(settingsService.Separator, 2);
                try
                {
                    M3u8DownloadParams m3U8DownloadParams = new(new Uri(result[0], UriKind.Absolute), result.Length > 1 ? result[1] : null, settingsService.SavePath, settingsService.SelectedFormat, settingsService.Headers);
                    ProcessM3u8Download(m3U8DownloadParams);
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

        //处理软件界面来的请求
        public void ProcessM3u8Download(IM3u8DownloadParam m3U8DownloadParam, string? pluginKey = default!)
        {
            FileEx.EnsureFileNotExist(m3U8DownloadParam.VideoFullName);

            string tmpPluginKey = pluginKey is not null
                                ? pluginKey
                                : string.IsNullOrWhiteSpace(settingsService.PluginKey)
                                ? m3U8DownloadParam.RequestUrl.GetHostName()
                                : settingsService.PluginKey;
            DownloadViewModel download = DownloadViewModel.CreateDownloadViewModel(m3U8DownloadParam, pluginService[tmpPluginKey]);
            if (download is null) return;

            EnqueueDownloadAction(download);
        }

        //处理接口过来的请求
        public void ProcessM3u8Download(IDownloadParamBase m3U8DownloadParam,IM3uFileInfo m3UFileInfo,  string? pluginKey = default!)
        {
            FileEx.EnsureFileNotExist(m3U8DownloadParam.VideoFullName);

            //这里因为不可能有url所以直接通过设置来判别使用某个插件
            DownloadViewModel download = DownloadViewModel.CreateDownloadViewModel(m3UFileInfo, m3U8DownloadParam, pluginService[pluginKey ?? settingsService.PluginKey]);
            if (download is null) return;

            EnqueueDownloadAction(download);
        }

    }
}
