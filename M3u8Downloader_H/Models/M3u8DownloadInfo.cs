using System;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using M3u8Downloader_H.Common.DownloadPrams;
using M3u8Downloader_H.Services;


namespace M3u8Downloader_H.Models
{
    public class M3u8DownloadInfo : PropertyChangedBase
    {
        private static readonly string[] extensionArr = ["m3u8", "json", "txt"];

        public string RequestUrl { get; set; } = default!;
        public string VideoName { get; set; } = default!;

        public string Method { get; set; } = default!;
        public string? Key { get; set; } = default!;
        public string? Iv { get; set; } = default!;

        public Action<Uri> HandleTextAction { get; set; } = default!;
        public Action<M3u8DownloadParams, string?> NormalProcessDownloadAction { get; set; } = default!;
        public Action<string, SettingsService> BatchProcessAction { get; set; } = default!;


        public void Reset(bool resetUrl,bool resetName)
        {
            if(resetUrl) RequestUrl = string.Empty;
            if(resetName) VideoName = string.Empty;
            Key = null;
            Method = "AES-128";
            Iv = null;
        }


        public void DoProcess(SettingsService settingsService)
        {
            if(string.IsNullOrWhiteSpace(RequestUrl))
                throw new InvalidOperationException("下载地址不能为空");

            // 检查是否包含多行（批量模式）
            string[] lines = RequestUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
            {
                // 多行批量模式：每行一个任务
                BatchProcessAction(RequestUrl, settingsService);
                return;
            }

            // 单行模式：保持原有逻辑
            string singleUrl = lines[0].Trim();
            Uri uri = new(singleUrl, UriKind.Absolute);
            if (!uri.IsFile)
            {
                M3u8DownloadParams m3U8DownloadParams = new(new Uri(singleUrl), VideoName, settingsService.SavePath, settingsService.SelectedFormat, settingsService.Headers, Method, Key, Iv);
                NormalProcessDownloadAction(m3U8DownloadParams, null);
                return;
            }


            string ext = Path.GetExtension(singleUrl).Trim('.');
            string extension = extensionArr.Where(e => e == ext).FirstOrDefault() ?? throw new InvalidOperationException("请确认是否为.m3u8或.txt或.json");
            if (extension == "txt")
            {
                HandleTextAction(uri);
                return;
            }
            else
            {
                M3u8DownloadParams m3U8DownloadParams = new(new Uri(singleUrl), VideoName, settingsService.SavePath, settingsService.SelectedFormat, settingsService.Headers, Method, Key, Iv);
                NormalProcessDownloadAction(m3U8DownloadParams, null);
                return;
            }
        }

    }
}
