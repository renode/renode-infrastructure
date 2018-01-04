//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Core
{
    [AttributeUsage(AttributeTargets.Class)]
	public class GPIOAttribute : Attribute
	{
		/// <summary>
		/// Specifies number of GPIO inputs. If it is 0 (default), the number of inputs is unbound.
		/// </summary>
		public int NumberOfInputs
		{
			get;
			set;
		}
		
		/// <summary>
		/// Specifies number of GPIO outputs. If it is 0 (default), the number of outputs is unbound.
		/// </summary>
		public int NumberOfOutputs
		{
			get;
			set;
		}
		
	}
}

