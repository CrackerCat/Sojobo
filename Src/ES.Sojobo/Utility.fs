﻿namespace ES.Sojobo

open System
open System.Runtime.InteropServices
open ES.Sojobo.Model
open B2R2

module Utility =
    let toArray(bitVector: BitVector) =
        let size = int32 <| BitVector.getType bitVector
        let value = BitVector.getValue bitVector
        match size with        
        | 8 -> [|byte value|]
        | 16 -> BitConverter.GetBytes(uint16 value)
        | 32 -> BitConverter.GetBytes(uint32 value)
        | 64 -> BitConverter.GetBytes(uint64 value)
        | _ -> failwith("Unexpected size: " + string size)

    let getType(regType: RegType) =
        match (RegType.toBitWidth regType) with
        | 1 -> EmulatedType.Bit
        | 8 -> EmulatedType.Byte
        | 16 -> EmulatedType.Word
        | 32 -> EmulatedType.DoubleWord
        | 64 -> EmulatedType.QuadWord
        | _ -> failwith("Invalid reg type size: " + regType.ToString())

    let getSize(emuType: EmulatedType) =
        match emuType with
        | EmulatedType.Bit -> 1
        | EmulatedType.Byte -> 8
        | EmulatedType.Word -> 16
        | EmulatedType.DoubleWord -> 32
        | EmulatedType.QuadWord -> 64

    let getTypeSize =
        getType >> getSize

    //let writeStructure<'T when 'T : struct>(s: 'T, offset: Int32, buffer: Byte array) =
    let writeStructure(s: Object, offset: Int32, buffer: Byte array) =        
        let size = Marshal.SizeOf(s)
        let ptr = Marshal.AllocHGlobal(size)
        Marshal.StructureToPtr(s, ptr, true)
        Marshal.Copy(ptr, buffer, offset, size)
        Marshal.FreeHGlobal(ptr)