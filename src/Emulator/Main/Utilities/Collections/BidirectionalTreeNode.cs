//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Utilities.Collections
{
    public class BidirectionalTreeNode<T> : TreeBase<BidirectionalTreeNode<T>, T>
    {
        public BidirectionalTreeNode(T value) : base(value)
        {
            // parent will be null - like for the root node
        }

        private BidirectionalTreeNode(T value, BidirectionalTreeNode<T> parent) : base(value)
        {
            Parent = parent;
        }

        public override BidirectionalTreeNode<T> AddChild(T value)
        {
            var node = new BidirectionalTreeNode<T>(value, this);
            ChildrenList.Add(node);
            return node;
        }

        public BidirectionalTreeNode<T> Parent { get; private set; }
    }
}

