Current Perfetto intergration uses protobuf-net to handle protocol buffers
Repository: https://github.com/protobuf-net/protobuf-net

We use version 2.4.7, because mono doesn't support features required by versions 3.x

Protobuf message definitions are created form the 'trace_packet.proto' file. This file is a
stripped-down version of the Perfetto message definition (original can be found here:
https://cs.android.com/android/platform/superproject/+/f5c4cacbe57e1ab7c5dfed53852ad67d9352b1ed:external/perfetto/protos/perfetto/trace/trace_packet.proto)
that includes only essential features that are required for the Renode intergration.

When using protobuf-net this file can be converted to a C# class using: https://protogen.marcgravell.com/.
The only required options is setting the C# version to 6 or lower as mono has some problems (eg.
lack of support for the default literal) with version 7.1.
The 'TracePacket.cs' file was created using this converter and this class is the only one that is required
to use the Perfetto intergration ('trace_packet.proto' can be used to regenerate the C# class if the 
message definition where to change)

'PerfettoTraceWriter.cs' is a helper class that wraps the packet creation in an easier API
