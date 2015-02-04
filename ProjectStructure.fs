﻿namespace RCPSP

open Microsoft.FSharp.Collections

open Utils
open TopologicalSorting

type IntMap = Map<int,int>

type ProjectStructure(jobs, durations, demands, preds: int -> Set<int>, resources, capacities, kappa, zmax) =
    let firstJob = Set.minElement jobs
    let lastJob = Set.maxElement jobs
    let actualJobs = Set.difference jobs (set [firstJob; lastJob])

    let T = Seq.sumBy durations jobs
    let horizon = [1..T]

    let transPreds = transitiveHull preds
    let succs = memoize (fun i -> jobs |> Seq.filter (fun j -> (preds j).Contains i) |> Set.ofSeq)
    let transSuccs = transitiveHull succs

    let topOrdering = topSort jobs preds
    let revTopOrdering = topSort jobs succs

    let ft (sts:IntMap) j = sts.[j] + durations j
    let st (fts:IntMap) j = fts.[j] - durations j

    let lastPredFinishingTime sts = Seq.max << Seq.map (ft sts) << preds
    let firstSuccStartingTime fts = Seq.min << Seq.map (st fts) << succs
    
    let (computeEsts, computeLfts) =        
        let elsftForJob seedKey seedVal func sts j =
            Map.add j (if j = seedKey then seedVal else func sts j) sts
        ((fun () -> Seq.fold (elsftForJob firstJob 0 lastPredFinishingTime) Map.empty topOrdering),
         (fun () -> Seq.fold (elsftForJob lastJob T firstSuccStartingTime) Map.empty revTopOrdering))

    let ests = computeEsts ()
    let efts = ft ests

    let lfts = computeLfts ()
    let lsts = st lfts

    let deadline = lfts.[lastJob]

    let isFinishedAtEndOfPeriod (sts:IntMap) t j = sts.ContainsKey j && ft sts j <= t
    let arePredsFinished sts j t = preds j |> Set.forall (isFinishedAtEndOfPeriod sts t)

    let isActiveInPeriod (sts:IntMap) t j = sts.[j] < t && t <= ft sts j        
    let activeInPeriodSet sts t = keys sts |> Seq.filter (isActiveInPeriod sts t)

    let demandInPeriod sts r t = activeInPeriodSet sts t |> Seq.sumBy (fun j -> demands j r)

    let residualCapacity sts r t = capacities r - demandInPeriod sts r t
        
    let enoughCapacityForJob z sts j stj =
        resources >< [stj+1..stj+durations j]
        |> Seq.forall (fun (r,t) -> residualCapacity sts r t + z r t >= demands j r)

    let predsAndCapacityFeasible z sts job t = arePredsFinished sts job t && enoughCapacityForJob z sts job t

    let scheduleJob z acc j = Map.add j (numsGeq (lastPredFinishingTime acc j) |> Seq.find (enoughCapacityForJob z acc j)) acc            
    let ssgsCore z sts λ = Seq.fold (scheduleJob z) sts λ
    let ssgs z λ = ssgsCore z (Map.ofList [(Seq.head λ, 0)]) (Seq.skip 1 λ)

    let makespan sts = ft sts lastJob

    let neededOvercapacityInPeriod sts r t = max 0 (-residualCapacity sts r t)
    let totalOvercapacityCosts sts =
        resources >< [0..makespan sts]
        |> Seq.sumBy (fun (r,t) -> kappa r * float (neededOvercapacityInPeriod sts r t))

    let zeroOc r t = 0
    let maxOc r t = zmax r

    let minMaxMakespanBounds =
        let tkappar r = System.Math.Ceiling(float (Seq.sumBy (fun j -> durations j * demands j r) jobs) / float (capacities r + zmax r)) |> int
        let tkappa = resources |> Seq.map tkappar |> Seq.max
        let minMakespanApprox = max (makespan ests) tkappa
        let maxMakespanApprox = makespan (ssgs zeroOc topOrdering)
        (minMakespanApprox, maxMakespanApprox)

    let u =
        let (minMakespanApprox, maxMakespanApprox) = minMaxMakespanBounds
        let maxOcCosts = totalOvercapacityCosts ests
        let ufunc t = maxOcCosts - maxOcCosts / System.Math.Pow(float(maxMakespanApprox-minMakespanApprox), 2.0) * System.Math.Pow(float(t - minMakespanApprox), 2.0) 

        if minMakespanApprox = maxMakespanApprox then fun t -> -float(t)
        else ufunc

    let revenue = u << makespan

    let profit sts = (revenue sts) - totalOvercapacityCosts sts
    
    let ssgsoc (ps:ProjectStructure) λ =
        let computeCandidateSchedule sts j t =
            let candidate = Map.add j t sts
            let jix = indexOf λ j
            let subλ = Seq.skip (inc jix) λ
            ssgsCore (fun r t -> 0) candidate subλ

        let chooseBestPeriod sts j tlower tupper =
            [tlower..tupper]
            |> Seq.filter (fun t -> enoughCapacityForJob (fun r t -> zmax r) sts j t)
            |> Seq.maxBy (fun t -> computeCandidateSchedule sts j t |> profit)
            
        let scheduleJob acc job =
            let tlower = lastPredFinishingTime acc job
            let tupper = numsGeq tlower |> Seq.find (enoughCapacityForJob (fun r t -> 0) acc job)
            Map.add job (chooseBestPeriod acc job tlower tupper) acc

        Seq.fold scheduleJob (Map.ofList [(Seq.head λ, 0)]) (Seq.skip 1 λ)

    member ps.ProfitForRemainingCapacity (sts: Map<int,int>, remainingRes: int[,]) =
        let rev = revenue sts
        let mutable tcosts = 0.0
        for res in 0..(Array2D.length1 remainingRes)-1 do
            for period in 0..(Array2D.length2 remainingRes)-1 do
                if remainingRes.[res,period] < 0 then
                    tcosts <- tcosts + (-(float remainingRes.[res,period]) * (kappa (res+1)))
        rev - tcosts

    member ps.IsMinMaxMakespanBoundsSame =
        let (minMakespanApprox, maxMakespanApprox) = minMaxMakespanBounds
        minMakespanApprox = maxMakespanApprox

    member ps.Revenue = revenue
    member ps.Profit = profit    

    member ps.ScheduleToGrid sts r =
        let nrows = capacities r + zmax r
        let grid = Array2D.zeroCreate nrows T
        for t in horizon do
            let actJobs = activeInPeriodSet sts t
            let mutable colCtr = dec nrows
            for j in actJobs do
                for k in 1..demands j r do
                    Array2D.set grid colCtr (dec t) j
                    colCtr <- dec colCtr
        grid

    member ps.FinishingTimesToStartingTimes fts =
        Map.map (fun j ftj -> ftj - durations j) fts

    member ps.ScheduleFeasibleWithMaxOC sts =
        resources >< [0..makespan sts]
        |> Seq.forall (fun (r,t) -> neededOvercapacityInPeriod sts r t <= zmax r)
            
    member ps.Jobs = jobs
    member ps.LastJob = lastJob
    member ps.ActualJobs = actualJobs
    member ps.Durations = durations
    member ps.Demands = demands
    member ps.Capacities = capacities
    member ps.Preds = preds
    member ps.Succs = succs
    member ps.TransPreds = transPreds
    member ps.TransSuccs = transSuccs
    member ps.Resources = resources    
    member ps.Kappa = kappa
    member ps.U = u
    member ps.ZMax = zmax
    member ps.TimeHorizon = horizon   

    member ps.EarliestStartingTimes = mapToFunc ests
    member ps.LatestStartingTimes = lsts
    member ps.EarliestFinishingTimes = efts
    member ps.LatestFinishingTimes = mapToFunc lfts
    member ps.Deadline = deadline

    member ps.EarliestStartSchedule = ests

    member ps.EnoughCapacityForJob = enoughCapacityForJob
    member ps.LastPredFinishingTime = lastPredFinishingTime

    member ps.Makespan = makespan
    member ps.TotalOvercapacityCosts = totalOvercapacityCosts

    member ps.SerialScheduleGenerationScheme () = ssgs zeroOc topOrdering
    member ps.SerialScheduleGenerationSchemeWithOC = ssgs     

    member ps.SerialScheduleGenerationSchemeCore = ssgsCore

    member ps.NeededOCForSchedule sts = neededOvercapacityInPeriod sts

    member ps.DemandInPeriod = demandInPeriod

    member ps.ArePredsFinished = arePredsFinished

    static member Create(jobs, durations, demands, capacities, preds, resources, kappa, zmax) =
        let arrayToBaseOneMap arr = Array.mapi (fun ix e -> (inc ix,e)) arr |> Map.ofSeq
        ProjectStructure(jobs, arrayToBaseOneMap durations |> mapToFunc,
                         demands |> Array.map arrayToBaseOneMap |> arrayToBaseOneMap |> map2DToFunc,
                         preds, resources, capacities |> arrayToBaseOneMap |> mapToFunc, kappa, zmax)