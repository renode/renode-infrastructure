//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Utilities;
using System.IO;
using System.Linq;
using Antmicro.Renode.Config.Devices;
using Antmicro.Renode.Core;
using System.Collections.Generic;

namespace Antmicro.Renode.SystemTests
{
    [TestFixture]
    public class JsonLoadingTest
    {
        [Test, TestCaseSource("GetJsons")]
        public void LoadAllJsons(string json)
        {
            using(var machine = new Machine())
            {
                new DevicesConfig(ReadFileContents(json), machine);
            }
        }

        private string ReadFileContents(string filename)
        {
            if(!File.Exists(filename))
            {
                throw new ArgumentException(string.Format(
                    "Cannot load devices configuration from file {0} as it does not exist.",
                    filename
                )
                );
            }
            
            string text = "";
            using(TextReader tr = File.OpenText(filename))
            {
                text = tr.ReadToEnd();
            }
	    return text;
        }

        private static IEnumerable<string> GetJsons()
        {
            if(!Misc.TryGetRootDirectory(out var rootDirectory))
            {
                throw new ArgumentException("Couldn't get root directory.");
            }
            TypeManager.Instance.Scan(rootDirectory);

            return Directory.GetFiles(rootDirectory, "*.json", SearchOption.AllDirectories).Where(x => x.Contains(Path.Combine("platforms", "cpus")));
        }
    }
}

