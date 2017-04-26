namespace Webhost

open System
open System.Collections.Concurrent
open System.Web.Http
open LiveTracker
open LiveTracker.Infrastructure
open LiveTracker.Reservations
open LiveTracker.Notifications
open System.Reactive
//open FSharp.Reactive
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open System.IO
open Newtonsoft.Json
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage
open Microsoft.Azure

type Agent<'T> = MailboxProcessor<'T>

type HttpRouteDefaults = { Controller : string; Id : obj }  


module AzureQ = 
    let enqueue (q: Queue.CloudQueue) msg = 
        let json = JsonConvert.SerializeObject msg
        Queue.CloudQueueMessage(json) |> q.AddMessage

    let dequeue (q : Queue.CloudQueue) =
        match q.GetMessage() with
        | null -> None
        | msg -> Some(msg)

type Global() =
    inherit Web.HttpApplication()
    member this.Application_Start (sender :obj) (e : EventArgs) = 
            let seatingCapacity = 10
                        
            //let reservations = ReservationsInFiles(DirectoryInfo(Web.HttpContext.Current.Server.MapPath("~/ReservationStore")))            
            //let notifications = NotificationsInFiles(DirectoryInfo(Web.HttpContext.Current.Server.MapPath("~/NotificationStore")))

            let storageAccount = CloudConfigurationManager.GetSetting "storageConnectionString" |> CloudStorageAccount.Parse

            //reservation queue
            let rq = storageAccount.CreateCloudQueueClient().GetQueueReference("reservations")
            rq.CreateIfNotExists() |> ignore

            //notification queue
            let nq = storageAccount.CreateCloudQueueClient().GetQueueReference("notifications")
            nq.CreateIfNotExists() |> ignore
            
            //reservations
            let reservationsContainer = storageAccount.CreateCloudBlobClient().GetContainerReference("reservations")
            reservationsContainer.CreateIfNotExists() |> ignore                    
            let reservations = ResevervationsInAzureBlobs(reservationsContainer)
            
            //notifications
            let notificationsContainer = storageAccount.CreateCloudBlobClient().GetContainerReference("notifications")
            notificationsContainer.CreateIfNotExists() |> ignore
            let notifications = NotificationsInAzureBlobs(notificationsContainer)
            
            let reservationSubject = new Subjects.Subject<Envelope<ReservationEvt> * Guid * AccessCondition>()
            reservationSubject.Subscribe reservations.Write |> ignore
                        
            let notificationSubject = new Subjects.Subject<NotificationEvt>()
            
            notificationSubject   
            |> Observable.map WrapWithDefaults
            |> Observable.subscribeWithCallbacks (AzureQ.enqueue nq) ignore ignore
            |> ignore
                                       
            let handleR (msg : Queue.CloudQueueMessage) = 
                let json = msg.AsString
                let cmd = 
                    JsonConvert.DeserializeObject<Envelope<ReservationCmd>> json
                let condition = reservations.GetAccessCondition cmd.Item.Date
                let newReservations = Handle seatingCapacity reservations cmd
                match newReservations with
                        | Some(r) -> 
                            reservationSubject.OnNext(r, cmd.Id, condition)
                            notificationSubject.OnNext 
                                {
                                    About = cmd.Id
                                    Type = "Success"
                                    Message = sprintf "completed %s " (cmd.Item.Date.ToString "yyyy.MM.dd")
                                }
                        | _ -> 
                            notificationSubject.OnNext 
                                    {
                                        About = cmd.Id
                                        Type = "Failure"
                                        Message = sprintf "didnt work %s " (cmd.Item.Date.ToString "yyyy.MM.dd")
                                    }
                rq.DeleteMessage msg

            let handleN (msg : Queue.CloudQueueMessage) = 
                            let json = msg.AsString
                            let notification = JsonConvert.DeserializeObject<Envelope<NotificationEvt>> json
                            notifications.Write notification                
                            nq.DeleteMessage msg
           
            System.Reactive.Linq.Observable.Interval(TimeSpan.FromSeconds 10.0)
            |> Observable.map (fun _ -> AzureQ.dequeue rq)
            |> Observable.choose id
            |> Observable.subscribeObserver (Observer.Create handleR)
            |> ignore

            System.Reactive.Linq.Observable.Interval(TimeSpan.FromSeconds 10.0)
            |> Observable.map (fun _ -> AzureQ.dequeue nq)
            |> Observable.choose id
            |> Observable.subscribeObserver (Observer.Create handleN)
            |> ignore
            
            LiveTracker.Infrastructure.Configure 
                reservations 
                notifications
                (Observer.Create (AzureQ.enqueue rq))
                seatingCapacity
                GlobalConfiguration.Configuration            
            
     