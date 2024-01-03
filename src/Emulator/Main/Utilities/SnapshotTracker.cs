//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public class SnapshotTracker : IExternal
    {
        public SnapshotTracker()
        {
            snapshots = new List<SnapshotDescriptor>();
            snapshotComparer = new SnapshotComparer();
        }

        public string GetLastSnapshotBeforeOrAtTimeStamp(TimeInterval timeStamp)
        {
            int index = snapshots.BinarySearch(new SnapshotDescriptor(timeStamp, null), snapshotComparer);

            if(index == -1)
            {
                throw new RecoverableException("There are no snapshots taken before this timestamp.");
            }

            if(index < 0)
            {
                // binary search returned bitwise complement of the index of the least element with larger timestamp
                // we decrement index to get the largest element less than given one
                index = ~index;
                index--;
            }

            while(index >= 0 && !File.Exists(snapshots[index].Path))
            {
                snapshots.RemoveAt(index);
                index--;
            }

            if(index >= 0)
            {
                return snapshots[index].Path;
            }

            throw new RecoverableException("There are no snapshots taken before this timestamp.");
        }

        public void Save(TimeInterval timeStamp, string path)
        {
            var newSnap = new SnapshotDescriptor(timeStamp, path);

            int index = snapshots.BinarySearch(newSnap, snapshotComparer);

            // only save snapshot if there is no other with the same timestamp to keep
            // the oldest snapshot at given virtual time in order to cover more breakpoints
            if(index < 0)
            {
                // binary search returned bitwise complement of the index of the first element larger than the new one
                snapshots.Insert(~index, newSnap);
            }
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

        public long TotalSnapshotsSize => snapshots.Select(x => new FileInfo(x.Path)).Where(x => x.Exists).Sum(x => x.Length);

        private string GetSnapshotSizeText(long size)
        {
            Misc.CalculateUnitSuffix(size, out var value, out var unit);
            return $"{value:F2} {unit}";
        }

        private readonly List<SnapshotDescriptor> snapshots;
        private readonly SnapshotComparer snapshotComparer;

        private class SnapshotDescriptor
        {
            public SnapshotDescriptor(TimeInterval timeStamp, string path)
            {
                TimeStamp = timeStamp;
                Path = path;
            }

            public TimeInterval TimeStamp { get; }
            public string Path { get; }
        }

        private class SnapshotComparer : IComparer<SnapshotDescriptor>
        {
            public int Compare(SnapshotDescriptor snap1, SnapshotDescriptor snap2)
            {
                return snap1.TimeStamp.CompareTo(snap2.TimeStamp);
            }
        }
    }
}

