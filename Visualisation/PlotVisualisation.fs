﻿namespace RCPSP

open System.Text
open System.IO

open System
open Serialization

module PlotVisualisation =
    let fext = ".dat"
    let writePlotData (f:int->float) (domain: int seq) filename =
        let sb = StringBuilder ()
        for x in domain do
            sb.Append(string(x)+" "+string(f(x))+"\n") |> ignore
        spit (filename + fext) (sb.ToString ())

    let showPlot filename =
        let gplotPath = @"C:\Program Files (x86)\gnuplot\bin\gnuplot.exe"
        runCmd Blocking gplotPath ("-e \"plot '" + filename + fext + "' using 1:2;pause -1\"")
        
    let generatePlot f domain filename =
        writePlotData f domain filename
        showPlot filename
        File.Delete (filename + fext)