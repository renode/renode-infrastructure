//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Utilities.Binding
{
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64(ActionUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64(FuncUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64(FuncInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64(FuncStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64(FuncUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64(UInt64 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64(FuncIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32(ActionInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32(FuncUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32(FuncInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32(FuncStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32(FuncUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32(Int32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32(FuncIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionString(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionString(ActionString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64String(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64String(FuncUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32String(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32String(FuncInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringString(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringString(FuncStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32String(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32String(FuncUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrString(String param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrString(FuncIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32(ActionUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32(FuncUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32(FuncInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32(FuncStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32(FuncUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32(UInt32 param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32(FuncIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtr(ActionIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtr(FuncUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtr(FuncInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtr(FuncStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtr(FuncUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtr(IntPtr param0);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtr(FuncIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64(ActionUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64(FuncUInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64(FuncInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64(FuncStringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64(FuncUInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64(UInt64 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64(FuncIntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32(ActionUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32(FuncUInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32(FuncInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32(FuncStringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32(FuncUInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32(UInt64 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32(FuncIntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64String(ActionUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64String(FuncUInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64String(FuncInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64String(FuncStringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64String(FuncUInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64String(UInt64 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64String(FuncIntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32(ActionUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32(FuncUInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32(FuncInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32(FuncStringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32(FuncUInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32(UInt64 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32(FuncIntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtr(ActionUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtr(FuncUInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtr(FuncInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtr(FuncStringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtr(FuncUInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtr(UInt64 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtr(FuncIntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64(ActionInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64(FuncUInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64(FuncInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64(FuncStringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64(FuncUInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64(Int32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64(FuncIntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32(ActionInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32(FuncUInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32(FuncInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32(FuncStringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32(FuncUInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32(Int32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32(FuncIntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32String(ActionInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32String(FuncUInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32String(FuncInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32String(FuncStringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32String(FuncUInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32String(Int32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32String(FuncIntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32(ActionInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32(FuncUInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32(FuncInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32(FuncStringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32(FuncUInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32(Int32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32(FuncIntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtr(ActionInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtr(FuncUInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtr(FuncInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtr(FuncStringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtr(FuncUInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtr(Int32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtr(FuncIntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64(ActionStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64(FuncUInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64(FuncInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64(FuncStringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64(FuncUInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64(String param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64(FuncIntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32(ActionStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32(FuncUInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32(FuncInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32(FuncStringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32(FuncUInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32(String param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32(FuncIntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringString(ActionStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringString(FuncUInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringString(FuncInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringString(FuncStringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringString(FuncUInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringString(String param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringString(FuncIntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32(ActionStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32(FuncUInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32(FuncInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32(FuncStringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32(FuncUInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32(String param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32(FuncIntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtr(ActionStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtr(FuncUInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtr(FuncInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtr(FuncStringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtr(FuncUInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtr(String param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtr(FuncIntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64(ActionUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64(FuncUInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64(FuncInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64(FuncStringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64(FuncUInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64(UInt32 param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64(FuncIntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32(ActionUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32(FuncUInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32(FuncInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32(FuncStringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32(FuncUInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32(UInt32 param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32(FuncIntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32String(ActionUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32String(FuncUInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32String(FuncInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32String(FuncStringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32String(FuncUInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32String(UInt32 param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32String(FuncIntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32(ActionUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32(FuncUInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32(FuncInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32(FuncStringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32(FuncUInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32(UInt32 param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32(FuncIntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtr(ActionUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtr(FuncUInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtr(FuncInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtr(FuncStringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtr(FuncUInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtr(UInt32 param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtr(FuncIntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64(ActionIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64(FuncUInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64(FuncInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64(FuncStringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64(FuncUInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64(IntPtr param0, UInt64 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64(FuncIntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32(ActionIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32(FuncUInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32(FuncInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32(FuncStringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32(FuncUInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32(IntPtr param0, Int32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32(FuncIntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrString(ActionIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrString(FuncUInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrString(FuncInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrString(FuncStringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrString(FuncUInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrString(IntPtr param0, String param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrString(FuncIntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32(ActionIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32(FuncUInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32(FuncInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32(FuncStringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32(FuncUInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32(IntPtr param0, UInt32 param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32(FuncIntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtr(ActionIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtr(FuncUInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtr(FuncInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtr(FuncStringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtr(FuncUInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtr(FuncIntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64UInt64(ActionUInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64UInt64(FuncUInt64UInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64UInt64(FuncInt32UInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64UInt64(FuncStringUInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64UInt64(FuncUInt32UInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64UInt64(UInt64 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64UInt64(FuncIntPtrUInt64UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64Int32(ActionUInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64Int32(FuncUInt64UInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64Int32(FuncInt32UInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64Int32(FuncStringUInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64Int32(FuncUInt32UInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64Int32(UInt64 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64Int32(FuncIntPtrUInt64UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64String(ActionUInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64String(FuncUInt64UInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64String(FuncInt32UInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64String(FuncStringUInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64String(FuncUInt32UInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64String(UInt64 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64String(FuncIntPtrUInt64UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64UInt32(ActionUInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64UInt32(FuncUInt64UInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64UInt32(FuncInt32UInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64UInt32(FuncStringUInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64UInt32(FuncUInt32UInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64UInt32(UInt64 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64UInt32(FuncIntPtrUInt64UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt64IntPtr(ActionUInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt64IntPtr(FuncUInt64UInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt64IntPtr(FuncInt32UInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt64IntPtr(FuncStringUInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt64IntPtr(FuncUInt32UInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt64IntPtr(UInt64 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt64IntPtr(FuncIntPtrUInt64UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32UInt64(ActionUInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32UInt64(FuncUInt64UInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32UInt64(FuncInt32UInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32UInt64(FuncStringUInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32UInt64(FuncUInt32UInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32UInt64(UInt64 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32UInt64(FuncIntPtrUInt64Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32Int32(ActionUInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32Int32(FuncUInt64UInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32Int32(FuncInt32UInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32Int32(FuncStringUInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32Int32(FuncUInt32UInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32Int32(UInt64 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32Int32(FuncIntPtrUInt64Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32String(ActionUInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32String(FuncUInt64UInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32String(FuncInt32UInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32String(FuncStringUInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32String(FuncUInt32UInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32String(UInt64 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32String(FuncIntPtrUInt64Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32UInt32(ActionUInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32UInt32(FuncUInt64UInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32UInt32(FuncInt32UInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32UInt32(FuncStringUInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32UInt32(FuncUInt32UInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32UInt32(UInt64 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32UInt32(FuncIntPtrUInt64Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64Int32IntPtr(ActionUInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64Int32IntPtr(FuncUInt64UInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64Int32IntPtr(FuncInt32UInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64Int32IntPtr(FuncStringUInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64Int32IntPtr(FuncUInt32UInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64Int32IntPtr(UInt64 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64Int32IntPtr(FuncIntPtrUInt64Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64StringUInt64(ActionUInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64StringUInt64(FuncUInt64UInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64StringUInt64(FuncInt32UInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64StringUInt64(FuncStringUInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64StringUInt64(FuncUInt32UInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64StringUInt64(UInt64 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64StringUInt64(FuncIntPtrUInt64StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64StringInt32(ActionUInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64StringInt32(FuncUInt64UInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64StringInt32(FuncInt32UInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64StringInt32(FuncStringUInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64StringInt32(FuncUInt32UInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64StringInt32(UInt64 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64StringInt32(FuncIntPtrUInt64StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64StringString(ActionUInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64StringString(FuncUInt64UInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64StringString(FuncInt32UInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64StringString(FuncStringUInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64StringString(FuncUInt32UInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64StringString(UInt64 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64StringString(FuncIntPtrUInt64StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64StringUInt32(ActionUInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64StringUInt32(FuncUInt64UInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64StringUInt32(FuncInt32UInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64StringUInt32(FuncStringUInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64StringUInt32(FuncUInt32UInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64StringUInt32(UInt64 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64StringUInt32(FuncIntPtrUInt64StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64StringIntPtr(ActionUInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64StringIntPtr(FuncUInt64UInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64StringIntPtr(FuncInt32UInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64StringIntPtr(FuncStringUInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64StringIntPtr(FuncUInt32UInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64StringIntPtr(UInt64 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64StringIntPtr(FuncIntPtrUInt64StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32UInt64(ActionUInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32UInt64(FuncUInt64UInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32UInt64(FuncInt32UInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32UInt64(FuncStringUInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32UInt64(FuncUInt32UInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32UInt64(UInt64 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32UInt64(FuncIntPtrUInt64UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32Int32(ActionUInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32Int32(FuncUInt64UInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32Int32(FuncInt32UInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32Int32(FuncStringUInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32Int32(FuncUInt32UInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32Int32(UInt64 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32Int32(FuncIntPtrUInt64UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32String(ActionUInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32String(FuncUInt64UInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32String(FuncInt32UInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32String(FuncStringUInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32String(FuncUInt32UInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32String(UInt64 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32String(FuncIntPtrUInt64UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32UInt32(ActionUInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32UInt32(FuncUInt64UInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32UInt32(FuncInt32UInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32UInt32(FuncStringUInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32UInt32(FuncUInt32UInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32UInt32(UInt64 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32UInt32(FuncIntPtrUInt64UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64UInt32IntPtr(ActionUInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64UInt32IntPtr(FuncUInt64UInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64UInt32IntPtr(FuncInt32UInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64UInt32IntPtr(FuncStringUInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64UInt32IntPtr(FuncUInt32UInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64UInt32IntPtr(UInt64 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64UInt32IntPtr(FuncIntPtrUInt64UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtrUInt64(ActionUInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtrUInt64(FuncUInt64UInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtrUInt64(FuncInt32UInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtrUInt64(FuncStringUInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtrUInt64(FuncUInt32UInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtrUInt64(UInt64 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtrUInt64(FuncIntPtrUInt64IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtrInt32(ActionUInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtrInt32(FuncUInt64UInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtrInt32(FuncInt32UInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtrInt32(FuncStringUInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtrInt32(FuncUInt32UInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtrInt32(UInt64 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtrInt32(FuncIntPtrUInt64IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtrString(ActionUInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtrString(FuncUInt64UInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtrString(FuncInt32UInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtrString(FuncStringUInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtrString(FuncUInt32UInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtrString(UInt64 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtrString(FuncIntPtrUInt64IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtrUInt32(ActionUInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtrUInt32(FuncUInt64UInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtrUInt32(FuncInt32UInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtrUInt32(FuncStringUInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtrUInt32(FuncUInt32UInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtrUInt32(UInt64 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtrUInt32(FuncIntPtrUInt64IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt64IntPtrIntPtr(ActionUInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt64IntPtrIntPtr(FuncUInt64UInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt64IntPtrIntPtr(FuncInt32UInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt64IntPtrIntPtr(FuncStringUInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt64IntPtrIntPtr(FuncUInt32UInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt64IntPtrIntPtr(UInt64 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt64IntPtrIntPtr(FuncIntPtrUInt64IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64UInt64(ActionInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64UInt64(FuncUInt64Int32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64UInt64(FuncInt32Int32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64UInt64(FuncStringInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64UInt64(FuncUInt32Int32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64UInt64(Int32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64UInt64(FuncIntPtrInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64Int32(ActionInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64Int32(FuncUInt64Int32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64Int32(FuncInt32Int32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64Int32(FuncStringInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64Int32(FuncUInt32Int32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64Int32(Int32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64Int32(FuncIntPtrInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64String(ActionInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64String(FuncUInt64Int32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64String(FuncInt32Int32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64String(FuncStringInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64String(FuncUInt32Int32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64String(Int32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64String(FuncIntPtrInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64UInt32(ActionInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64UInt32(FuncUInt64Int32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64UInt32(FuncInt32Int32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64UInt32(FuncStringInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64UInt32(FuncUInt32Int32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64UInt32(Int32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64UInt32(FuncIntPtrInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt64IntPtr(ActionInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt64IntPtr(FuncUInt64Int32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt64IntPtr(FuncInt32Int32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt64IntPtr(FuncStringInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt64IntPtr(FuncUInt32Int32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt64IntPtr(Int32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt64IntPtr(FuncIntPtrInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32UInt64(ActionInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32UInt64(FuncUInt64Int32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32UInt64(FuncInt32Int32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32UInt64(FuncStringInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32UInt64(FuncUInt32Int32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32UInt64(Int32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32UInt64(FuncIntPtrInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32Int32(ActionInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32Int32(FuncUInt64Int32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32Int32(FuncInt32Int32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32Int32(FuncStringInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32Int32(FuncUInt32Int32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32Int32(Int32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32Int32(FuncIntPtrInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32String(ActionInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32String(FuncUInt64Int32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32String(FuncInt32Int32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32String(FuncStringInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32String(FuncUInt32Int32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32String(Int32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32String(FuncIntPtrInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32UInt32(ActionInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32UInt32(FuncUInt64Int32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32UInt32(FuncInt32Int32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32UInt32(FuncStringInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32UInt32(FuncUInt32Int32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32UInt32(Int32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32UInt32(FuncIntPtrInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32Int32IntPtr(ActionInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32Int32IntPtr(FuncUInt64Int32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32Int32IntPtr(FuncInt32Int32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32Int32IntPtr(FuncStringInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32Int32IntPtr(FuncUInt32Int32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32Int32IntPtr(Int32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32Int32IntPtr(FuncIntPtrInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32StringUInt64(ActionInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32StringUInt64(FuncUInt64Int32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32StringUInt64(FuncInt32Int32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32StringUInt64(FuncStringInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32StringUInt64(FuncUInt32Int32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32StringUInt64(Int32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32StringUInt64(FuncIntPtrInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32StringInt32(ActionInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32StringInt32(FuncUInt64Int32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32StringInt32(FuncInt32Int32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32StringInt32(FuncStringInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32StringInt32(FuncUInt32Int32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32StringInt32(Int32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32StringInt32(FuncIntPtrInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32StringString(ActionInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32StringString(FuncUInt64Int32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32StringString(FuncInt32Int32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32StringString(FuncStringInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32StringString(FuncUInt32Int32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32StringString(Int32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32StringString(FuncIntPtrInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32StringUInt32(ActionInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32StringUInt32(FuncUInt64Int32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32StringUInt32(FuncInt32Int32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32StringUInt32(FuncStringInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32StringUInt32(FuncUInt32Int32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32StringUInt32(Int32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32StringUInt32(FuncIntPtrInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32StringIntPtr(ActionInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32StringIntPtr(FuncUInt64Int32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32StringIntPtr(FuncInt32Int32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32StringIntPtr(FuncStringInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32StringIntPtr(FuncUInt32Int32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32StringIntPtr(Int32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32StringIntPtr(FuncIntPtrInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32UInt64(ActionInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32UInt64(FuncUInt64Int32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32UInt64(FuncInt32Int32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32UInt64(FuncStringInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32UInt64(FuncUInt32Int32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32UInt64(Int32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32UInt64(FuncIntPtrInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32Int32(ActionInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32Int32(FuncUInt64Int32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32Int32(FuncInt32Int32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32Int32(FuncStringInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32Int32(FuncUInt32Int32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32Int32(Int32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32Int32(FuncIntPtrInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32String(ActionInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32String(FuncUInt64Int32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32String(FuncInt32Int32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32String(FuncStringInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32String(FuncUInt32Int32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32String(Int32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32String(FuncIntPtrInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32UInt32(ActionInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32UInt32(FuncUInt64Int32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32UInt32(FuncInt32Int32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32UInt32(FuncStringInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32UInt32(FuncUInt32Int32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32UInt32(Int32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32UInt32(FuncIntPtrInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32UInt32IntPtr(ActionInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32UInt32IntPtr(FuncUInt64Int32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32UInt32IntPtr(FuncInt32Int32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32UInt32IntPtr(FuncStringInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32UInt32IntPtr(FuncUInt32Int32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32UInt32IntPtr(Int32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32UInt32IntPtr(FuncIntPtrInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtrUInt64(ActionInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtrUInt64(FuncUInt64Int32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtrUInt64(FuncInt32Int32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtrUInt64(FuncStringInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtrUInt64(FuncUInt32Int32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtrUInt64(Int32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtrUInt64(FuncIntPtrInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtrInt32(ActionInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtrInt32(FuncUInt64Int32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtrInt32(FuncInt32Int32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtrInt32(FuncStringInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtrInt32(FuncUInt32Int32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtrInt32(Int32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtrInt32(FuncIntPtrInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtrString(ActionInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtrString(FuncUInt64Int32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtrString(FuncInt32Int32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtrString(FuncStringInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtrString(FuncUInt32Int32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtrString(Int32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtrString(FuncIntPtrInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtrUInt32(ActionInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtrUInt32(FuncUInt64Int32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtrUInt32(FuncInt32Int32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtrUInt32(FuncStringInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtrUInt32(FuncUInt32Int32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtrUInt32(Int32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtrUInt32(FuncIntPtrInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionInt32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionInt32IntPtrIntPtr(ActionInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64Int32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64Int32IntPtrIntPtr(FuncUInt64Int32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32Int32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32Int32IntPtrIntPtr(FuncInt32Int32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringInt32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringInt32IntPtrIntPtr(FuncStringInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32Int32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32Int32IntPtrIntPtr(FuncUInt32Int32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrInt32IntPtrIntPtr(Int32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrInt32IntPtrIntPtr(FuncIntPtrInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64UInt64(ActionStringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64UInt64(FuncUInt64StringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64UInt64(FuncInt32StringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64UInt64(FuncStringStringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64UInt64(FuncUInt32StringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64UInt64(String param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64UInt64(FuncIntPtrStringUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64Int32(ActionStringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64Int32(FuncUInt64StringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64Int32(FuncInt32StringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64Int32(FuncStringStringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64Int32(FuncUInt32StringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64Int32(String param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64Int32(FuncIntPtrStringUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64String(ActionStringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64String(FuncUInt64StringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64String(FuncInt32StringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64String(FuncStringStringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64String(FuncUInt32StringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64String(String param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64String(FuncIntPtrStringUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64UInt32(ActionStringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64UInt32(FuncUInt64StringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64UInt32(FuncInt32StringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64UInt32(FuncStringStringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64UInt32(FuncUInt32StringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64UInt32(String param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64UInt32(FuncIntPtrStringUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt64IntPtr(ActionStringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt64IntPtr(FuncUInt64StringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt64IntPtr(FuncInt32StringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt64IntPtr(FuncStringStringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt64IntPtr(FuncUInt32StringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt64IntPtr(String param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt64IntPtr(FuncIntPtrStringUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32UInt64(ActionStringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32UInt64(FuncUInt64StringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32UInt64(FuncInt32StringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32UInt64(FuncStringStringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32UInt64(FuncUInt32StringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32UInt64(String param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32UInt64(FuncIntPtrStringInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32Int32(ActionStringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32Int32(FuncUInt64StringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32Int32(FuncInt32StringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32Int32(FuncStringStringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32Int32(FuncUInt32StringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32Int32(String param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32Int32(FuncIntPtrStringInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32String(ActionStringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32String(FuncUInt64StringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32String(FuncInt32StringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32String(FuncStringStringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32String(FuncUInt32StringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32String(String param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32String(FuncIntPtrStringInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32UInt32(ActionStringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32UInt32(FuncUInt64StringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32UInt32(FuncInt32StringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32UInt32(FuncStringStringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32UInt32(FuncUInt32StringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32UInt32(String param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32UInt32(FuncIntPtrStringInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringInt32IntPtr(ActionStringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringInt32IntPtr(FuncUInt64StringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringInt32IntPtr(FuncInt32StringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringInt32IntPtr(FuncStringStringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringInt32IntPtr(FuncUInt32StringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringInt32IntPtr(String param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringInt32IntPtr(FuncIntPtrStringInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringStringUInt64(ActionStringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringStringUInt64(FuncUInt64StringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringStringUInt64(FuncInt32StringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringStringUInt64(FuncStringStringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringStringUInt64(FuncUInt32StringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringStringUInt64(String param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringStringUInt64(FuncIntPtrStringStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringStringInt32(ActionStringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringStringInt32(FuncUInt64StringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringStringInt32(FuncInt32StringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringStringInt32(FuncStringStringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringStringInt32(FuncUInt32StringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringStringInt32(String param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringStringInt32(FuncIntPtrStringStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringStringString(ActionStringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringStringString(FuncUInt64StringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringStringString(FuncInt32StringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringStringString(FuncStringStringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringStringString(FuncUInt32StringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringStringString(String param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringStringString(FuncIntPtrStringStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringStringUInt32(ActionStringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringStringUInt32(FuncUInt64StringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringStringUInt32(FuncInt32StringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringStringUInt32(FuncStringStringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringStringUInt32(FuncUInt32StringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringStringUInt32(String param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringStringUInt32(FuncIntPtrStringStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringStringIntPtr(ActionStringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringStringIntPtr(FuncUInt64StringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringStringIntPtr(FuncInt32StringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringStringIntPtr(FuncStringStringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringStringIntPtr(FuncUInt32StringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringStringIntPtr(String param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringStringIntPtr(FuncIntPtrStringStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32UInt64(ActionStringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32UInt64(FuncUInt64StringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32UInt64(FuncInt32StringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32UInt64(FuncStringStringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32UInt64(FuncUInt32StringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32UInt64(String param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32UInt64(FuncIntPtrStringUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32Int32(ActionStringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32Int32(FuncUInt64StringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32Int32(FuncInt32StringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32Int32(FuncStringStringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32Int32(FuncUInt32StringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32Int32(String param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32Int32(FuncIntPtrStringUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32String(ActionStringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32String(FuncUInt64StringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32String(FuncInt32StringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32String(FuncStringStringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32String(FuncUInt32StringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32String(String param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32String(FuncIntPtrStringUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32UInt32(ActionStringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32UInt32(FuncUInt64StringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32UInt32(FuncInt32StringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32UInt32(FuncStringStringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32UInt32(FuncUInt32StringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32UInt32(String param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32UInt32(FuncIntPtrStringUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringUInt32IntPtr(ActionStringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringUInt32IntPtr(FuncUInt64StringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringUInt32IntPtr(FuncInt32StringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringUInt32IntPtr(FuncStringStringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringUInt32IntPtr(FuncUInt32StringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringUInt32IntPtr(String param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringUInt32IntPtr(FuncIntPtrStringUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtrUInt64(ActionStringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtrUInt64(FuncUInt64StringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtrUInt64(FuncInt32StringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtrUInt64(FuncStringStringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtrUInt64(FuncUInt32StringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtrUInt64(String param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtrUInt64(FuncIntPtrStringIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtrInt32(ActionStringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtrInt32(FuncUInt64StringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtrInt32(FuncInt32StringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtrInt32(FuncStringStringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtrInt32(FuncUInt32StringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtrInt32(String param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtrInt32(FuncIntPtrStringIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtrString(ActionStringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtrString(FuncUInt64StringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtrString(FuncInt32StringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtrString(FuncStringStringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtrString(FuncUInt32StringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtrString(String param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtrString(FuncIntPtrStringIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtrUInt32(ActionStringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtrUInt32(FuncUInt64StringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtrUInt32(FuncInt32StringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtrUInt32(FuncStringStringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtrUInt32(FuncUInt32StringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtrUInt32(String param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtrUInt32(FuncIntPtrStringIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionStringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionStringIntPtrIntPtr(ActionStringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64StringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64StringIntPtrIntPtr(FuncUInt64StringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32StringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32StringIntPtrIntPtr(FuncInt32StringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringStringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringStringIntPtrIntPtr(FuncStringStringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32StringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32StringIntPtrIntPtr(FuncUInt32StringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrStringIntPtrIntPtr(String param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrStringIntPtrIntPtr(FuncIntPtrStringIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64UInt64(ActionUInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64UInt64(FuncUInt64UInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64UInt64(FuncInt32UInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64UInt64(FuncStringUInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64UInt64(FuncUInt32UInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64UInt64(UInt32 param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64UInt64(FuncIntPtrUInt32UInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64Int32(ActionUInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64Int32(FuncUInt64UInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64Int32(FuncInt32UInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64Int32(FuncStringUInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64Int32(FuncUInt32UInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64Int32(UInt32 param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64Int32(FuncIntPtrUInt32UInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64String(ActionUInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64String(FuncUInt64UInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64String(FuncInt32UInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64String(FuncStringUInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64String(FuncUInt32UInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64String(UInt32 param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64String(FuncIntPtrUInt32UInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64UInt32(ActionUInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64UInt32(FuncUInt64UInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64UInt32(FuncInt32UInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64UInt32(FuncStringUInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64UInt32(FuncUInt32UInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64UInt32(UInt32 param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64UInt32(FuncIntPtrUInt32UInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt64IntPtr(ActionUInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt64IntPtr(FuncUInt64UInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt64IntPtr(FuncInt32UInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt64IntPtr(FuncStringUInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt64IntPtr(FuncUInt32UInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt64IntPtr(UInt32 param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt64IntPtr(FuncIntPtrUInt32UInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32UInt64(ActionUInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32UInt64(FuncUInt64UInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32UInt64(FuncInt32UInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32UInt64(FuncStringUInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32UInt64(FuncUInt32UInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32UInt64(UInt32 param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32UInt64(FuncIntPtrUInt32Int32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32Int32(ActionUInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32Int32(FuncUInt64UInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32Int32(FuncInt32UInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32Int32(FuncStringUInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32Int32(FuncUInt32UInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32Int32(UInt32 param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32Int32(FuncIntPtrUInt32Int32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32String(ActionUInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32String(FuncUInt64UInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32String(FuncInt32UInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32String(FuncStringUInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32String(FuncUInt32UInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32String(UInt32 param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32String(FuncIntPtrUInt32Int32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32UInt32(ActionUInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32UInt32(FuncUInt64UInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32UInt32(FuncInt32UInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32UInt32(FuncStringUInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32UInt32(FuncUInt32UInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32UInt32(UInt32 param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32UInt32(FuncIntPtrUInt32Int32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32Int32IntPtr(ActionUInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32Int32IntPtr(FuncUInt64UInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32Int32IntPtr(FuncInt32UInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32Int32IntPtr(FuncStringUInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32Int32IntPtr(FuncUInt32UInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32Int32IntPtr(UInt32 param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32Int32IntPtr(FuncIntPtrUInt32Int32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32StringUInt64(ActionUInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32StringUInt64(FuncUInt64UInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32StringUInt64(FuncInt32UInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32StringUInt64(FuncStringUInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32StringUInt64(FuncUInt32UInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32StringUInt64(UInt32 param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32StringUInt64(FuncIntPtrUInt32StringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32StringInt32(ActionUInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32StringInt32(FuncUInt64UInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32StringInt32(FuncInt32UInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32StringInt32(FuncStringUInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32StringInt32(FuncUInt32UInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32StringInt32(UInt32 param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32StringInt32(FuncIntPtrUInt32StringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32StringString(ActionUInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32StringString(FuncUInt64UInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32StringString(FuncInt32UInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32StringString(FuncStringUInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32StringString(FuncUInt32UInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32StringString(UInt32 param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32StringString(FuncIntPtrUInt32StringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32StringUInt32(ActionUInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32StringUInt32(FuncUInt64UInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32StringUInt32(FuncInt32UInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32StringUInt32(FuncStringUInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32StringUInt32(FuncUInt32UInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32StringUInt32(UInt32 param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32StringUInt32(FuncIntPtrUInt32StringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32StringIntPtr(ActionUInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32StringIntPtr(FuncUInt64UInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32StringIntPtr(FuncInt32UInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32StringIntPtr(FuncStringUInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32StringIntPtr(FuncUInt32UInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32StringIntPtr(UInt32 param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32StringIntPtr(FuncIntPtrUInt32StringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32UInt64(ActionUInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32UInt64(FuncUInt64UInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32UInt64(FuncInt32UInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32UInt64(FuncStringUInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32UInt64(FuncUInt32UInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32UInt64(UInt32 param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32UInt64(FuncIntPtrUInt32UInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32Int32(ActionUInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32Int32(FuncUInt64UInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32Int32(FuncInt32UInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32Int32(FuncStringUInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32Int32(FuncUInt32UInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32Int32(UInt32 param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32Int32(FuncIntPtrUInt32UInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32String(ActionUInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32String(FuncUInt64UInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32String(FuncInt32UInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32String(FuncStringUInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32String(FuncUInt32UInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32String(UInt32 param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32String(FuncIntPtrUInt32UInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32UInt32(ActionUInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32UInt32(FuncUInt64UInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32UInt32(FuncInt32UInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32UInt32(FuncStringUInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32UInt32(FuncUInt32UInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32UInt32(UInt32 param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32UInt32(FuncIntPtrUInt32UInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32UInt32IntPtr(ActionUInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32UInt32IntPtr(FuncUInt64UInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32UInt32IntPtr(FuncInt32UInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32UInt32IntPtr(FuncStringUInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32UInt32IntPtr(FuncUInt32UInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32UInt32IntPtr(UInt32 param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32UInt32IntPtr(FuncIntPtrUInt32UInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtrUInt64(ActionUInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtrUInt64(FuncUInt64UInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtrUInt64(FuncInt32UInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtrUInt64(FuncStringUInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtrUInt64(FuncUInt32UInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtrUInt64(UInt32 param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtrUInt64(FuncIntPtrUInt32IntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtrInt32(ActionUInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtrInt32(FuncUInt64UInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtrInt32(FuncInt32UInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtrInt32(FuncStringUInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtrInt32(FuncUInt32UInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtrInt32(UInt32 param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtrInt32(FuncIntPtrUInt32IntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtrString(ActionUInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtrString(FuncUInt64UInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtrString(FuncInt32UInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtrString(FuncStringUInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtrString(FuncUInt32UInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtrString(UInt32 param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtrString(FuncIntPtrUInt32IntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtrUInt32(ActionUInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtrUInt32(FuncUInt64UInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtrUInt32(FuncInt32UInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtrUInt32(FuncStringUInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtrUInt32(FuncUInt32UInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtrUInt32(UInt32 param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtrUInt32(FuncIntPtrUInt32IntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionUInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionUInt32IntPtrIntPtr(ActionUInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64UInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64UInt32IntPtrIntPtr(FuncUInt64UInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32UInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32UInt32IntPtrIntPtr(FuncInt32UInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringUInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringUInt32IntPtrIntPtr(FuncStringUInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32UInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32UInt32IntPtrIntPtr(FuncUInt32UInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrUInt32IntPtrIntPtr(UInt32 param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrUInt32IntPtrIntPtr(FuncIntPtrUInt32IntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64UInt64(ActionIntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64UInt64(FuncUInt64IntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64UInt64(FuncInt32IntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64UInt64(FuncStringIntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64UInt64(FuncUInt32IntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64UInt64(IntPtr param0, UInt64 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64UInt64(FuncIntPtrIntPtrUInt64UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64Int32(ActionIntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64Int32(FuncUInt64IntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64Int32(FuncInt32IntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64Int32(FuncStringIntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64Int32(FuncUInt32IntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64Int32(IntPtr param0, UInt64 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64Int32(FuncIntPtrIntPtrUInt64Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64String(ActionIntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64String(FuncUInt64IntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64String(FuncInt32IntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64String(FuncStringIntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64String(FuncUInt32IntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64String(IntPtr param0, UInt64 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64String(FuncIntPtrIntPtrUInt64String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64UInt32(ActionIntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64UInt32(FuncUInt64IntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64UInt32(FuncInt32IntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64UInt32(FuncStringIntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64UInt32(FuncUInt32IntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64UInt32(IntPtr param0, UInt64 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64UInt32(FuncIntPtrIntPtrUInt64UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt64IntPtr(ActionIntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt64IntPtr(FuncUInt64IntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt64IntPtr(FuncInt32IntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt64IntPtr(FuncStringIntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt64IntPtr(FuncUInt32IntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt64IntPtr(IntPtr param0, UInt64 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt64IntPtr(FuncIntPtrIntPtrUInt64IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32UInt64(ActionIntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32UInt64(FuncUInt64IntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32UInt64(FuncInt32IntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32UInt64(FuncStringIntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32UInt64(FuncUInt32IntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32UInt64(IntPtr param0, Int32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32UInt64(FuncIntPtrIntPtrInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32Int32(ActionIntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32Int32(FuncUInt64IntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32Int32(FuncInt32IntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32Int32(FuncStringIntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32Int32(FuncUInt32IntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32Int32(IntPtr param0, Int32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32Int32(FuncIntPtrIntPtrInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32String(ActionIntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32String(FuncUInt64IntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32String(FuncInt32IntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32String(FuncStringIntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32String(FuncUInt32IntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32String(IntPtr param0, Int32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32String(FuncIntPtrIntPtrInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32UInt32(ActionIntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32UInt32(FuncUInt64IntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32UInt32(FuncInt32IntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32UInt32(FuncStringIntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32UInt32(FuncUInt32IntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32UInt32(IntPtr param0, Int32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32UInt32(FuncIntPtrIntPtrInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrInt32IntPtr(ActionIntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrInt32IntPtr(FuncUInt64IntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrInt32IntPtr(FuncInt32IntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrInt32IntPtr(FuncStringIntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrInt32IntPtr(FuncUInt32IntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrInt32IntPtr(IntPtr param0, Int32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrInt32IntPtr(FuncIntPtrIntPtrInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrStringUInt64(ActionIntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrStringUInt64(FuncUInt64IntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrStringUInt64(FuncInt32IntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrStringUInt64(FuncStringIntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrStringUInt64(FuncUInt32IntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrStringUInt64(IntPtr param0, String param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrStringUInt64(FuncIntPtrIntPtrStringUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrStringInt32(ActionIntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrStringInt32(FuncUInt64IntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrStringInt32(FuncInt32IntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrStringInt32(FuncStringIntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrStringInt32(FuncUInt32IntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrStringInt32(IntPtr param0, String param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrStringInt32(FuncIntPtrIntPtrStringInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrStringString(ActionIntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrStringString(FuncUInt64IntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrStringString(FuncInt32IntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrStringString(FuncStringIntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrStringString(FuncUInt32IntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrStringString(IntPtr param0, String param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrStringString(FuncIntPtrIntPtrStringString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrStringUInt32(ActionIntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrStringUInt32(FuncUInt64IntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrStringUInt32(FuncInt32IntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrStringUInt32(FuncStringIntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrStringUInt32(FuncUInt32IntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrStringUInt32(IntPtr param0, String param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrStringUInt32(FuncIntPtrIntPtrStringUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrStringIntPtr(ActionIntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrStringIntPtr(FuncUInt64IntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrStringIntPtr(FuncInt32IntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrStringIntPtr(FuncStringIntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrStringIntPtr(FuncUInt32IntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrStringIntPtr(IntPtr param0, String param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrStringIntPtr(FuncIntPtrIntPtrStringIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32UInt64(ActionIntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32UInt64(FuncUInt64IntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32UInt64(FuncInt32IntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32UInt64(FuncStringIntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32UInt64(FuncUInt32IntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32UInt64(IntPtr param0, UInt32 param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32UInt64(FuncIntPtrIntPtrUInt32UInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32Int32(ActionIntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32Int32(FuncUInt64IntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32Int32(FuncInt32IntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32Int32(FuncStringIntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32Int32(FuncUInt32IntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32Int32(IntPtr param0, UInt32 param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32Int32(FuncIntPtrIntPtrUInt32Int32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32String(ActionIntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32String(FuncUInt64IntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32String(FuncInt32IntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32String(FuncStringIntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32String(FuncUInt32IntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32String(IntPtr param0, UInt32 param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32String(FuncIntPtrIntPtrUInt32String param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32UInt32(ActionIntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32UInt32(FuncUInt64IntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32UInt32(FuncInt32IntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32UInt32(FuncStringIntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32UInt32(FuncUInt32IntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32UInt32(IntPtr param0, UInt32 param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32UInt32(FuncIntPtrIntPtrUInt32UInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrUInt32IntPtr(ActionIntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrUInt32IntPtr(FuncUInt64IntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrUInt32IntPtr(FuncInt32IntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrUInt32IntPtr(FuncStringIntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrUInt32IntPtr(FuncUInt32IntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrUInt32IntPtr(IntPtr param0, UInt32 param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrUInt32IntPtr(FuncIntPtrIntPtrUInt32IntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtrUInt64(ActionIntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtrUInt64(FuncUInt64IntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtrUInt64(FuncInt32IntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtrUInt64(FuncStringIntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtrUInt64(FuncUInt32IntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtrUInt64(IntPtr param0, IntPtr param1, UInt64 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtrUInt64(FuncIntPtrIntPtrIntPtrUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtrInt32(ActionIntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtrInt32(FuncUInt64IntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtrInt32(FuncInt32IntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtrInt32(FuncStringIntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtrInt32(FuncUInt32IntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtrInt32(IntPtr param0, IntPtr param1, Int32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtrInt32(FuncIntPtrIntPtrIntPtrInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtrString(ActionIntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtrString(FuncUInt64IntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtrString(FuncInt32IntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtrString(FuncStringIntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtrString(FuncUInt32IntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtrString(IntPtr param0, IntPtr param1, String param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtrString(FuncIntPtrIntPtrIntPtrString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtrUInt32(ActionIntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtrUInt32(FuncUInt64IntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtrUInt32(FuncInt32IntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtrUInt32(FuncStringIntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtrUInt32(FuncUInt32IntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtrUInt32(IntPtr param0, IntPtr param1, UInt32 param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtrUInt32(FuncIntPtrIntPtrIntPtrUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void ActionIntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachActionIntPtrIntPtrIntPtr(ActionIntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64IntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64IntPtrIntPtrIntPtr(FuncUInt64IntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32IntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32IntPtrIntPtrIntPtr(FuncInt32IntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncStringIntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncStringIntPtrIntPtrIntPtr(FuncStringIntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32IntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32IntPtrIntPtrIntPtr(FuncUInt32IntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtrIntPtrIntPtrIntPtr(IntPtr param0, IntPtr param1, IntPtr param2);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtrIntPtrIntPtrIntPtr(FuncIntPtrIntPtrIntPtrIntPtr param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachAction(System.Action param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt64 FuncUInt64();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt64(FuncUInt64 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate Int32 FuncInt32();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncInt32(FuncInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate String FuncString();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncString(FuncString param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate UInt32 FuncUInt32();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncUInt32(FuncUInt32 param);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate IntPtr FuncIntPtr();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AttachFuncIntPtr(FuncIntPtr param);

}
