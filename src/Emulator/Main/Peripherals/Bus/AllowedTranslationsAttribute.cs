//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AllowedTranslationsAttribute : Attribute
    {
        public AllowedTranslationsAttribute(AllowedTranslation allowedTranslations)
        {
            this.allowedTranslations = allowedTranslations;
        }

        public AllowedTranslation AllowedTranslations
        {
            get
            {
                return allowedTranslations;
            }
        }

        private readonly AllowedTranslation allowedTranslations;
    }
}

