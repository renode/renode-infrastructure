//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Reflection;

namespace Antmicro.Renode.Core
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SupportedRIDAttribute : Attribute
    {
        public SupportedRIDAttribute(string rid)
        {
            RID = rid;
        }

        public string RID;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UnsupportedRIDAttribute : Attribute
    {
        public UnsupportedRIDAttribute(string rid)
        {
            RID = rid;
        }

        public string RID;
    }

    public static class TypeRIDCheckExtensions
    {
        public static bool IsRIDSupported(this MemberInfo me)
        {
            var passedSupported = false;
            var noAttribute = true;
            foreach(var attr in me.GetCustomAttributes(false))
            {
                if(attr is SupportedRIDAttribute supportedAttr)
                {
                    noAttribute = false;
                    passedSupported = passedSupported || RuntimeInfo.RIDMatches(supportedAttr.RID);
                }
                else if(attr is UnsupportedRIDAttribute unsupportedAttr)
                {
                    if(RuntimeInfo.RIDMatches(unsupportedAttr.RID))
                    {
                        return false;
                    }
                }
            }
            return noAttribute || passedSupported;
        }
    }
}
