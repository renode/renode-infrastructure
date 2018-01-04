//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Runtime.Serialization;
using LuceneNet = Lucene.Net;
using Antmicro.Renode.Logging.Lucene;

namespace Antmicro.Renode.Logging.Backends
{
    public class LuceneLoggerBackend : LoggerBackend
    {
        public static LuceneLoggerBackend Instance 
        {
            get
            {
                EnsureBackend();
                return instance;
            }
        }

        public static void EnsureBackend()
        {
            if(instance == null)
            {
                instance = new LuceneLoggerBackend();
                Logger.AddBackend(instance, LuceneLoggerBackendIdentifier);
            }
        }

        public override void Dispose()
        {
        }

        public Task<SearchResults> FilterHistoryViewAsync(string queryString, ViewFilter view, int count, Direction direction, ulong? referenceId = null)
        {
            return Task.Run<SearchResults>(() => FilterHistoryView(queryString, view, count, direction, referenceId));
        }

        public Task<SearchResults> FindAsync(string searchQueryString, ViewFilter view, int count, Direction direction, ulong? referenceId = null)
        {
            return Task.Run<SearchResults>(() => Find(searchQueryString, view, count, direction, referenceId));
        }

        public override void Log(LogEntry entry)
        {
            cache.Add(entry);
        }

        public Task RebuildIndexAsync(ProgressMonitor progressMonitor)
        {
            return Task.Run(() =>
            {
                using(var progress = progressMonitor.Start("Rebuilding index..."))
                {
                    cache.SwitchBuffer();
                    var converter = new ThreadLocal<LogEntryToDocumentConverter>(() => new LogEntryToDocumentConverter());
                    Parallel.ForEach(cache.ReadDataFromInactiveBuffer(progressMonitor), new ParallelOptions { MaxDegreeOfParallelism = 4 },  buffered =>
                    {
                        writer.AddDocument(converter.Value.ToDocument(buffered));
                    });
                    writer.Commit();
                    searcher.Refresh();
                }
            });
        }

        public override void SetLogLevel(LogLevel level, int sourceId = -1)
        {
            // this method is intentionally left blank
        }

        public override bool IsControllable { get { return false; } }

        private static LuceneLoggerBackend instance;

        private LuceneLoggerBackend()
        {
            indexDirectory = Path.Combine(TemporaryFilesManager.Instance.EmulatorTemporaryPath, "index");
            var directory = FSDirectory.Open(indexDirectory);
            analyzer = new StandardAnalyzer(LuceneNet.Util.Version.LUCENE_30);
            writer = new IndexWriter(directory, analyzer, IndexWriter.MaxFieldLength.LIMITED);

            writer.UseCompoundFile = false;

            logLevel = LogLevel.Noisy;
            searcher = new Searcher(directory, analyzer);

            cache = new FileCache();
        }

        private SearchResults FilterHistoryView(string queryString, ViewFilter view, int count, Direction direction, ulong? referenceId)
        {
            int totalCount;
            var result = searcher.Filter(queryString, view, count, direction, referenceId, out totalCount);
            return new SearchResults(totalCount, result);
        }

        private SearchResults Find(string queryString, ViewFilter view, int count, Direction direction, ulong? referenceId)
        {
            ulong foundId;
            int totalFound;
            var result = searcher.Find(queryString, view, count, direction, referenceId, out foundId, out totalFound);
            return new SearchResults(totalFound, result) { FoundId = foundId };
        }

        public const string IdFieldName = "id";
        public const string MachineFieldName = "machine";
        public const string MessageFieldName = "message";
        public const string SourceFieldName = "source";
        public const string SourceIdFieldName = "source_id";
        public const string TimeFiledName = "time";
        public const string TypeFiledName = "type";

        private readonly Analyzer analyzer;
        private readonly FileCache cache;
        private readonly string indexDirectory;
        private readonly Searcher searcher;
        private readonly IndexWriter writer;

        private const string LuceneLoggerBackendIdentifier = "lucene";
        private const int limit = 1000;

        private class FileCache
        {
            public FileCache()
            {
                currentBuffer = 1;
                SwitchBuffer();
            }

            public void Add(LogEntry entry)
            {
                var copyOfWriter = currentWriter;
                Interlocked.Increment(ref copyOfWriter.ReferenceCounter);
                entry.Save(copyOfWriter.Writer);
                Interlocked.Decrement(ref copyOfWriter.ReferenceCounter);
            }

