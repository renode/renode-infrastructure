//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Net;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Migrant;
using System.IO.Compression;
using System.Threading;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Globalization;
using System.ComponentModel;
using Antmicro.Renode.Core;
using System.Text;
using Antmicro.Migrant.Customization;

namespace Antmicro.Renode.Utilities
{
    public class CachingFileFetcher
    {
        public CachingFileFetcher()
        {
            fetchedFiles = new Dictionary<string, Uri>();
            progressUpdateThreshold = TimeSpan.FromSeconds(0.25);
        }

        public IDictionary<string, Uri> GetFetchedFiles()
        {
            return fetchedFiles.ToDictionary(x => x.Key, x => x.Value);
        }

        public string FetchFromUri(Uri uri)
        {
            string fileName;
            if(!TryFetchFromUri(uri, out fileName))
            {
                throw new RecoverableException("Could not download file from {0}.".FormatWith(uri));
            }
            return fileName;
        }

        public void CancelDownload()
        {
            if(client != null && client.IsBusy)
            {
                client.CancelAsync();
            }
        }

        public bool TryFetchFromUri(Uri uri, out string fileName)
        {
            fileName = null;
            if(!Monitor.TryEnter(concurrentLock))
            {
                Logger.LogAs(this, LogLevel.Error, "Cannot perform concurrent downloads, aborting...");
                return false;
            }
#if PLATFORM_WINDOWS
            using(var locker = new WindowsFileLocker(GetCacheIndexLockLocation()))
#else
            using(var locker = new PosixFileLocker(GetCacheIndexLockLocation()))
#endif
            {
                if(TryGetFromCache(uri, out fileName))
                {
                    fetchedFiles.Add(fileName, uri);
                    Monitor.Exit(concurrentLock);
                    return true;
                }
                using(client = new WebClient())
                {
                    fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                    try
                    {
                        var attempts = 0;
                        var success = false;
                        do
                        {
                            var downloadProgressHandler = EmulationManager.Instance.ProgressMonitor.Start(GenerateProgressMessage(uri), false, true);
                            Logger.LogAs(this, LogLevel.Info, "Downloading {0}.", uri);
                            var now = CustomDateTime.Now;
                            var bytesDownloaded = 0L;
                            client.DownloadProgressChanged += (sender, e) =>
                            {
                                var newNow = CustomDateTime.Now;

                                var period = newNow - now;
                                if (period > progressUpdateThreshold)
                                {
                                    downloadProgressHandler.UpdateProgress(e.ProgressPercentage,
                                        GenerateProgressMessage(uri,
                                            e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage, 1.0 * (e.BytesReceived - bytesDownloaded) / period.TotalSeconds));

                                    now = newNow;
                                    bytesDownloaded = e.BytesReceived;
                                }
                            };
                            var wasCancelled = false;
                            Exception exception = null;
                            var resetEvent = new ManualResetEvent(false);
                            client.DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e)
                            {
                                exception = e.Error;
                                if (e.Cancelled)
                                {
                                    wasCancelled = true;
                                }
                                resetEvent.Set();
                                downloadProgressHandler.Finish();
                            };
                            client.DownloadFileAsync(uri, fileName);
                            resetEvent.WaitOne();
                            downloadProgressHandler.Finish();

                            if (wasCancelled)
                            {
                                Logger.LogAs(this, LogLevel.Info, "Download cancelled.");
                                File.Delete(fileName);
                                return false;
                            }
                            if (exception != null)
                            {
                                var webException = exception as WebException;
                                File.Delete(fileName);
                                Logger.LogAs(this, LogLevel.Error, "Failed to download from {0}, reason: {1}.", uri, webException != null ? ResolveWebException(webException) : exception.Message);
                                return false;
                            }
                            Logger.LogAs(this, LogLevel.Info, "Download done.");
                            if (uri.ToString().EndsWith(".gz", StringComparison.InvariantCulture))
                            {
                                var decompressionProgressHandler = EmulationManager.Instance.ProgressMonitor.Start(string.Format("Decompressing {0}", uri), false, false);
                                Logger.LogAs(this, LogLevel.Info, "Decompressing {0}.", uri);
                                var decompressedFile = TemporaryFilesManager.Instance.GetTemporaryFile();
                                using (var gzipStream = new GZipStream(File.OpenRead(fileName), CompressionMode.Decompress))
                                using (var outputStream = File.OpenWrite(decompressedFile))
                                {
                                    gzipStream.CopyTo(outputStream);
                                }
                                fileName = decompressedFile;
                                Logger.LogAs(this, LogLevel.Info, "Decompression done");
                                decompressionProgressHandler.Finish();
                            }
                        }
                        while (!(success = UpdateInCache(uri, fileName)) && attempts++ < 2);
                        if (!success)
                        {
                            Logger.LogAs(this, LogLevel.Error, "Download failed {0} times, wrong checksum or size, aborting.", attempts);
                            File.Delete(fileName);
                            return false;
                        }
                        fetchedFiles.Add(fileName, uri);
                        return true;
                    }
                    finally
                    {
                        Monitor.Exit(concurrentLock);
                    }
                }
            }
        }

