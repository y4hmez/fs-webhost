namespace LiveTracker

open System

type Period =
    | Year of int
    | Month of int * int
    | Day of int * int * int

module Dates =
    let InitInfinite (startSeed : DateTime) = 
        startSeed |> Seq.unfold (fun d -> Some(d, d.AddDays 1.0))

    let In period = 
        let generate dt predicate =
            dt |> InitInfinite |> Seq.takeWhile predicate
        match period with
        | Year(y) -> generate (DateTime(y,1,1)) (fun d -> d.Year = y)
        | Month(y,m) -> generate (DateTime(y,m,1)) (fun d -> d.Year = y && d.Month = d.Month) 
        | Day(y,m,d) -> DateTime(y,m,d) |> Seq.singleton

    let BoundariesIn period =
        let getBoundaries firstTick (forward : DateTime -> DateTime) =
            let lastTick = forward(firstTick).AddTicks -1L
            (firstTick, lastTick)
        match period with
        | Year(y) -> getBoundaries (DateTime(y,1,1)) (fun d -> d.AddYears 1)
        | Month(y,m) -> getBoundaries (DateTime(y,m,1)) (fun d -> d.AddMonths 1)
        | Day(y,m,d) -> getBoundaries (DateTime(y,m,d)) (fun d -> d.AddDays 1.0)


module Reservations = 
    open System.IO
    open Newtonsoft.Json

    type IReservations = 
        inherit seq<Envelope<ReservationEvt>>
        abstract Between : DateTime -> DateTime -> seq<Envelope<ReservationEvt>>
    
    type ReservationsInFiles(directory : DirectoryInfo) =
        let toReservation (f : FileInfo) =
            let json = File.ReadAllText f.FullName
            JsonConvert.DeserializeObject<Envelope<ReservationEvt>>(json)
        let toEnumerator (s : seq<'T>) = s.GetEnumerator();
        let getContainingDirectory (d : DateTime) = 
            Path.Combine(
                directory.FullName,
                d.Year.ToString(),
                d.Month.ToString(),
                d.Day.ToString())
        let appendPath p2 p1 = Path.Combine(p1,p2)
        let getJsonFiles (dir : DirectoryInfo) = 
            if Directory.Exists (dir.FullName) then
                dir.EnumerateFiles("*.json",SearchOption.AllDirectories)
            else
                Seq.empty<FileInfo>

        member this.Write (reservation : Envelope<ReservationEvt>) =
            let withExtension extension path = Path.ChangeExtension(path, extension)
            let directoryName = reservation.Item.Date |> getContainingDirectory
            let fileName =
                directoryName
                |> appendPath (reservation.Id.ToString())
                |> withExtension "json"

            let json = JsonConvert.SerializeObject reservation
            Directory.CreateDirectory directoryName |> ignore
            File.WriteAllText(fileName, json)

        member this.Add (reservation : Envelope<ReservationEvt>) =
            this.Write reservation

        interface IReservations with
            member this.Between min max =
                Dates.InitInfinite min
                |> Seq.takeWhile (fun d -> d <= max)
                |> Seq.map getContainingDirectory
                |> Seq.collect (fun dir -> DirectoryInfo(dir) |> getJsonFiles)
                |> Seq.map toReservation

            member this.GetEnumerator() =
                directory
                |> getJsonFiles
                |> Seq.map toReservation
                |> toEnumerator

            member this.GetEnumerator() =
                (this :> seq<Envelope<ReservationEvt>>).GetEnumerator() :> System.Collections.IEnumerator
    
    let Between min max (reservations : IReservations) = 
        reservations.Between min max

    let On (date : DateTime) reservations =
        let min = date.Date
        let max = (min.AddDays 1.0) - TimeSpan.FromTicks 1L
        reservations |> Between min max

    let Handle capacity reservations (request : Envelope<ReservationCmd>) =
        let reservedSeatsOnDate = 
            reservations
            |> On request.Item.Date
            |> Seq.sumBy (fun r -> r.Item.Quantity)
        if capacity - reservedSeatsOnDate < request.Item.Quantity then
            None
        else
             ({
                Date = request.Item.Date
                Name = request.Item.Name
                Email = request.Item.Email
                Quantity = request.Item.Quantity                
            } : ReservationEvt)            
            |> WrapWithDefaults
            |> Some


//[<AutoOpen>]
module Notifications =
    open System.IO
    open Newtonsoft.Json

    type INotifications = 
        inherit seq<Envelope<NotificationEvt>>
        abstract  About : Guid -> seq<Envelope<NotificationEvt>>
                 
    type NotificationsInFiles(directory : DirectoryInfo) =
        let toNotification (f : FileInfo) =
            let json = File.ReadAllText f.FullName
            JsonConvert.DeserializeObject<Envelope<NotificationEvt>>(json)

        let toEnumerator (s : seq<'T>) = s.GetEnumerator();

        let getContainingDirectory id = Path.Combine(directory.FullName, id.ToString())
    
        let appendPath p2 p1 = Path.Combine(p1,p2)

        let getJsonFiles (dir : DirectoryInfo) = 
            if Directory.Exists (dir.FullName) then
                dir.EnumerateFiles("*.json",SearchOption.AllDirectories)
            else
                Seq.empty<FileInfo>

        member this.Write (notification : Envelope<NotificationEvt>) =
            let withExtension extension path = Path.ChangeExtension(path, extension)
            let directoryName = notification.Item.About |> getContainingDirectory
            let fileName =
                directoryName
                |> appendPath (notification.Id.ToString())
                |> withExtension "json"

            let json = JsonConvert.SerializeObject notification
            Directory.CreateDirectory directoryName |> ignore
            File.WriteAllText(fileName, json)
    
        interface INotifications with
            member this.About id =
                id
                |> getContainingDirectory
                |> (fun dir -> DirectoryInfo(dir))
                |> getJsonFiles
                |> Seq.map toNotification
            member this.GetEnumerator() =
                directory
                |> getJsonFiles
                |> Seq.map toNotification
                |> toEnumerator
            member this.GetEnumerator() =
                (this :> seq<Envelope<NotificationEvt>>).GetEnumerator() :> System.Collections.IEnumerator

    let About id (notifications : INotifications) = notifications.About id