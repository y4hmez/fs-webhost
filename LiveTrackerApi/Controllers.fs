namespace LiveTracker

open System
open System.Net
open System.Net.Http
open System.Web.Http
//open System.Reactive.Subjects
open System.Reactive.Linq
open System.Reactive
//open FSharp.Reactive
//open FSharp.Reactive.Observable
open FSharp.Control
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open System.Reactive.Subjects


type HomeController() =
    inherit ApiController()
    member this.Get() = new HttpResponseMessage()

    
//this controller implments IObservable -> other types can subscribe to it and be notified when a rendition arrives.
///
/// Message Endpoint (to outside world)
/// Converts dto to real reservation
/// Wraps in Envelop
/// Publishes
///


type ReservationsController() =
    inherit ApiController()
    //let subject = new Subject<Envelope<ReservationCmd>>()
    let subject = new Subject<Envelope<ReservationCmd>>() 
    member this.Post (rendition : MakeReservationDto) =
        let (cmdMkRsvrtn : Envelope<ReservationCmd>) = 
            {
                ReservationCmd.Date = DateTime.Parse rendition.Date
                Name = rendition.Name
                Email = rendition.Email
                Quantity = rendition.Quantity
            } 
            |> WrapWithDefaults
        subject.OnNext cmdMkRsvrtn
        
        new HttpResponseMessage(HttpStatusCode.Accepted);
    interface IObservable<Envelope<ReservationCmd>> with 
        member this.Subscribe observer = subject.Subscribe observer
    override this.Dispose disposing =
        if disposing then subject.Dispose()
        base.Dispose disposing