//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using System.IO;
using System.Collections.Generic;
using System;


namespace Antmicro.Renode.Core
{
    public static class PlatformsProvider
    {
        private const string Name = "name";
        private const string ScriptPath = "scriptpath";
        private const string IconResource = "icon";
        private const string HasFlash = "hasflash";
        private const string HasPendrive = "haspendrive";
        private static readonly Platform[] platforms;

        public static bool IsPlatformAvailable(string name)
        {
            return platforms.Any(p => p.Name == name);
        }

        public static Platform GetPlatformByName(string name)
        {
            return platforms.SingleOrDefault(p => p.Name == name);
        }

        public static Platform[] GetAvailablePlatforms()
        {
            return platforms;
        }

        private static Platform[] LoadPlatforms()
        {
            var platformList = new List<Platform>();
            string scriptsPath = Directory.GetCurrentDirectory() + "/platforms/boards";

            foreach(var fileName in Directory.EnumerateFiles(scriptsPath))
            {
                var tagParser = new PropertyTagParser(File.ReadAllLines(fileName));
                var platform = new Platform();
                Tuple<string, string> tag;

                while((tag = tagParser.GetNextTag()) != null)
                {
                    bool val;
                    switch(tag.Item1.ToLower())
                    {
                    case Name:
                        platform.Name = tag.Item2;
                        break;
                    case IconResource:
                        platform.IconResource = tag.Item2;
                        break;
                    case HasPendrive:
                        if(!bool.TryParse(tag.Item2, out val))
                        {
                            break;
                        }
                        platform.HasPendrive = val;
                        break;
                    case HasFlash:
                        if(!bool.TryParse(tag.Item2, out val))
                        {
                            break;
                        }
                        platform.HasFlash = val;
                        break;
                    }
                }

                if(string.IsNullOrEmpty(platform.Name))
                {
                    continue;
                }

                platform.ScriptPath = fileName;
                platformList.Add(platform);
            }

            return platformList.ToArray<Platform>();
        }

        static PlatformsProvider()
        {
            platforms = LoadPlatforms();
        }
    }
}

