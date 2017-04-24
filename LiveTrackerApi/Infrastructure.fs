module LiveTracker.Infrastructure

open System
open System.Net.Http
open System.Web.Http
open System.Web.Http.Dispatcher
open System.Web.Http.Controllers
open System.Reactive
open FSharp.Control.Reactive
//FSharp.Reactive
open LiveTracker.Reservations

type CompositionRoot(reservations : IReservations, notifications,  reservationRequestObserver, seatingCapacity) =
            
    interface IHttpControllerActivator with 
        member this.Create(request, controllerDescriptor, controllerType) =
            if controllerType = typeof<HomeController> then
                new HomeController() :> IHttpController
            elif controllerType = typeof<ReservationsController> then
                let c = new ReservationsController()                 
                c
                |> Observable.subscribeObserver reservationRequestObserver //at the controller implements IObservable - here were subcribing the observer to lit.
                |> request.RegisterForDispose
                c :> IHttpController
            elif controllerType = typeof<NotificationsController> then
                new NotificationsController(notifications) :> IHttpController
            elif controllerType = typeof<AvailablityController> then
                new AvailablityController(reservations, seatingCapacity) :> IHttpController
            else 
                raise 
                <| ArgumentException(
                    sprintf "Unknown controller type requested: %O" controllerType, "Controller Type")


type HttpRouteDefaults = { Controller : string; Id : obj }  

let ConfigureServices reservations notifications reservationRequestObserver seatingCapacity (config : HttpConfiguration) = 
    config.Services.Replace(
        typeof<IHttpControllerActivator>,CompositionRoot(reservations, notifications, reservationRequestObserver, seatingCapacity)
    )
    
let ConfigureRoutes (config : HttpConfiguration) =
    config.Routes.MapHttpRoute(
        "AvailabilityYear",
        "availability/{year}",
        { Controller = "Availability"; Id = RouteParameter.Optional }) |> ignore        

    config.Routes.MapHttpRoute(
            "AvailabilityMonth",
            "availability/{year}/{month}",
            { Controller = "Availability"; Id = RouteParameter.Optional }) |> ignore        

    config.Routes.MapHttpRoute(
            "AvailabilityDay",
            "availability/{year}/{month}/{day}",
            { Controller = "Availablity"; Id = RouteParameter.Optional }) |> ignore        

    config.Routes.MapHttpRoute(
            "DefaultAPI",
            "{controller}/{id}",
            { Controller = "Home"; Id = RouteParameter.Optional } 
        ) |> ignore

let ConfigureFormatting (config : HttpConfiguration) =
    config.Formatters.JsonFormatter.SerializerSettings.ContractResolver <- Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
    
let  Configure 
        reservations
        notifications
        reservationRequestObserver
        seatingCapacity
        config = 
    ConfigureRoutes config
    ConfigureServices  reservations notifications reservationRequestObserver seatingCapacity config
    ConfigureFormatting config
