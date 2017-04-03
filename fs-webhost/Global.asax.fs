namespace Webhost

open System
open System.Web.Http
open LiveTracker.Infrastructure


type HttpRouteDefaults = { Controller : string; Id : obj }  

type Global() =
    inherit System.Web.HttpApplication()
    member this.Application_Start (sender :obj) (e : EventArgs) = 
            Configure GlobalConfiguration.Configuration
            
            



