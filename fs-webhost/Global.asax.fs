namespace Webhost

open System
open System.Collections.Concurrent
open System.Web.Http
open LiveTracker
//open LiveTracker.Infrastructure
open LiveTracker.Reservations
open System.Reactive
//open FSharp.Reactive
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable


type Agent<'T> = MailboxProcessor<'T>

type HttpRouteDefaults = { Controller : string; Id : obj }  

type Global() =
    inherit Web.HttpApplication()
    member this.Application_Start (sender :obj) (e : EventArgs) = 
            let seatingCapacity = 10

            let reservations = ConcurrentBag<Envelope<ReservationEvt>>()                        
            let reservationSubject = new Subjects.Subject<Envelope<ReservationEvt>>()
            reservationSubject.Subscribe reservations.Add |> ignore

            let notifications = ConcurrentBag<Envelope<NotificationEvt>>()
            let notificationSubject = new Subjects.Subject<NotificationEvt>()
            notificationSubject
            |> Observable.map WrapWithDefaults
            |> Observable.subscribeWithCallbacks notifications.Add ignore ignore
            |> ignore
            
            let agent = new Agent<Envelope<ReservationCmd>>(fun inbox ->
                let rec loop () =
                    async {
                        let! cmd = inbox.Receive()
                        let rs = reservations |> Reservations.ToReservations
                        let handle = Reservations.Handle seatingCapacity rs
                        let newReservations = handle cmd
                        match newReservations with
                        | Some(r) -> 
                            reservationSubject.OnNext r //this is thing that actually adds the reservation - if it has it to the collection of reservation evts.
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
                        return! loop() }
                loop())
            do agent.Start()
            let reservationRequestObserver = Observer.Create agent.Post //create the observer that will post the requests (booking request cmds) to the agent

            LiveTracker.Infrastructure.Configure (reservations |> ToReservations )  reservationRequestObserver GlobalConfiguration.Configuration            
            
            