            public IEnumerable<LogEntry> ReadDataFromInactiveBuffer(ProgressMonitor progressMonitor)
            {
                var monitoredAction = progressMonitor.Start("Reading log entries...", progressable: true);
                var lastUpdate = CustomDateTime.Now;
                switchSync.WaitOne();
                try
                {
                    var bufferPath = Path.Combine(TemporaryFilesManager.Instance.EmulatorTemporaryPath, string.Format("{0}.{1}", BUFFER_FILE, (currentBuffer + 1) % 2));
                    using(var inactiveBuffer = File.OpenRead(bufferPath))
                    {
                        var primitiveReader = new PrimitiveReader(inactiveBuffer, false);
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        var entriesCount = 0;

                        LogEntry entry;
                        while(TryDeserializeEntry(primitiveReader, out entry))
                        {
                            if(stopwatch.Elapsed > TimeSpan.FromMilliseconds(50))
                            {
                                monitoredAction.UpdateProgress((int)(100.0 * inactiveBuffer.Position / inactiveBuffer.Length),
                                    string.Format("Reading log entries ({0})...", Interlocked.Add(ref entriesCount, 0)));
                                stopwatch.Restart();
                            }
                            yield return entry;
                            Interlocked.Increment(ref entriesCount);
                        }

                        monitoredAction.UpdateProgress(100);
                    }
                }
                finally
                {
                    switchSync.Release();
                }
            }

            public void SwitchBuffer()
            {
                switchSync.WaitOne();
                bufferSync.WaitOne();
                try
                {
                    var newBuffer = (currentBuffer + 1) % 2;
                    var newFileStream = File.Create(Path.Combine(TemporaryFilesManager.Instance.EmulatorTemporaryPath, string.Format("{0}.{1}", BUFFER_FILE, newBuffer)));
                    var newWriter = new WriterDescriptor { Writer = new PrimitiveWriter(newFileStream, false) };

                    var oldWriter = currentWriter;
                    currentWriter = newWriter;

                    if(oldWriter != null)
                    {
                        while (oldWriter.ReferenceCounter > 0)
                        {
                            Thread.Sleep(10);
                        }

                        oldWriter.Writer.Dispose();
                        currentFileStream.Dispose();
                    }

                    currentBuffer = newBuffer;
                    currentFileStream = newFileStream;
                }
                finally
                {
                    bufferSync.Release();
                    switchSync.Release();
                }
            }


            private static bool TryDeserializeEntry(PrimitiveReader reader, out LogEntry entry)
            {
                var localEntry = (LogEntry)FormatterServices.GetUninitializedObject(typeof(LogEntry));
                try 
                {
                    localEntry.Load(reader);
                    entry = localEntry;
                    return true;
                } 
                catch (EndOfStreamException)
                {
                    // intentionally left blank
                }
                entry = null;
                return false;
            }

            private readonly Semaphore bufferSync = new Semaphore(1, 1);
            private readonly Semaphore switchSync = new Semaphore(1, 1);
            private int currentBuffer = 0;
            private FileStream currentFileStream;
            private WriterDescriptor currentWriter;

            private const string BUFFER_FILE = "buffer-file";

            private class WriterDescriptor
            {
                public PrimitiveWriter Writer;
                public int ReferenceCounter;
            }
        }

        private class LogEntryToDocumentConverter
        {
            public LogEntryToDocumentConverter()
            {
                document = new Document();

                machineField = new Field(MachineFieldName, string.Empty, Field.Store.YES, Field.Index.ANALYZED);
                sourceField = new Field(SourceFieldName, string.Empty, Field.Store.YES, Field.Index.ANALYZED);
                sourceIdField = new NumericField(SourceIdFieldName, Field.Store.YES, false);
                idField = new NumericField(IdFieldName, Field.Store.YES, true);
                typeField = new Field(TypeFiledName, string.Empty, Field.Store.YES, Field.Index.ANALYZED);
                messageField = new Field(MessageFieldName, string.Empty, Field.Store.YES, Field.Index.ANALYZED);
                timeField = new NumericField(TimeFiledName, Field.Store.YES, true);

                document.Add(idField);
                document.Add(machineField);
                document.Add(sourceField);
                document.Add(typeField);
                document.Add(messageField);
                document.Add(timeField);
                document.Add(sourceIdField);
            }

            public Document ToDocument(LogEntry entry)
            {
                machineField.SetValue(entry.MachineName ?? string.Empty);
                sourceField.SetValue(entry.ObjectName ?? string.Empty);
                idField.SetLongValue((long)entry.Id);
                typeField.SetValue(entry.Type.ToString());
                messageField.SetValue(entry.Message);
                timeField.SetLongValue(entry.Time.ToBinary());
                sourceIdField.SetIntValue(entry.SourceId);

                return document;
            }

