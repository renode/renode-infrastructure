Trace Based Model expects unified instruction format for all supported architectures. 
The format is defined in `instruction.fbs` file written in FlatBuffers schema language. 
FlatBuffers compiler (`flatc`) is used to generate C# code for reading and writing the instruction format. 
FlatBuffers project is available at repository: https://github.com/google/flatbuffers.

'Instruction.cs' and `Instructions.cs` in this directory are auto-generated from FlatBuffers schema file `instruction.fbs`:

```
flatc --csharp instruction.fbs
```