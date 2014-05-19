﻿namespace RCPSP

open Utils
open TopologicalSorting

module ModifiedSSGS =
    let cleverSsgsHeuristic (ps:ProjectStructure) λ =
        let computeCandidateSchedule sts j t =
            let candidate = Map.add j t sts
            let jix = indexOf λ j
            let subλ = Seq.skip (inc jix) λ
            FastSSGS.solvePartial ps (fun r t -> 0) candidate subλ

        let chooseBestPeriod sts j tlower tupper =
            [tlower..tupper]
            |> Seq.filter (fun t -> ps.EnoughCapacityForJob (fun r t -> ps.ZMax r) sts j t)
            |> Seq.maxBy (fun t -> computeCandidateSchedule sts j t |> ps.ProfitForRemainingCapacity)
            
        let scheduleJob acc job =
            let tlower = ps.LastPredFinishingTime acc job
            let tupper = numsGeq tlower |> Seq.find (ps.EnoughCapacityForJob (fun r t -> 0) acc job)
            Map.add job (chooseBestPeriod acc job tlower tupper) acc

        Seq.fold scheduleJob (Map.ofList [(Seq.head λ, 0)]) (Seq.skip 1 λ)

    let cleverSSGSHeuristicDefault (ps:ProjectStructure) = cleverSsgsHeuristic ps (TopologicalSorting.topSort ps.Jobs ps.Preds)

    let cleverSSGSHeuristicAllOrderings (ps:ProjectStructure) =
        let winner = Seq.maxBy (ps.Profit << (cleverSsgsHeuristic ps)) (allTopSorts ps.Jobs ps.Preds)
        cleverSsgsHeuristic ps winner

    let cleverSSGSHeuristicOrderingStats (ps:ProjectStructure) (optimalSts:Map<int,int>) =
        let addPair acc ordering =
            Map.add ordering (cleverSsgsHeuristic ps ordering |> ps.CalculateGap optimalSts) acc
        Seq.fold addPair Map.empty (allTopSorts ps.Jobs ps.Preds)