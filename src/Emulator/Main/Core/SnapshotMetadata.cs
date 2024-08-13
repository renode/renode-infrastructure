//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Core
{
    public readonly struct SnapshotMetadata
    {
        public SnapshotMetadata(string versionString)
        {
            VersionString = versionString;
            Runner = RuntimeInfo.Version;
        }

        public SnapshotMetadata(string versionString, string runner)
        {
            VersionString = versionString;
            Runner = runner;
        }

        public string VersionString { get; }
        public string Runner { get; }
    }
}