//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;
using System;

namespace Antmicro.Renode.Utilities.Collections
{
    public class MultiTreeNode<TValue, TConnectionWay> : TreeBase<MultiTreeNode<TValue, TConnectionWay>, TValue>
    {
        internal MultiTreeNode(TValue value, MultiTree<TValue, TConnectionWay> root) : base(value)
        {
            if(root == null)
            {
                root = (MultiTree<TValue, TConnectionWay>)this;
            }
            this.root = root;
            ConnectionWays = new List<TConnectionWay>();
        }

        public override MultiTreeNode<TValue, TConnectionWay> AddChild(TValue value)
        {
            return AddChild(value, default(TConnectionWay));
        }

        public MultiTreeNode<TValue, TConnectionWay> AddChild(TValue value, TConnectionWay connectionWay)
        {
            var node = root.FindOrCreateNode(value);
            node.SetParent(this);
            ChildrenList.Add(node);
            ConnectionWays.Add(connectionWay);
            return node;
        }

        public void SetParent(MultiTreeNode<TValue, TConnectionWay> parentNode)
        {
            ParentsList.Add(parentNode);
        }

        public IEnumerable<TConnectionWay> GetConnectionWays(TValue value)
        {
            var childNode = root.GetNode(value);
            var valueIndices = ChildrenList.IndicesOf(childNode);
            foreach(var index in valueIndices)
            {
                if(index == -1)
                {
                    throw new KeyNotFoundException($"Could not find child with value '{value}'.");
                }
                yield return ConnectionWays[index];
            }
        }

        public void OnConnectionWays(Action<TValue, TConnectionWay> handler)
        {
            for(var i = 0; i < ChildrenList.Count; i++)
            {
                handler(ChildrenList[i].Value, ConnectionWays[i]);
            }
        }

        public override void RemoveChild(TValue value)
        {
            int index;
            var nodeToRemove = root.GetNode(value);
            var removed = false;
            while((index = ChildrenList.IndexOf(nodeToRemove)) != -1)
            {
                ChildrenList.RemoveAt(index);
                ConnectionWays.RemoveAt(index);
                removed = true;
            }
            if(!removed)
            {
                throw new InvalidOperationException($"Node '{Value}' does not have child '{value}'.");
            }
            root.GarbageCollect();
        }

        public void RemoveChild(TConnectionWay connectionWay, Func<TValue, bool> tester = null)
        {
            var connectionWayIndices = ConnectionWays.IndicesOf(connectionWay);
            var removed = false;
            foreach(var index in connectionWayIndices.OrderByDescending(x => x))
            {
                if(tester != null && !tester(ChildrenList[index].Value))
                {
                    root.GarbageCollect();
                    return;
                }

                ChildrenList.RemoveAt(index);
                ConnectionWays.RemoveAt(index);
                removed = true;
            }
            if(!removed)
            {
                throw new InvalidOperationException($"Node '{Value}' does not have child connected by '{connectionWay}'.");
            }
            root.GarbageCollect();
        }

        public void ReplaceConnectionWay(TConnectionWay oldValue, TConnectionWay newValue)
        {
            if(newValue.Equals(oldValue))
            {
                return;
            }

            int index;
            var replaced = false;
            while((index = ConnectionWays.IndexOf(oldValue)) != -1)
            {
                ConnectionWays[index] = newValue;
                replaced = true;
            }
            if(!replaced)
            {
                throw new InvalidOperationException($"Node '{Value}' does not have child with connection way '{oldValue}'.");
            }
        }

        protected void TraverseWithConnectionWaysChildrenOnly(Action<MultiTreeNode<TValue, TConnectionWay>, TConnectionWay, TValue, int> nodeHandler, int initialLevel)
        {
            for(var i = 0; i < ChildrenList.Count; i++)
            {
                nodeHandler(ChildrenList[i], ConnectionWays[i], Value, initialLevel + 1);
                ChildrenList[i].TraverseWithConnectionWaysChildrenOnly(nodeHandler, initialLevel + 1);
            }
        }

        protected readonly List<TConnectionWay> ConnectionWays;
        private readonly MultiTree<TValue, TConnectionWay> root;
    }

    public static class ListExtensions
    {
        public static IEnumerable<int> IndicesOf<T>(this List<T> list, T value)
        {
            var index = 0;
            while((index = list.IndexOf(value, index)) != -1)
            {
                yield return index++;
            }
        }
    }
}