            public LogEntry ToLogEntry(Document doc)
            {
                LogLevel ll;
                LogLevel.TryParse(doc.Get(TypeFiledName), out ll);

                var result = new LogEntry(DateTime.FromBinary(long.Parse(doc.Get(TimeFiledName))), ll, doc.Get(MessageFieldName), int.Parse(doc.Get(SourceIdFieldName)));
                result.Id = ulong.Parse(doc.Get(IdFieldName));
                return result;
            }

            private NumericField idField;
            private Document document;
            private Field machineField;
            private Field messageField;
            private Field sourceField;
            private NumericField sourceIdField;
            private NumericField timeField;
            private Field typeField;
        }

        private class Searcher
        {
            public Searcher(LuceneNet.Store.Directory dir, Analyzer analyzer)
            {
                directory = dir;
                searcher = new IndexSearcher(directory);
                this.analyzer = analyzer;
                converter = new LogEntryToDocumentConverter();
            }

            public void Refresh()
            {
                lock(searcherLock)
                {
                    searcher.Dispose();
                    searcher = new IndexSearcher(directory);
                }
            }

            public IEnumerable<LogEntry> Filter(string queryString, ViewFilter view, int count, Direction direction, ulong? referenceId, out int totalCount)
            {
                var context = new IncermentalSearchContext(count, direction, referenceId);

                var parser = new CustomQueryParser(LuceneNet.Util.Version.LUCENE_30, MessageFieldName, analyzer);
                var sortBy = new Sort(new SortField(IdFieldName, SortField.LONG, direction == Direction.Backward));
                while(true)
                {
                    var combinedQueryString = Searcher.CombineQuery(queryString, view, context.Range);
                    if(combinedQueryString.Length == 0)
                    {
                        totalCount = 0;
                        return new LogEntry[0];
                    }

                    var query = parser.Parse(combinedQueryString);

                    lock(searcherLock)
                    {
                        var searchResults = searcher.Search(query, null, count, sortBy);

                        totalCount = searchResults.TotalHits;
                        var result = searchResults.ScoreDocs.Take(count).Select(d => searcher.Doc(d.Doc)).Select(x => converter.ToLogEntry(x)); 

                        context.UpdateResult(result);
                        if(context.IsFinished)
                        {
                            return (direction == Direction.Backward) ? context.Result.Reverse() : context.Result;
                        }

                        context.UpdateRange();
                    }
                }
            }

            private ulong? FindFirstId(string queryString, ViewFilter view, Direction direction, ulong? referenceId, out int totalCount)
            {
                if(direction == Direction.Backward)
                {
                    if(referenceId.HasValue && referenceId.Value > 0)
                    {
                        referenceId--;
                    }
                }
                else
                {
                    if(referenceId.HasValue && referenceId.Value < ulong.MaxValue)
                    {
                        referenceId++;
                    }
                }

                var context = new IncermentalSearchContext(1, direction, referenceId);
                while(true)
                {
                    var combinedQueryString = Searcher.CombineQuery(queryString, view, context.Range);
                    if(combinedQueryString.Length == 0)
                    {
                        totalCount = 0;
                        return null;
                    }

                    var parser = new CustomQueryParser(LuceneNet.Util.Version.LUCENE_30, MessageFieldName, analyzer);
                    var query = parser.Parse(combinedQueryString);
                    var sortBy = new Sort(new SortField(IdFieldName, SortField.LONG, direction == Direction.Backward));

                    lock(searcherLock)
                    {
                        var searchResults = searcher.Search(query, null, 1, sortBy);
                        var firstResult = searchResults.ScoreDocs.FirstOrDefault();

                        totalCount = searchResults.TotalHits;
                        if(firstResult != null)
                        {
                            return converter.ToLogEntry(searcher.Doc(firstResult.Doc)).Id;
                        }
                        if(context.IsFinished)
                        {
                            return null;
                        }

                        context.UpdateRange();
                    }
                }
            }

            public IEnumerable<LogEntry> Find(string queryString, ViewFilter view, int count, Direction direction, ulong? referenceId, out ulong foundId, out int totalFound)
            {
                int fake;

                var findFirstResult = FindFirstId(queryString, view, direction, referenceId, out totalFound);
                if(findFirstResult == null)
                {
                    foundId = 0;
                    return null;
                }

                foundId = findFirstResult.Value;
                var resultsBefore = Filter(string.Empty, view, count, Direction.Backward, foundId, out fake).ToList();
                var resultsAfter  = Filter(string.Empty, view, count, Direction.Forward,  foundId, out fake).Skip(1).ToList();
                var foundResult = resultsBefore.Last();
                resultsBefore.Remove(foundResult);

                IEnumerable<LogEntry> result;

                if(resultsBefore.Count < (count / 2))
                {
                    result = resultsBefore.Concat(new [] { foundResult }).Concat(resultsAfter).Take(count).ToList();
                }
                else if(resultsAfter.Count < ((count - 1) / 2))
                {
                    result = resultsBefore.Concat(new [] { foundResult }).Concat(resultsAfter).ToList();
                    result = result.Skip(((List<LogEntry>)result).Count - count).ToList();
                }
                else
                {
                    result = resultsBefore.Skip(resultsBefore.Count - (count / 2)).Concat(new [] { foundResult }).Concat(resultsAfter.Take((count - 1) / 2)).ToList();
                }

                return result;
            }

