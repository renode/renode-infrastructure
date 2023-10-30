//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities.Collections
{

    public class MultiTree<TValue, TConnectionWay> : MultiTreeNode<TValue, TConnectionWay>
    {
        public MultiTree(TValue value) : base(value, null)
        {
            valueToNode = new Dictionary<TValue, MultiTreeNode<TValue, TConnectionWay>>();
            valueToNode.Add(value, this);
        }

        public MultiTreeNode<TValue, TConnectionWay> GetNode(TValue value)
        {
            return valueToNode[value];
        }

        public bool TryGetNode(TValue value, out MultiTreeNode<TValue, TConnectionWay> node)
        {
            node = null;
            if(valueToNode.ContainsKey(value))
            {
                node = valueToNode[value];
                return true;
            }
            return false;
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                return valueToNode.Select(x => x.Key).ToArray();
            }
        }

        public bool ContainsValue(TValue value)
        {
            return valueToNode.ContainsKey(value);
        }

        public void TraverseWithConnectionWaysParentFirst(Action<MultiTreeNode<TValue, TConnectionWay>, TConnectionWay, TValue, int> nodeHandler, int initialLevel)
        {
            nodeHandler(this, default(TConnectionWay), default(TValue), initialLevel);
            TraverseWithConnectionWaysChildrenOnly(nodeHandler, initialLevel);
        }

        internal MultiTreeNode<TValue, TConnectionWay> FindOrCreateNode(TValue value)
        {
            if(valueToNode.ContainsKey(value))
            {
                return valueToNode[value];
            }
            var newNode = new MultiTreeNode<TValue, TConnectionWay>(value, this);
            valueToNode.Add(value, newNode);
            return newNode;
        }

        internal void GarbageCollect()
        {
            var toDelete = valueToNode.Select(x => x.Key).Except(this).ToArray();
            foreach(var value in toDelete)
            {
                valueToNode.Remove(value);
            }
        }

        private readonly Dictionary<TValue, MultiTreeNode<TValue, TConnectionWay>> valueToNode;
    }
}
