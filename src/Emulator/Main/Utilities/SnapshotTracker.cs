//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities
{
    public class SnapshotTracker : IExternal
    {
        public SnapshotTracker()
        {
        }

        public string GetLastSnapshotBeforeOrAtTimeStamp(TimeInterval timeStamp)
        {
            // Look through from the latest to the earliest snapshot.
            var snapshotView = snapshots.GetViewBetween(EarliestSnapshot, new(timeStamp, null, uint.MaxValue)).Reverse();
            return GetFirstExistingSnapshot(snapshotView).Path;
        }

        public string GetSnapshotForGdbBeforeTimeStamp(TimeInterval timeStamp)
        {
            timeStamp -= TimeInterval.FromTicks(1);

            // Look for an existing snapshot with the latest possible timestamp.
            var snapshotView = snapshots.GetViewBetween(EarliestSnapshot, new(timeStamp, null, uint.MaxValue)).Reverse();
            var latestSnapshot = GetFirstExistingSnapshot(snapshotView);

            // Find the first snapshot created on 'latestSnapshot.TimeStamp'.
            var snapshotsOnTimestamp = snapshots.GetViewBetween(
                new(latestSnapshot.TimeStamp, null, 0),
                new(latestSnapshot.TimeStamp, null, uint.MaxValue));

            return GetFirstExistingSnapshot(snapshotsOnTimestamp).Path;
        }

        public void Save(TimeInterval timeStamp, string path)
        {
            var newSnapshot = new SnapshotDescriptor(timeStamp, path, nextSnapshoId++);
            snapshots.Add(newSnapshot);
        }

        public string PrintSnapshotsInfo()
        {
            return $"Count: {Count}\nTotal Size: {GetSnapshotSizeText(TotalSnapshotsSize)}";
        }

        public string[,] PrintDetailedSnapshotsInfo()
        {
            var table = new Table().AddRow("Path", "Timestamp", "Size");
            table.AddRows(snapshots,
                x => x.Path,
                x => x.TimeStamp.ToString(),
                x => GetSnapshotSizeText(new FileInfo(x.Path).Length)
            );

            return table.ToArray();
        }

        public int Count => snapshots.Count;

        public long TotalSnapshotsSize
        {
            get
            {
                var totalSize = 0L;

                var snapshotsToRemove = new List<SnapshotDescriptor>();
                foreach(var snapshot in snapshots)
                {
                    var snapshotInfo = new FileInfo(snapshot.Path);
                    if(snapshotInfo.Exists)
                    {
                        totalSize += snapshotInfo.Length;
                    }
                    else
                    {
                        snapshotsToRemove.Add(snapshot);
                    }
                }

                RemoveSnapshots(snapshotsToRemove);
                return totalSize;
            }
        }

        private static readonly SnapshotDescriptor EarliestSnapshot = new(TimeInterval.Empty, null, 0);

        private string GetSnapshotSizeText(long size)
        {
            Misc.CalculateUnitSuffix(size, out var value, out var unit);
            return $"{value:F2} {unit}";
        }

        private SnapshotDescriptor GetFirstExistingSnapshot(IEnumerable<SnapshotDescriptor> snapshotView)
        {
            SnapshotDescriptor firstSnapshot = null;

            var snapshotsToRemove = new List<SnapshotDescriptor>();
            foreach(var snapshot in snapshotView)
            {
                if(File.Exists(snapshot.Path))
                {
                    firstSnapshot = snapshot;
                    break;
                }
                else
                {
                    snapshotsToRemove.Add(snapshot);
                }
            }

            RemoveSnapshots(snapshotsToRemove);

            if(firstSnapshot != null)
            {
                return firstSnapshot;
            }

            throw new RecoverableException("There are no snapshots taken before this timestamp");
        }

        private void RemoveSnapshots(IEnumerable<SnapshotDescriptor> snapshotsToRemove)
        {
            foreach(var removedSnapshot in snapshotsToRemove)
            {
                snapshots.Remove(removedSnapshot);
            }
        }

        private uint nextSnapshoId = 0;

        private readonly SortedSet<SnapshotDescriptor> snapshots = new SortedSet<SnapshotDescriptor>();

        private record SnapshotDescriptor(TimeInterval TimeStamp, string Path, uint SnapshotId) : IComparable<SnapshotDescriptor>
        {
            public int CompareTo(SnapshotDescriptor other)
            {
                var timestampCompare = TimeStamp.CompareTo(other.TimeStamp);
                if(timestampCompare == 0)
                {
                    return SnapshotId.CompareTo(other.SnapshotId);
                }

                return timestampCompare;
            }

            public TimeInterval TimeStamp { get; init; } = TimeStamp;

            public string Path { get; init; } = Path;

            public uint SnapshotId { get; init; } = SnapshotId;
        }
    }
}
