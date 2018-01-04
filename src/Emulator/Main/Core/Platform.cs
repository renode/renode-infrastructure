//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Core
{
    public class Platform
    {
        public Platform()
        {
            HasMemory = true;
        }       

        public override string ToString()
        {
            return Name;
        }

        public string Name        { get; set; }
        public string ScriptPath  { get; set; }
        public bool   HasFlash    { get; set; }
        public bool   HasPendrive { get; set; }
        public bool   HasMemory   { get; set; }
        public string IconResource { get; set; }
    }
}

