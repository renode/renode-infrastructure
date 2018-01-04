//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace Antmicro.Renode.Utilities
{
	public class MonitorInfo
	{
		public IEnumerable<MethodInfo> Methods{get;set;}
		
		public IEnumerable<PropertyInfo> Properties{get;set;}
		
        public IEnumerable<PropertyInfo> Indexers{ get; set; }

		public IEnumerable<FieldInfo> Fields{get;set;}


	    public IEnumerable<String> AllNames
	    {
	        get
	        {
	            var names = new List<String>();
	            if (Methods != null)
	            {
	                names.AddRange(Methods.Select(x => x.Name));
	            }
	            if (Properties != null)
	            {
	                names.AddRange(Properties.Select(x => x.Name));
	            }
	            if (Fields != null)
	            {
	                names.AddRange(Fields.Select(x => x.Name));
	            }
                if(Indexers != null)
                {
                    names.AddRange(Indexers.Select(x => x.Name));
                }
	            return names;
	        }
	    }
	}
}

