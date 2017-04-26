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
    open Microsoft.WindowsAzure.Storage.Blob
    open Microsoft.WindowsAzure.Storage
    open Microsoft.Azure

    type IReservations = 
        inherit seq<Envelope<ReservationEvt>>
        abstract Between : DateTime -> DateTime -> seq<Envelope<ReservationEvt>>

    [<CLIMutable>]
    type StoredReservatinons = {
        Reservations : Envelope<ReservationEvt>  array
        AcceptedCommandIds : Guid array 
    }
          
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

    type ResevervationsInAzureBlobs(blobContainer : CloudBlobContainer) =
        let toReservation (b : CloudBlockBlob) =
            let json = b.DownloadText()
            let sr = JsonConvert.DeserializeObject<StoredReservatinons> json
            sr.Reservations
        let toEnumerator (s : seq<'T>) = s.GetEnumerator()
        let getId (d : DateTime) =
            String.Join(
                "/",
                [
                    d.Year.ToString()
                    d.Month.ToString()
                    d.Day.ToString()
                ]) |> sprintf "%s.json"

        member this.GetAccessCondition date =
            let id = date |> getId
            let b = blobContainer.GetBlockBlobReference id
            try 
                b.FetchAttributes()
                b.Properties.ETag |> AccessCondition.GenerateIfMatchCondition
            with
            | :? StorageException as e when e.RequestInformation.HttpStatusCode = 404 ->
                AccessCondition.GenerateIfNoneMatchCondition "*"

        member this.Write (reservation : Envelope<ReservationEvt>, commandId, condition) = 
            let id = reservation.Item.Date |> getId
            let b = blobContainer.GetBlockBlobReference id
            let inStore =
                try
                    let jsonInStore = b.DownloadText(accessCondition = condition)
                    JsonConvert.DeserializeObject<StoredReservatinons> jsonInStore
                with 
                | :? StorageException as e when e.RequestInformation.HttpStatusCode = 404 -> { Reservations = [||]; AcceptedCommandIds = [||] }

            let isReplay = 
                inStore.AcceptedCommandIds
                |> Array.exists (fun id -> commandId = id)

            if not isReplay then            
                let updated = 
                    {
                        Reservations = Array.append [| reservation |] inStore.Reservations
                        AcceptedCommandIds = Array.append [| commandId |] inStore.AcceptedCommandIds
                    }

                let json = JsonConvert.SerializeObject updated
                b.Properties.ContentType <- "application/json"
                b.UploadText(json, accessCondition = condition)
                
        interface IReservations with
            member this.Between min max =
                Dates.InitInfinite min
                |> Seq.takeWhile (fun d -> d <= max)
                |> Seq.map getId
                |> Seq.map blobContainer.GetBlockBlobReference
                |> Seq.filter (fun b -> b.Exists())
                |> Seq.collect toReservation

            member this.GetEnumerator() =
                blobContainer.ListBlobs()
                |> Seq.cast<CloudBlockBlob>
                |> Seq.collect toReservation
                |> toEnumerator

            member this.GetEnumerator() = 
                (this :> seq<Envelope<ReservationEvt>>).GetEnumerator() :> System.Collections.IEnumerator





[<AutoOpen>]
module Notifications =
    open System.IO
    open Newtonsoft.Json
    open Microsoft.WindowsAzure.Storage.Blob
    open Microsoft.WindowsAzure.Storage
    open Microsoft.Azure


    type INotifications = 
        inherit seq<Envelope<NotificationEvt>>
        abstract  About : Guid -> seq<Envelope<NotificationEvt>>

    type NotificationsInAzureBlobs(blobContainer : CloudBlobContainer) =
        let toNotification (b : CloudBlockBlob) =
            let json = b.DownloadText()
            JsonConvert.DeserializeObject<Envelope<NotificationEvt>> json
        
        let toEnumerator (s : seq<'T>) = s.GetEnumerator()

        member this.Write notification =
            let id = sprintf "%O/%O.json" notification.Item.About notification.Id
            let b = blobContainer.GetBlockBlobReference id
            let json = JsonConvert.SerializeObject notification
            b.Properties.ContentType <- "application/json"
            b.UploadText json

        interface INotifications with
            member this.About id =
                blobContainer.ListBlobs(id.ToString(), true)
                |> Seq.cast<CloudBlockBlob>
                |> Seq.map toNotification

            member this.GetEnumerator() = 
                blobContainer.ListBlobs(useFlatBlobListing = true)
                |> Seq.cast<CloudBlockBlob>
                |> Seq.map toNotification
                |> toEnumerator

            member this.GetEnumerator() = (this :> seq<Envelope<NotificationEvt>>).GetEnumerator() :> System.Collections.IEnumerator
                 
