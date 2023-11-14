//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Linq;

namespace Antmicro.Renode.Utilities.Collections
{

    public abstract class TreeBase<TNode, TValue> : IEnumerable<TValue> where TNode : TreeBase<TNode, TValue>
    {
        protected TreeBase(TValue value)
        {
            this.value = value;
            ChildrenList = new List<TNode>();
            ParentsList = new List<TNode>();
        }

        public IEnumerable<TNode> Children
        {
            get
            {
                return new ReadOnlyCollection<TNode>(ChildrenList);
            }
        }

        public IEnumerable<TNode> Parents
        {
            get
            {
                return new ReadOnlyCollection<TNode>(ParentsList);
            }
        }

        public TValue Value
        {
            get
            {
                return value;
            }
        }

        public void TraverseChildrenFirst(Action<TNode, List<TNode>, int> nodeHandler, int initialLevel)
        {
            foreach(var child in ChildrenList)
            {
                child.TraverseChildrenFirst(nodeHandler, initialLevel + 1);
            }

            nodeHandler((TNode)this, ChildrenList, initialLevel);
        }

        public void TraverseParentFirst(Action<TValue, int> nodeHandler, int initialLevel)
        {
            nodeHandler(value, initialLevel);
            foreach (var child in ChildrenList)
            {
                child.TraverseParentFirst(nodeHandler, initialLevel + 1);
            }
        }

        public TNode TryGetNode(Func<TValue, bool> predicate)
        {
            if(predicate(value))
            {
                return (TNode)this;
            }
            foreach (var child in ChildrenList)
            {
                var node = child.TryGetNode(predicate);
                if (node != null)
                {
                    return node;
                }
            }
            return null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            yield return Value;
            foreach(var child in ChildrenList)
            {
                foreach(var element in child)
                {
                    yield return element;
                }
            }
        }

        public abstract TNode AddChild(TValue value);

        public TNode TryGetNode(TValue valueToFind)
        {
            return TryGetNode(val => valueToFind.Equals(val));
        }

        public virtual void RemoveChild(TValue value)
        {
            var node = ChildrenList.FirstOrDefault(x => value.Equals(x.Value));
            if(node == null)
            {
                throw new InvalidOperationException($"Could not find child '{value}'.");
            }
            ChildrenList.Remove(node);
        }

        protected readonly List<TNode> ParentsList;
        protected readonly List<TNode> ChildrenList;
        private readonly TValue value;
    }
}
