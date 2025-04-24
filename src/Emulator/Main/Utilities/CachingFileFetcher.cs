//
// Copyright (c) 2010-2024 Antmicro
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
    public class CachingFileFetcher : IDisposable
    {
        public CachingFileFetcher()
        {
            fetchedFiles = new Dictionary<string, string>();
            progressUpdateThreshold = TimeSpan.FromSeconds(0.25);
        }

        public IDictionary<string, string> GetFetchedFiles()
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

            try
            {
                var disableCaching = Emulator.InCIMode;

                return disableCaching
                    ? TryFetchFromUriInner(uri, out fileName)
                    : TryFetchFromCacheOrUriInner(uri, out fileName);
            }
            finally
            {
                Monitor.Exit(concurrentLock);
            }
        }

        public void Dispose()
        {
            if(EmulationManager.DisableEmulationFilesCleanup)
            {
                return;
            }

            foreach(var file in fetchedFiles.Keys)
            {
                try
                {
                    File.Delete(file);
                }
                catch(Exception)
                {
                    // nothing we can do
                }
            }
        }

        private bool TryFetchFromCacheOrUriInner(Uri uri, out string fileName)
        {
            using(var locker = new FileLocker(GetCacheIndexLockLocation()))
            {
                if(TryGetFromCache(uri, out fileName))
                {
                    fetchedFiles.Add(fileName, uri.ToString());
                    return true;
                }

                if(TryFetchFromUriInner(uri, out fileName))
                {
                    UpdateInCache(uri, fileName);
                    return true;
                }
            }

            fileName = null;
            return false;
        }

        private bool TryFetchFromUriInner(Uri uri, out string fileName)
        {
            fileName = TemporaryFilesManager.Instance.GetTemporaryFile(Path.GetExtension(uri.AbsoluteUri));
            // try download the file a few times times
            // in order to handle some intermittent
            // network problems
            if(!TryDownload(uri, fileName, DownloadAttempts))
            {
                return false;
            }

            // at this point the file has been successfully downloded;
            // now verify its size and checksum based on the information
            // encoded in URI
            // NOTE: there is no point in redownloading the file as a result
            // of checksum/size verification failure; we are using TCP/IP
            // protocol that guarantee a failure-free communication, so
            // a checksum/size mismatch must be a result of a broken file
            // at server side or a wrong URI
            if(TryGetChecksumAndSizeFromUri(uri, out var checksum, out var size))
            {
                if(!VerifySize(fileName, size))
                {
                    Logger.Log(LogLevel.Error, "Wrong size of the downloaded file, aborting");
                    return false;
                }

                if(!VerifyChecksum(fileName, checksum))
                {
                    Logger.Log(LogLevel.Error, "Wrong checksum of the downloaded file, aborting");
                    return false;
                }
            }

            if(uri.ToString().EndsWith(".gz", StringComparison.InvariantCulture))
            {
                fileName = Decompress(fileName);
            }

            fetchedFiles.Add(fileName, uri.ToString());

            return true;
        }

        private bool TryDownload(Uri uri, string fileName, int attemptsLimit)
        {
            var attempts = 0;
            do
            {
                if(!TryDownloadInner(uri, fileName, out var error))
                {
                    if(error == null)
                    {
                        Logger.LogAs(this, LogLevel.Info, "Download cancelled.");
                        return false;
                    }
                    else
                    {
                        var webException = error as WebException;
                        Logger.Log(LogLevel.Error, "Failed to download from {0}, reason: {1} (attempt {2}/{3})", uri, webException != null ? ResolveWebException(webException) : error.Message, attempts + 1, attemptsLimit);
                    }
                }
                else
                {
                    Logger.LogAs(this, LogLevel.Info, "Download done.");
                    return true;
                }
            }
            while(++attempts < attemptsLimit);

            Logger.Log(LogLevel.Error, "Download failed {0} times, aborting.", attempts);
            return false;
        }

        private bool TryDownloadInner(Uri uri, string fileName, out Exception error)
        {
            Exception localError = null;
            var wasCancelled = false;

            using(var downloadProgressHandler = EmulationManager.Instance.ProgressMonitor.Start(GenerateProgressMessage(uri), false, true))
            using(client = new ImpatientWebClient())
            {
                Logger.LogAs(this, LogLevel.Info, "Downloading {0}.", uri);
                var now = CustomDateTime.Now;
                var bytesDownloaded = 0L;
                client.DownloadProgressChanged += (sender, e) =>
                {
                    var newNow = CustomDateTime.Now;

                    var period = newNow - now;
                    if(period > progressUpdateThreshold)
                    {
                        downloadProgressHandler.UpdateProgress(e.ProgressPercentage,
                            GenerateProgressMessage(uri,
                                e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage, 1.0 * (e.BytesReceived - bytesDownloaded) / period.TotalSeconds));

                        now = newNow;
                        bytesDownloaded = e.BytesReceived;
                    }
                };
                var resetEvent = new ManualResetEvent(false);
                client.DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e)
                {
                    localError = e.Error;
                    if(e.Cancelled)
                    {
                        wasCancelled = true;
                    }
                    resetEvent.Set();
                };
                client.DownloadFileAsync(uri, fileName);
                resetEvent.WaitOne();
                error = localError;
            }
            client = null;

            return !wasCancelled && error == null;
        }

        private string Decompress(string fileName)
        {
            var decompressedFile = TemporaryFilesManager.Instance.GetTemporaryFile();

            using(var decompressionProgressHandler = EmulationManager.Instance.ProgressMonitor.Start("Decompressing file"))
            {
                Logger.Log(LogLevel.Info, "Decompressing file");
                using(var gzipStream = new GZipStream(File.OpenRead(fileName), CompressionMode.Decompress))
                using(var outputStream = File.OpenWrite(decompressedFile))
                {
                    gzipStream.CopyTo(outputStream);
                }
                Logger.Log(LogLevel.Info, "Decompression done");
            }

            return decompressedFile;
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
                // https://github.com/mono/mono/issues/9808
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
                if(!index.TryGetValue(uri.ToString(), out entry))
                {
                    return false;
                }
                var fileToCopy = GetBinaryFileName(entry.Index);
                if(!VerifyCachedFile(fileToCopy, entry))
                {
                    return false;
                }
                fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(GetBinaryFileName(entry.Index), fileName, true);
                return true;
            }
        }

        private bool VerifyCachedFile(string fileName, BinaryEntry entry)
        {
            if(!File.Exists(fileName))
            {
                Logger.LogAs(this, LogLevel.Warning, "Binary {0} found in index but is missing in cache.", fileName);
                return false;
            }

            if(entry.Checksum == null)
            {
                return true;
            }

            return VerifySize(fileName, entry.Size) && VerifyChecksum(fileName, entry.Checksum);
        }

        private bool VerifySize(string fileName, long expectedSize)
        {
            var actualSize = new FileInfo(fileName).Length;
            if(actualSize != expectedSize)
            {
                Logger.LogAs(this, LogLevel.Warning, "Size of the file differs: is {0}B, should be {1}B.", actualSize, expectedSize);
                return false;
            }
            return true;
        }

        private bool VerifyChecksum(string fileName, byte[] expectedChecksum)
        {
            if(!ConfigurationManager.Instance.Get("file-fetcher", "calculate-checksum", true))
            {
                // with a disabled checksum verification we pretend everything is peachy
                return true;
            }

            byte[] checksum;
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Calculating SHA1 checksum..."))
            {
                checksum = GetSHA1Checksum(fileName);
            }
            if(!checksum.SequenceEqual(expectedChecksum))
            {
                Logger.LogAs(this, LogLevel.Warning, "Checksum of the file differs, is {0}, should be {1}.", ChecksumToText(checksum), ChecksumToText(expectedChecksum));
                return false;
            }
            return true;
        }

        private void UpdateInCache(Uri uri, string withFile)
        {
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Updating cache"))
            {
                lock(CacheDirectory)
                {
                    var index = ReadBinariesIndex();
                    BinaryEntry entry;
                    var fileId = 0;
                    if(!index.TryGetValue(uri.ToString(), out entry))
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

                    // checksum will be 'null' if the uri pattern does not contain
                    // checksum/size information
                    TryGetChecksumAndSizeFromUri(uri, out var checksum, out var size);
                    index[uri.ToString()] = new BinaryEntry(fileId, size, checksum);
                    WriteBinariesIndex(index);
                }
            }
        }

        private Dictionary<string, BinaryEntry> ReadBinariesIndex()
        {
            using(var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Reading cache"))
            {
                using(var fStream = GetIndexFileStream())
                {
                    if(fStream.Length == 0)
                    {
                        return new Dictionary<string, BinaryEntry>();
                    }
                    Dictionary<string, BinaryEntry> result;
                    if(Serializer.TryDeserialize<Dictionary<string, BinaryEntry>>(fStream, out result) != DeserializationResult.OK)
                    {
                        Logger.LogAs(this, LogLevel.Warning, "There was an error while loading index file. Cache will be rebuilt.");
                        fStream.Close();
                        ResetIndex();
                        return new Dictionary<string, BinaryEntry>();
                    }
                    return result;
                }
            }
        }

        private void WriteBinariesIndex(Dictionary<string, BinaryEntry> index)
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
            if(Directory.Exists(cacheDir))
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
            using(var sha = SHA1.Create())
            {
                sha.Initialize();
                return sha.ComputeHash(file);
            }
        }

        private static bool TryGetChecksumAndSizeFromUri(Uri uri, out byte[] checksum, out long size)
        {
            size = 0;
            checksum = null;

            var groups = ChecksumRegex.Match(uri.ToString()).Groups;
            if(groups.Count != 3)
            {
                return false;
            }

            // regex check above ensures that all the data below is parsable
            size = long.Parse(groups[1].Value);
            var checksumAsString = groups[2].Value;
            checksum = new byte[20];
            for(var i = 0; i < checksum.Length; i++)
            {
                checksum[i] = byte.Parse(checksumAsString.Substring(2 * i, 2), NumberStyles.HexNumber);
            }

            return true;
        }

        static CachingFileFetcher()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                return true;
            };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        private TimeSpan progressUpdateThreshold;
        private WebClient client;
        private object concurrentLock = new object();
        private const string CacheDirectory = "cached_binaries";
        private const string CacheIndex = "binaries_index";
        private const string CacheLock = "cache_lock";
        private readonly Dictionary<string, string> fetchedFiles;

        private static readonly Serializer Serializer = new Serializer(new Settings(versionTolerance: VersionToleranceLevel.AllowGuidChange, disableTypeStamping: true));
        private static readonly Regex ChecksumRegex = new Regex(@"-s_(\d+)-([a-f,0-9]{40})$");

        private const int DownloadAttempts = 5;

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

#pragma warning disable SYSLIB0014 // Even though WebClient is technically obselete, there's no better replacement for our use case
        private class ImpatientWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                // This 15s timeout refers to the connection to the server, not the whole download duration
                w.Timeout = 15 * 1000;
                return w;
            }
        }
#pragma warning restore SYSLIB0014
    }
}