        private static string ResolveWebException(WebException e)
        {
            string reason;
            switch(e.Status)
            {
            case WebExceptionStatus.ConnectFailure:
                reason = "unable to connect to the server";
                break;

            case WebExceptionStatus.ConnectionClosed:
                reason = "the connection was prematurely closed";
                break;

            case WebExceptionStatus.NameResolutionFailure:
                reason = "server name resolution error";
                break;

            case WebExceptionStatus.ProtocolError:
                switch(((HttpWebResponse)e.Response).StatusCode)
                {
                case HttpStatusCode.NotFound:
                    reason = "file was not found on a server";
                    break;

                default:
                    reason = string.Format("http protocol status code {0}", (int)((HttpWebResponse)e.Response).StatusCode);
                    break;
                }
                break;

            default:
                reason = e.Status.ToString();
                break;
            }

            return reason;
        }

        private string GenerateProgressMessage(Uri uri, long? bytesDownloaded = null, long? totalBytes = null, int? progressPercentage = null, double? speed = null)
        {
            var strBldr = new StringBuilder();
            strBldr.AppendFormat("Downloading: {0}", uri);
            if(bytesDownloaded.HasValue && totalBytes.HasValue)
            {
                // A workaround for a bug in Mono misreporting TotalBytesToReceive
                if(totalBytes == -1)
                {
                    strBldr.AppendFormat("\nProgress: {0}B downloaded", Misc.NormalizeBinary(bytesDownloaded.Value));
                }
                else
                {
                    strBldr.AppendFormat("\nProgress: {0}% ({1}B/{2}B)", progressPercentage, Misc.NormalizeBinary(bytesDownloaded.Value), Misc.NormalizeBinary(totalBytes.Value));
                }
            }
            if(speed != null)
            {
                double val;
                string unit;

                Misc.CalculateUnitSuffix(speed.Value, out val, out unit);
                strBldr.AppendFormat("\nSpeed: {0:F2}{1}/s", val, unit);
            }
            return strBldr.Append(".").ToString();
        }

        private bool TryGetFromCache(Uri uri, out string fileName)
        {
            lock(CacheDirectory)
            {
                fileName = null;
                var index = ReadBinariesIndex();
                BinaryEntry entry;
                if(!index.TryGetValue(uri, out entry))
                {
                    return false;
                }
                var fileToCopy = GetBinaryFileName(entry.Index);
                if(!Verify(fileToCopy, entry))
                {
                    return false;
                }
                fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(GetBinaryFileName(entry.Index), fileName, true);
                return true;
            }
        }

        private bool Verify(string fileName, BinaryEntry entry)
        {
            if(!File.Exists(fileName))
            {
                Logger.LogAs(this, LogLevel.Warning, "Binary {0} found in index but is missing in cache.", fileName);
                return false;
            }

            if(entry.Checksum != null)
            {
                var actualSize = new FileInfo(fileName).Length;
                if(actualSize != entry.Size)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Size of the file differs: is {0}B, should be {1}B.", actualSize, entry.Size);
                    return false;
                }

                if(ConfigurationManager.Instance.Get("file-fetcher", "calculate-checksum", true))
                {
                    byte[] checksum;
                    using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Calculating SHA1 checksum..."))
                    {
                        checksum = GetSHA1Checksum(fileName);
                    }
                    if(!checksum.SequenceEqual(entry.Checksum))
                    {
                        Logger.LogAs(this, LogLevel.Warning, "Checksum of the file differs, is {0}, should be {1}.", ChecksumToText(checksum), ChecksumToText(entry.Checksum));
                        return false;
                    }
                }
            }

