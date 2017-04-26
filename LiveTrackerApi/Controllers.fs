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
        
        //new HttpResponseMessage(HttpStatusCode.Accepted);
        this.Request.CreateResponse (
            HttpStatusCode.Accepted, {
                Links =
                    [| {
                        Rel = "http://localhost:64741/notifications"
                        Href = "http://localhost:64741/notifications/" + cmdMkRsvrtn.Id.ToString "N" } |] })


    interface IObservable<Envelope<ReservationCmd>> with 
        member this.Subscribe observer = subject.Subscribe observer
    override this.Dispose disposing =
        if disposing then subject.Dispose()
        base.Dispose disposing

type NotificationsController (notifications : INotifications) =
    inherit ApiController()

    member this.Notifications = notifications

    member this.Get id = 
        let toDto (n : Envelope<NotificationEvt>) =  {
            About = n.Item.About.ToString()
            Type = n.Item.Type
            Message = n.Item.Message
        }
        let matches = 
            notifications.About id            
            |> Seq.map toDto
            |> Seq.toArray

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Notifications = matches }
        )

type AvailablityController(reservations : Reservations.IReservations, seatingCapacity : int) = 
    inherit ApiController()

    let getAvailableSeats map (now : DateTimeOffset) date = 
        if date < now.Date then 0
        elif map |> Map.containsKey date then
            seatingCapacity - (map |> Map.find date)
        else seatingCapacity

    let toMapOfDatesAndQuantities (min, max) reservations =
        reservations
        |> Reservations.Between min max
        |> Seq.groupBy(fun r -> r.Item.Date) 
        |> Seq.map (fun (d, rs) -> 
            (d, rs |> Seq.sumBy(fun r -> r.Item.Quantity)))
        |> Map.ofSeq

    let toOpening ((d : DateTime), seats) =
        { Date = d.ToString "yyyy.MM.dd"; Seats = seats }

    let getOpeningsIn period =
        let boundaries = Dates.BoundariesIn period
        let map = reservations |> toMapOfDatesAndQuantities boundaries
        let getAvailable  = getAvailableSeats map DateTimeOffset.Now
        
        let now = DateTimeOffset.Now        
        Dates.In period
        |> Seq.map (fun d -> (d, getAvailable d))
        |> Seq.map toOpening
        |> Seq.toArray
                            
    member this.Get year =

        let openings = getOpeningsIn(Year(year))

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get(year, month) = 

        let openings = getOpeningsIn(Month(year, month))
            
        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get(year, month, day) = 

        let openings = getOpeningsIn(Day(year, month, day))
        
        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })
        
        

    member this.SeatingCapacity = seatingCapacity







