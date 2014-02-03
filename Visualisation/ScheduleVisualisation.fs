﻿namespace RCPSP

open System
open System.Collections.Generic
open System.Drawing
open System.Windows.Forms
open System.Data

open Utils

module ScheduleVisualisation =
    let saveViewToPng (view:Control) filename =
        let bmp = new Bitmap (view.Width, view.Height)
        view.DrawToBitmap(bmp, Rectangle(Point.Empty, bmp.Size))
        bmp.Save (filename+".png")

    let saveViews prefix views =
        Seq.iteri (fun i view -> saveViewToPng view (prefix+string(i+1))) views
        
    let show caption (ps:ProjectStructure) (sts:IntMap) =
        let lblOffsetY = 500       

        let mainForm = new Form (Width = 1280, Height = 720, Text = "Ablaufplan - " + caption)
        mainForm.StartPosition <- FormStartPosition.CenterScreen

        let z = ps.NeededOCForSchedule sts

        let addLbl text loc =
            let lbl = new Label ()
            lbl.Text <- text
            lbl.Location <- loc
            lbl.Size <- Size (200, 20)
            mainForm.Controls.Add lbl

        let dgv = new DataGridView ()

        let cell = new DataGridViewTextBoxCell ()        

        let remZeroes str = if str = "0" then "" else str

        let initJobToColorMap () =
            let r = Random 23
            let colMap = Dictionary<int, Color> ()
            colMap.Add (0, Color.White)
            for j in ps.Jobs do
                let rval () = r.Next (20, 256)
                colMap.Add(j, Color.FromArgb(rval (), rval (), rval ()))
            colMap

        let capLbl = new Label ()
        let updateCapLbl cap =
            capLbl.Text <- "Capacity: " + string cap
        updateCapLbl (ps.Capacities 1)
        capLbl.Location <- Point (10, lblOffsetY+90)
        mainForm.Controls.Add capLbl
        
        let colMap = initJobToColorMap()
        let setCell (i:int) (j:int) (v:int) =
                let cell = dgv.[j,i]
                cell.Value <- remZeroes (v.ToString ())
                cell.Style.BackColor <- colMap.[v]

        let addZrtRow r =
            let rcount = dgv.Rows.Count
            let dgvr = new DataGridViewRow ()
            dgvr.Height <- 20
            dgvr.HeaderCell.Value <- "zrt"
            dgv.Rows.Add dgvr |> ignore
            for t in ps.TimeHorizon do
                let cell = dgv.[t-1, rcount-1]
                cell.Value <- z r t

        let updateGridForRes r =
            dgv.Columns.Clear ()
            dgv.Rows.Clear ()

            let grid = ps.ScheduleToGrid sts r

            let nrows = Array2D.length1 grid
            let ncols = Array2D.length2 grid

            for t in 1..ncols do
                let dgvc = new DataGridViewColumn ()
                dgvc.Width <- 20
                dgvc.CellTemplate <- cell
                dgvc.HeaderText <- t.ToString ()
                dgv.Columns.Add dgvc |> ignore

            for k in 1..nrows do            
                let dgvr = new DataGridViewRow ()
                dgvr.Height <- 12
                dgvr.HeaderCell.Value <- (nrows-k+1).ToString ()
                dgv.Rows.Add dgvr |> ignore

            Array2D.iteri setCell grid
            addZrtRow r
            dgv.Refresh ()

            updateCapLbl (ps.Capacities r)

        updateGridForRes 1

        dgv.Anchor <- AnchorStyles.Top ||| AnchorStyles.Left
        dgv.AutoSize <- true
        dgv.CellBorderStyle <- DataGridViewCellBorderStyle.None
        dgv.ScrollBars <- ScrollBars.Both

        mainForm.Controls.Add dgv
        mainForm.HorizontalScroll.Enabled <- true
        mainForm.VerticalScroll.Enabled <- true
        mainForm.AutoScroll <- true

        addLbl "Selected resource" (Point (10, lblOffsetY))

        let resCb = new ComboBox ()
        resCb.DataSource <- Array.ofSeq ps.Resources
        resCb.Location <- Point (10, lblOffsetY+30)
        resCb.SelectedValueChanged.Add(fun _ -> updateGridForRes (resCb.SelectedIndex+1))
        mainForm.Controls.Add resCb

        addLbl ("Makespan: " + sts.[Seq.length (keys sts)].ToString ()) (Point (10, lblOffsetY+60))
        
        addLbl ("Horizon: " + string ps.TimeHorizon.Length) (Point (10, lblOffsetY+120))

        let sumOc = ps.Resources >< ps.TimeHorizon |> Seq.sumBy (fun (r,t) -> float (z r t) * (ps.Kappa r))
        addLbl ("Total OC costs: " + string sumOc) (Point(10, lblOffsetY+150))

        let profit = ps.Profit sts
        addLbl ("Profit: " + string profit) (Point(10, lblOffsetY+180))

        mainForm.Closed.Add(fun _ -> Application.Exit ())
        mainForm.Show ()

        dgv

    let showSchedules data =
        //Seq.map (fun (caption,ps,sts) -> show caption ps sts) data |> saveViews "schedule"
        Seq.iter (fun (caption,ps,sts) -> show caption ps sts |> ignore) data
        System.Windows.Forms.Application.Run ()

    