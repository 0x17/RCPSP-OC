﻿namespace RCPSP

open System.IO
open System

open Utils

module Serialization =
    let mapToStr m =
        String.Join("\n", Seq.map (fun k -> k.ToString () + "->" + (Map.find k m).ToString ()) (keys m))
    let mapFromStr (s:string) t =        
        let parseLine (line:string) =
            let lhsAndRhs = line.Trim().Split ([|"->"|], StringSplitOptions.None)
            (int lhsAndRhs.[0], t lhsAndRhs.[1])
        let lineNotEmpty (line:string) = line.Trim().Length > 0
        s.Split [|'\n'|] |> Array.filter lineNotEmpty |> Array.map parseLine |> Map.ofArray
    let array2DToStr (a:int [,]) =
        let rowStr i = Seq.fold (fun acc j -> acc + " " + a.[i,j].ToString ())  "" [0..(Array2D.length2 a)-1]
        System.String.Join ("\n", Seq.map rowStr [0..(Array2D.length1 a)-1])

    let slurp = File.ReadAllText
    let slurpLines = File.ReadAllLines
    let spit filename content = File.WriteAllText (filename, content)
    let spitAppend filename content = File.AppendAllText (filename, content)

    let spitMap filename = spit filename << mapToStr
    let slurpMap filename = mapFromStr (slurp filename) int

