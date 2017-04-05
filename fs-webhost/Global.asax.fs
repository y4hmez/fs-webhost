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
            //let reservations = (ConcurrentBag<Envelope<ReservationEvt>>()) :> IReservations
            let reservationSubject = new Subjects.Subject<Envelope<ReservationEvt>>()
            reservationSubject.Subscribe reservations.Add |> ignore

            let agent = new Agent<Envelope<ReservationCmd>>(fun inbox ->
                let rec loop () =
                    async {
                        let! cmd = inbox.Receive()
                        let rs = reservations |> Reservations.ToReservations
                        let handle = Reservations.Handle seatingCapacity rs
                        let newReservations = handle cmd
                        match newReservations with
                        | Some(r) -> reservationSubject.OnNext r
                        | _ -> ()
                        return! loop() }
                loop())
            do agent.Start()
            let reservationRequestObserver = Observer.Create agent.Post

            LiveTracker.Infrastructure.Configure (reservations |> ToReservations )  reservationRequestObserver GlobalConfiguration.Configuration            
            
            



