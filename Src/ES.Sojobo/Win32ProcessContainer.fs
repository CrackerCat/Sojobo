﻿namespace ES.Sojobo

open System
open System.Collections.Generic
open B2R2
open B2R2.FrontEnd
open B2R2.BinFile
open ES.Sojobo.Model
open B2R2.FrontEnd.Intel

type Win32ProcessContainer() =    
    let _va = new Dictionary<UInt64, MemoryRegion>()    
    let _tempVariables = new Dictionary<String, EmulatedValue>()
    let _variables = new Dictionary<String, EmulatedValue>()
    let mutable _activeRegion: MemoryRegion option = None
    
    let addRegion(memRegion: MemoryRegion) =        
        _va.[memRegion.BaseAddress] <- memRegion

    let getMemoryRegion(programCounter: UInt64) =
        _va.Values
        |> Seq.find(fun memRegion -> 
            let startAddr = memRegion.BaseAddress
            let endAddr = memRegion.BaseAddress + uint64 memRegion.Content.Length
            programCounter >= startAddr && programCounter <= endAddr
        )

    let writeMemory(address: UInt64, value: Byte array) =
        // copy the memory
        let region = getMemoryRegion(address)
        let offset = region.Handler.FileInfo.TranslateAddress address
        Array.Copy(value, 0, region.Content, offset, value.Length)
        
        // create a new handler and modify region
        let newHandler = BinHandler.UpdateCode region.Handler region.BaseAddress region.Content
        let newRegion = {region with Handler = newHandler}
        _va.[region.BaseAddress] <- newRegion

    let setEntryPoint(handler: BinHandler) =
        _activeRegion <- 
            getMemoryRegion(handler.FileInfo.EntryPoint)
            |> Some

        // save the EIP registry value
        let eip = string Register.EIP
        let eipValue = createVariableWithValue(eip, EmulatedType.DoubleWord, BitVector.ofUInt64 handler.FileInfo.EntryPoint 32<rt>)
        _variables.Add(eip, eipValue)

    let mapSections(handler: BinHandler) =            
        handler.FileInfo.GetSections()
        |> Seq.map(fun section -> {
                BaseAddress = section.Address
                Content = handler.BinReader.Bytes
                Handler = handler
                Protection = section.Kind
                Type = section.Name
                Info = handler.FileInfo.FilePath
        })
        |> Seq.iter(addRegion)

    let setupRegisters() =
        [
            // 32 bits
            createVariableWithValue(string Register.EAX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)
            createVariableWithValue(string Register.EBX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)
            createVariableWithValue(string Register.ECX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)
            createVariableWithValue(string Register.EDX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)
            createVariableWithValue(string Register.ESI, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)
            createVariableWithValue(string Register.EDI, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 32<rt>)

            // 16 bits
            createVariableWithValue(string Register.AX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)
            createVariableWithValue(string Register.BX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)
            createVariableWithValue(string Register.CX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)
            createVariableWithValue(string Register.DX, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)
            createVariableWithValue(string Register.SI, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)
            createVariableWithValue(string Register.DI, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 16<rt>)

            // 8 bits
            createVariableWithValue(string Register.AH, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.AL, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.BH, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.BL, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.CH, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.CL, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.DH, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)
            createVariableWithValue(string Register.DL, EmulatedType.DoubleWord, BitVector.ofUInt32 0u 8<rt>)

            // Flags bit register
            createVariableWithValue(string Register.OF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.DF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.IF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.TF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.SF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.ZF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.AF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.PF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
            createVariableWithValue(string Register.CF, EmulatedType.Bit, BitVector.ofUInt32 0u 1<rt>)
        ] |> List.iter(fun register ->
            _variables.Add(register.Name, register)
        )

    let addStackRegion(handler: BinHandler) =
        // must create a new handler for each newly created region
        let stackBuffer = Array.zeroCreate<Byte>(8192)
        let isa = ISA.OfString "x86"
        let baseAddress = 0x1000UL
        let stackHandler = BinHandler.Init(isa, ArchOperationMode.NoMode, FileFormat.RawBinary, baseAddress, stackBuffer)

        let stack = {
            BaseAddress = baseAddress
            Content = stackBuffer
            Handler = stackHandler
            Protection = SectionKind.WritableSection ||| SectionKind.ExecutableSection
            Type = "Stack"
            Info = handler.FileInfo.FilePath
        }
        addRegion(stack)

        // set ESP value
        let esp = string Register.ESP
        let startAddress = int32 stack.BaseAddress + int32 (stackBuffer.Length / 2)
        let espValue = createVariableWithValue(esp, EmulatedType.DoubleWord, BitVector.ofInt32 startAddress 32<rt>)
        _variables.Add(esp, espValue)

        // set EBP value equals to ESP
        let ebp = string Register.EBP
        let ebpValue = createVariableWithValue(ebp, EmulatedType.DoubleWord, espValue.Value)
        _variables.Add(ebp, ebpValue)

    let mapIAT(handler: BinHandler) =
        handler.FileInfo.GetSymbols()
        |> Seq.iter(fun symbol ->
            if symbol.Kind = SymbolKind.ExternFunctionType || symbol.Kind = SymbolKind.FunctionType 
            then writeMemory(symbol.Address, [|1uy; 2uy; 3uy; 4uy|])
            Console.WriteLine("[0x{0, -20}] {1} ({2}) from {3}", symbol.Address.ToString("X"), symbol.Name, symbol.Kind, symbol.LibraryName)
        )

    let initialize(handler: BinHandler) =
        mapSections(handler)
        addStackRegion(handler)
        setEntryPoint(handler)
        mapIAT(handler)
        setupRegisters()        

    let getTempName(index: String, emuType: EmulatedType) =
        let size =  Utility.getSize(emuType)
        String.Format("T_{0}:{1}", index, size)

    member this.GetOrCreateTemporaryVariable(index: String, emuType: EmulatedType) =
        let name = getTempName(index, emuType)
        match _tempVariables.TryGetValue(name) with
        | (true, value) -> value
        | _ -> 
            let variable = {createVariable(name, emuType) with IsTemp = true}
            _tempVariables.[name] <- variable
            variable
    
    member this.GetVariable(name: String) =
        _variables.[name]

    member this.GetVariable(name: String, emuType: EmulatedType) =        
        match _variables.TryGetValue(name) with
        | (true, value) -> value
        | _ ->
            let name = getTempName(name, emuType)
            _tempVariables.[name]

    member this.SetVariable(value: EmulatedValue) =
        if value.IsTemp
        then _tempVariables.[value.Name] <- value
        else _variables.[value.Name] <- value

    member this.ClearTemporaryVariables() =
        _tempVariables.Clear()

    member this.Initialize(buffer: Byte array) =
        let isa = ISA.OfString "x86"
        let handler = BinHandler.Init(isa, ArchOperationMode.NoMode, FileFormat.RawBinary, Addr.MinValue, buffer)
        initialize(handler)

    member this.Initialize(filename: String) =  
        let isa = ISA.OfString "x86"
        let handler = BinHandler.Init(isa, ArchOperationMode.NoMode, FileFormat.PEBinary, Addr.MinValue, filename)        
        initialize(handler)

    member this.GetMemoryRegion(address: UInt64) =
        getMemoryRegion(address)

    member this.GetActiveMemoryRegion() =
        _activeRegion.Value

    member this.UpdateMemoryRegion(oldRegion: MemoryRegion, newRegion: MemoryRegion) =
        _va.[oldRegion.BaseAddress] <- newRegion

    member this.WriteMemory(address: UInt64, value: Byte array) =
        writeMemory(address, value)

    member this.GetInstruction() =
        let programCounter = _variables.["EIP"]
        let instruction = BinHandler.ParseInstr (this.GetActiveMemoryRegion().Handler) (programCounter.Value |> BitVector.toUInt64)
        _variables.["EIP"] <- 
            {programCounter with
                Value = BitVector.add programCounter.Value (BitVector.ofUInt32 instruction.Length 32<rt>)
            }
        instruction