            return true;
        }

        private bool UpdateInCache(Uri uri, string withFile)
        {
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Updating cache"))
            {
                lock(CacheDirectory)
                {
                    var index = ReadBinariesIndex();
                    BinaryEntry entry;
                    var fileId = 0;
                    if(!index.TryGetValue(uri, out entry))
                    {
                        foreach(var element in index)
                        {
                            fileId = Math.Max(fileId, element.Value.Index) + 1;
                        }
                    }
                    else
                    {
                        fileId = entry.Index;
                    }
                    FileCopier.Copy(withFile, GetBinaryFileName(fileId), true);
                    long size;
                    var checksum = GetChecksumAndSizeFromUri(uri, out size);
                    entry = new BinaryEntry(fileId, size, checksum);
                    if(!Verify(withFile, entry))
                    {
                        return false;
                    }
                    index[uri] = entry;
                    WriteBinariesIndex(index);
                    return true;
                }
            }
        }

        private Dictionary<Uri, BinaryEntry> ReadBinariesIndex()
        {
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Reading cache"))
            {
                using(var fStream = GetIndexFileStream())
                {
                    if(fStream.Length == 0)
                    {
                        return new Dictionary<Uri, BinaryEntry>();
                    }
                    Dictionary<Uri, BinaryEntry> result;
                    if(Serializer.TryDeserialize<Dictionary<Uri, BinaryEntry>>(fStream, out result) != DeserializationResult.OK)
                    {
                        Logger.LogAs(this, LogLevel.Warning, "There was an error while loading index file. Cache will be rebuilt.");
                        fStream.Close();
                        ResetIndex();
                        return new Dictionary<Uri, BinaryEntry>();
                    }
                    return result;
                }
            }
        }

        private void WriteBinariesIndex(Dictionary<Uri, BinaryEntry> index)
        {
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Writing binaries index"))
            {
                using(var fStream = GetIndexFileStream())
                {
                    Serializer.Serialize(index, fStream);
                }
            }
        }

        private FileStream GetIndexFileStream()
        {
            return new FileStream(GetCacheIndexLocation(), FileMode.OpenOrCreate);
        }

        private void ResetIndex()
        {
            File.WriteAllText(GetCacheIndexLocation(), string.Empty);
            var cacheDir = GetCacheLocation();
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
        }

        private string GetBinaryFileName(int id)
        {
            var cacheDir = GetCacheLocation();
            if(!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            return Path.Combine(cacheDir, "bin" + id);
        }

        private static string ChecksumToText(byte[] checksum)
        {
            return checksum.Select(x => x.ToString("x2")).Aggregate((x, y) => x + y);
        }

        private static string GetCacheLocation()
        {
            return Path.Combine(Emulator.UserDirectoryPath, CacheDirectory);
        }

        private static string GetCacheIndexLocation()
        {
            return Path.Combine(Emulator.UserDirectoryPath, CacheIndex);
        }

        private static string GetCacheIndexLockLocation()
        {
            return Path.Combine(Emulator.UserDirectoryPath, CacheLock);
        }

        private static byte[] GetSHA1Checksum(string fileName)
        {
            using(var file = new FileStream(fileName, FileMode.Open))
            using(var sha = new SHA1Managed())
            {
                sha.Initialize();
                return sha.ComputeHash(file);
            }
        }

        private static byte[] GetChecksumAndSizeFromUri(Uri uri, out long size)
        {
            size = 0;
            var groups = ChecksumRegex.Match(uri.ToString()).Groups;
            if(groups.Count != 3)
            {
                return null;
            }
            size = long.Parse(groups[1].Value);
            var checksumAsString = groups[2].Value;
            var result = new byte[20];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(new string(new[] {checksumAsString[2 * i], checksumAsString[2 * i + 1]}), NumberStyles.HexNumber);
            }
            return result;
        }

        static CachingFileFetcher()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                return true;
            };
        }

        private TimeSpan progressUpdateThreshold;
        private WebClient client;
        private object concurrentLock = new object();
        private const string CacheDirectory = "cached_binaries";
        private const string CacheIndex = "binaries_index";
        private const string CacheLock = "cache_lock";
        private readonly Dictionary<string, Uri> fetchedFiles;

        private static readonly Serializer Serializer = new Serializer(new Settings(versionTolerance: VersionToleranceLevel.AllowGuidChange, disableTypeStamping: true));
        private static readonly Regex ChecksumRegex = new Regex(@"-s_(\d+)-([a-f,0-9]{40})$");

        private class BinaryEntry
        {
            public BinaryEntry(int index, long size, byte[] checksum)
            {
                this.Index = index;
                this.Size = size;
                this.Checksum = checksum;
            }

            public int Index { get; set; }
            public long Size { get; set; }
            public byte[] Checksum { get; set; }
        }
    }
}
