//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Antmicro.Renode.Utilities.Packets
{
    public class DynamicPropertiesObject : DynamicObject
    {
        public DynamicPropertiesObject()
        {
            fields = new Dictionary<string, object>();
            setters = new Dictionary<string, Action<object>>();
            getters = new Dictionary<string, Func<object>>();
        }

        public void ProvideProperty(string name, Action<object> setter = null, Func<object> getter = null)
        {
            if(setter != null)
            {
                setters[name] = setter;
            }
            if(getter != null)
            {
                getters[name] = getter;
            }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if(setters.TryGetValue(binder.Name, out var setter))
            {
                setter(value);
                return true;
            }

            fields[binder.Name] = value;
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {

            if(getters.TryGetValue(binder.Name, out var getter))
            {
                result = getter();
                return true;
            }

            if(!fields.TryGetValue(binder.Name, out result))
            {
                return base.TryGetMember(binder, out result);
            }

            return true;
        }

        private readonly Dictionary<string, object> fields;

        private readonly Dictionary<string, Func<object>> getters;
        private readonly Dictionary<string, Action<object>> setters;
    }
}