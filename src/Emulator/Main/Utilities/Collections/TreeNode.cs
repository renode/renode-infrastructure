//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//


namespace Antmicro.Renode.Utilities.Collections
{
    public class TreeNode<T> : TreeBase<TreeNode<T>, T>
    {
        public TreeNode(T value) : base(value)
        {

        }

        public override TreeNode<T> AddChild(T value)
        {
            var node = new TreeNode<T>(value);
            ChildrenList.Add(node);
            return node;
        }
    }
}

