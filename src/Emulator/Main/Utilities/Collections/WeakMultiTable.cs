//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;

namespace Antmicro.Renode.Utilities.Collections
{
    public class WeakMultiTable<TLeft, TRight>
    {
        public WeakMultiTable()
        {
            sync = new object();
            Init();
        }

        public void Add(TLeft left, TRight right)
        {
            lock(sync)
            {
                lefts.Add(new WeakReference(left));
                rights.Add(new WeakReference(right));
            }
        }

        public IEnumerable<TRight> GetAllForLeft(TLeft left)
        {
            lock(sync)
            {
                try
                {
                    Snapshot();
                    var neededIndices = new HashSet<int>(snapshotLefts.Select((l, index) => new { l, index }).Where(x => x.l.Equals(left)).Select(x => x.index));
                    return snapshotRights.Where((r, index) => neededIndices.Contains(index)).Distinct().ToList();
                }
                finally
                {
                    FreeSnapshot();
                }
            }
        }

        public IEnumerable<TLeft> GetAllForRight(TRight right)
        {
            lock(sync)
            {
                try
                {
                    Snapshot();
                    var neededIndices = new HashSet<int>(snapshotRights.Select((r, index) => new { r, index }).Where(x => x.r.Equals(right)).Select(x => x.index));
                    return snapshotLefts.Where((l, index) => neededIndices.Contains(index)).Distinct().ToList();
                }
                finally
                {
                    FreeSnapshot();
                }
            }
        }

        public void RemovePair(TLeft left, TRight right)
        {
            lock(sync)
            {
                try
                {
                    Snapshot();
                    for(var i = 0; i < snapshotLefts.Count; i++)
                    {
                        if(left.Equals(snapshotLefts[i]) && right.Equals(snapshotRights[i]))
                        {
                            snapshotLefts.RemoveAt(i);
                            snapshotRights.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }
                    FromSnapshot();
                }
                finally
                {
                    FreeSnapshot();
                }
            }
        }

        private void Init()
        {
            lefts = new List<WeakReference>();
            rights = new List<WeakReference>();
        }

        private void Snapshot()
        {
            snapshotLefts = new List<TLeft>();
            snapshotRights = new List<TRight>();
            lock(sync)
            {
                for(var i = 0; i < lefts.Count; i++)
                {
                    var left = (TLeft)lefts[i].Target;
                    var right = (TRight)rights[i].Target;
                    if(left == null || right == null)
                    {
                        lefts.RemoveAt(i);
                        rights.RemoveAt(i);
                        i--;
                        continue;
                    }
                    snapshotLefts.Add(left);
                    snapshotRights.Add(right);
                }
            }
        }

        private void FromSnapshot()
        {
            lefts = snapshotLefts.Select(x => new WeakReference(x)).ToList();
            rights = snapshotRights.Select(x => new WeakReference(x)).ToList();
        }

        private void FreeSnapshot()
        {
            snapshotLefts = null;
            snapshotRights = null;
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            Snapshot();
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            FreeSnapshot();
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            FromSnapshot();
            FreeSnapshot();
        }

        private readonly object sync;
        private List<TLeft> snapshotLefts;
        private List<TRight> snapshotRights;

        [Transient]
        private List<WeakReference> lefts;

        [Transient]
        private List<WeakReference> rights;
    }
}