            private static string CombineQuery(string queryString, ViewFilter view, Range forcedRange)
            {
                var values = new List<string>();

                var viewQueryString = view.GenerateQueryString();
                if(!string.IsNullOrEmpty(viewQueryString))
                {
                    values.Add(viewQueryString);
                }

                if(!string.IsNullOrEmpty(queryString))
                {
                    values.Add(queryString);
                }

                if(forcedRange != null)
                {
                    values.Add(forcedRange.GenerateQueryString());
                }

                return string.Join(" AND ", values.Select(x => string.Format("({0})", x)));
            }

            private readonly LogEntryToDocumentConverter converter;
            private readonly Analyzer analyzer;
            private readonly LuceneNet.Store.Directory directory;
            private IndexSearcher searcher;
            private readonly object searcherLock = new object();

            private class CustomQueryParser : QueryParser
            {
                public CustomQueryParser(LuceneNet.Util.Version matchVersion, string f, Analyzer a) : base(matchVersion, f, a)
                {
                }

                protected override Query NewTermQuery(Term term)
                {
                    long value;
                    if(long.TryParse(term.Text, out value))
                    {
                        return NumericRangeQuery.NewLongRange(term.Field, value, value, true, true);
                    }
                    return base.NewTermQuery(term);
                }

                protected override Query NewRangeQuery(string field, string part1, string part2, bool inclusive)
                {
                    long valuePart1;
                    long valuePart2;
                    if(long.TryParse(part1, out valuePart1) && long.TryParse(part2, out valuePart2))
                    {
                        return NumericRangeQuery.NewLongRange(field, valuePart1, valuePart2, inclusive, inclusive);
                    }
                    return base.NewRangeQuery(field, part1, part2, inclusive);
                }
            }

            private class IncermentalSearchContext
            {
                public IncermentalSearchContext(int count, Direction direction, ulong? referenceId)
                {
                    Result = new List<LogEntry>();

                    if (!referenceId.HasValue)
                    {
                        MaximalCount = 0;
                        return;
                    }

                    MaximalCount = count;
                    this.direction = direction;
                    currentStep = (ulong)count;

                    Range = new Range();

                    if (direction == Direction.Backward)
                    {
                        Range.MinimalId = SubstractToZero(referenceId.Value, currentStep);
                        Range.MaximalId = referenceId;
                    }
                    else
                    {
                        Range.MinimalId = referenceId;
                        Range.MaximalId = AddToMaxUlong(referenceId.Value, currentStep);
                    }
                }

                public Range Range { get; private set; }

                public int MaximalCount { get; private set; }

                public bool IsFinished 
                { 
                    get 
                    { 
                        return (Result.Count() >= MaximalCount) 
                            || (direction == Direction.Backward && Range.MinimalId == 0) 
                            || (direction == Direction.Forward && Range.MaximalId == ulong.MaxValue); 
                    }  
                }

                public IEnumerable<LogEntry> Result { get; private set; }

                public void UpdateResult(IEnumerable<LogEntry> entries)
                {
                    ((List<LogEntry>)Result).AddRange(entries);
                }

                public void UpdateRange()
                {
                    currentStep = MultiplyToMaxUlong(currentStep, Multiplier);

                    if(direction == Direction.Backward)
                    {
                        Range.MaximalId = Range.MinimalId;
                        Range.MinimalId = SubstractToZero(Range.MaximalId.Value, currentStep);
                    }
                    else
                    {
                        Range.MinimalId = Range.MaximalId;
                        Range.MaximalId = AddToMaxUlong(Range.MaximalId.Value, currentStep);
                    }
                }

                private static ulong SubstractToZero(ulong val1, ulong val2)
                {
                    return (val2 >= val1) ? 0 : val1 - val2;
                }

                private static ulong AddToMaxUlong(ulong val1, ulong val2)
                {
                    return (ulong.MaxValue - val1 < val2) ? ulong.MaxValue : val1 + val2;
                }

                private static ulong MultiplyToMaxUlong(ulong val1, double val2)
                {
                    return (ulong.MaxValue / val2) < val1 ? ulong.MaxValue : (ulong)(val1 * val2);
                }

                private Direction direction;
                private ulong currentStep;

                private const double Multiplier = 2.5;
            }
        }
    }
}
