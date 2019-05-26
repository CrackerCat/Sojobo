﻿namespace ES.Sojobo

open System
open System.Collections.Generic
open System.Text
open System.Runtime.InteropServices
open System.Reflection.PortableExecutable
open System.IO
open ES.Sojobo.Model

(*
    The following structures follow the definition provided by MS.
    As general rule, reserved fields are not valorized.
    Serialization info:
        - Class type are serialized as pointer to anothe memory region
        - Add "Struct" attribute if the class must be serialized as struct and not as a pointer
        - For array type always add the "MarshalAs" with "SizeConst" property in order to know how many items must be serialized
*)
module Win32 =
    
    // https://docs.microsoft.com/en-us/windows/desktop/api/subauth/ns-subauth-_unicode_string
    [<CLIMutable>]
    [<Struct>]
    [<StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)>]
    type UNICODE_STRING = {
        Length: UInt16
        MaximumLength: UInt16
        Buffer: UInt32
    }
    
    // https://www.aldeid.com/wiki/LDR_DATA_TABLE_ENTRY 
    // https://docs.microsoft.com/en-us/windows/desktop/api/winternl/ns-winternl-_peb_ldr_data    
    [<CLIMutable>]
    [<ReferenceEquality>]
    [<StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)>]
    type LDR_DATA_TABLE_ENTRY = {
        mutable InLoadOrderLinksForward: LDR_DATA_TABLE_ENTRY
        mutable InLoadOrderLinksBackward: LDR_DATA_TABLE_ENTRY
        mutable InMemoryOrderLinksForward: LDR_DATA_TABLE_ENTRY
        mutable InMemoryOrderLinksBackward: LDR_DATA_TABLE_ENTRY
        mutable InInitializationOrderLinksForward: LDR_DATA_TABLE_ENTRY
        mutable InInitializationOrderLinksBackward: LDR_DATA_TABLE_ENTRY
        DllBase: UInt32
        EntryPoint: UInt32
        Reserved3: UInt32
        FullDllName: UNICODE_STRING
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)>]
        Reserved4: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)>]
        Reserved5: UInt32 array        
        Reserved6: UInt32
        TimeDateStamp: UInt32
    }

    // https://cheesehack.tistory.com/99
    // https://www.aldeid.com/wiki/PEB_LDR_DATA
    // https://docs.microsoft.com/en-us/windows/desktop/api/winternl/ns-winternl-_peb_ldr_data
    [<CLIMutable>]
    [<ReferenceEquality>]
    [<StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)>]
    type PEB_LDR_DATA = {
        Length: UInt32
        Initialized: UInt32
        SsHandle: UInt32
        InLoadOrderModuleList: LDR_DATA_TABLE_ENTRY
        InMemoryOrderModuleList: LDR_DATA_TABLE_ENTRY
        InInitializationOrderModuleList: LDR_DATA_TABLE_ENTRY
    }

    // https://docs.microsoft.com/en-us/windows/desktop/api/winternl/ns-winternl-peb
    // https://www.nirsoft.net/kernel_struct/vista/PEB.html
    // https://www.aldeid.com/wiki/PEB-Process-Environment-Block
    [<CLIMutable>]
    [<ReferenceEquality>]
    [<StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)>]
    type PEB32 = {
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)>]
        Reserved1: Byte array
        BeingDebugged: Byte
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)>]
        Reserved2: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)>]
        Reserved3: UInt32 array
        Ldr: PEB_LDR_DATA
        ProcessParameters: UInt32
        SubSystemData: UInt32
        ProcessHeap: UInt32
        FastPebLock: UInt32
        AtlThunkSListPtr: UInt32
        Reserved5: UInt32
        Reserved6: UInt32
        Reserved7: UInt32
        Reserved8: UInt32
        AtlThunkSListPtr32: UInt32
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 45)>]
        Reserved9: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)>]
        Reserved10: Byte array
        PostProcessInitRoutine: UInt32
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)>]
        Reserved11: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)>]
        Reserved12: Byte array
        SessionId: UInt32
    } 
    
    // https://docs.microsoft.com/en-us/windows/desktop/api/winternl/ns-winternl-_teb
    // https://www.nirsoft.net/kernel_struct/vista/TEB.html
    [<CLIMutable>]
    [<ReferenceEquality>]
    [<StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)>]
    type TEB32 = {
        // TIB
        ExceptionList: UInt32
        StackBase: UInt32
        StackLimit: UInt32
        SubSystemTib: UInt32
        Version: UInt32
        ArbitraryUserPointer: UInt32
        Self: UInt32

        // TEB
        EnvironmentPointer: UInt32
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)>]
        ClientId: UInt32 array
        ActiveRpcHandle: UInt32
        ThreadLocalStoragePointer: UInt32
        ProcessEnvironmentBlock: PEB32
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 399)>]
        Reserved2: UInt32 array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1952)>]
        Reserved3: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)>]
        TlsSlots: UInt32 array        
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)>]
        Reserved4: Byte array
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)>]
        Reserved5: UInt32 array
        ReservedForOle: UInt32
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)>]
        Reserved6: UInt32 array
        TlsExpansionSlots: UInt32
    }
    
    let private getLibraryMap(sandbox: BaseSandbox) = dict <| [
        for lib in sandbox.Libraries do
            match lib with
            | Native lib when lib.Filename.IsSome -> 
                yield (Path.GetFileName <| lib.Filename.Value.ToLowerInvariant(), lib)
            | _ -> ()
    ]
    
    let private createPeb(sandbox: BaseSandbox) =
        let proc = sandbox.GetRunningProcess()
        let librariesDetails = getLibraryMap(sandbox)
        let dataEntries = new List<LDR_DATA_TABLE_ENTRY>()
                        
        // create the data table entries
        proc.GetImportedFunctions()            
        |> Seq.groupBy(fun s -> s.LibraryName)
        |> Seq.map(fun (libraryName, _) -> libraryName)
        |> Seq.iter(fun libraryName ->
            // get details to insert into PEB
            let (imageBase, entryPoint) =
                match librariesDetails.TryGetValue(libraryName.ToLowerInvariant()) with
                | (true, lib) -> (uint32 lib.BaseAddress, uint32 lib.EntryPoint)
                | (false, _) -> (0u, 0u)

            // fill UnicodeString
            let fullNameBytes = Encoding.Unicode.GetBytes(libraryName)
            let fullNameDll = 
                {Activator.CreateInstance<UNICODE_STRING>() with 
                    Length = uint16 fullNameBytes.Length
                    MaximumLength = uint16 fullNameBytes.Length   
                    Buffer = proc.Memory.AllocateMemory(fullNameBytes, MemoryProtection.Read) |> uint32
                }
                
            // create Data Table Entry
            let dataTableEntry =
                {Activator.CreateInstance<LDR_DATA_TABLE_ENTRY>() with
                    FullDllName = fullNameDll
                    DllBase = imageBase
                    EntryPoint = entryPoint
                }

            // set link to refer to itself
            dataTableEntry.InInitializationOrderLinksForward <- dataTableEntry
            dataTableEntry.InInitializationOrderLinksBackward <- dataTableEntry
            dataTableEntry.InMemoryOrderLinksForward <- dataTableEntry
            dataTableEntry.InMemoryOrderLinksBackward <- dataTableEntry
            dataTableEntry.InLoadOrderLinksForward <- dataTableEntry
            dataTableEntry.InLoadOrderLinksBackward <- dataTableEntry

            dataEntries.Add(dataTableEntry)
        )        

        // connect the link among them
        dataEntries        
        |> Seq.iteri(fun index entry -> 
            let fIndex = (index + 1) % dataEntries.Count
            let fEntry = dataEntries.[fIndex]
            
            let bIndex = if index = 0 then dataEntries.Count - 1 else (index - 1) % dataEntries.Count
            let bEntry = dataEntries.[bIndex]
           
            // set connection            
            entry.InInitializationOrderLinksForward <- fEntry
            entry.InInitializationOrderLinksBackward <- bEntry
            entry.InMemoryOrderLinksForward <- fEntry
            entry.InMemoryOrderLinksBackward <- bEntry
            entry.InLoadOrderLinksForward <- fEntry
            entry.InLoadOrderLinksBackward <- bEntry                    
        )
        
        // finally create the PEB
        {Activator.CreateInstance<PEB32>() with 
            Reserved1 = Array.zeroCreate<Byte>(2)
            BeingDebugged = 0uy
            Reserved2 = Array.zeroCreate<Byte>(1)
            Reserved3 = Array.zeroCreate<UInt32>(2)            
            Ldr = 
                {Activator.CreateInstance<PEB_LDR_DATA>() with    
                    InLoadOrderModuleList = Seq.head dataEntries
                    InMemoryOrderModuleList = Seq.head dataEntries
                    InInitializationOrderModuleList = Seq.head dataEntries
                }
            ProcessParameters = 0u
            SubSystemData = 0u
            ProcessHeap = uint32 proc.Memory.Heap.BaseAddress
            FastPebLock = 0u
            AtlThunkSListPtr = 0u
            Reserved5 = 0u
            Reserved6 = 0u
            Reserved7 = 0u
            Reserved8 = 0u
            AtlThunkSListPtr32 = 0u
            Reserved9 = Array.zeroCreate<Byte>(45)
            Reserved10 = Array.zeroCreate<Byte>(96)
            PostProcessInitRoutine = 0u
            Reserved11 = Array.zeroCreate<Byte>(128)
            Reserved12 = Array.zeroCreate<Byte>(1)
            SessionId = 0u
        }

    let createTeb(sandbox: BaseSandbox) =
        let proc = sandbox.GetRunningProcess()

        // create the TEB
        let teb =
            {Activator.CreateInstance<TEB32>() with
                StackBase = uint32 proc.Memory.Stack.BaseAddress + uint32 proc.Memory.Stack.Content.Length
                StackLimit = uint32 proc.Memory.Stack.BaseAddress
                Self = 0x7ff70000u
                ProcessEnvironmentBlock = createPeb(sandbox)
            }

        // for TEB I have to specify the base address
        let tebRegion = createMemoryRegion(uint64 teb.Self, Marshal.SizeOf<TEB32>(), MemoryProtection.Read)
        proc.Memory.AddMemoryRegion(tebRegion)
        proc.Memory.WriteMemory(uint64 teb.Self, teb)
        tebRegion.BaseAddress