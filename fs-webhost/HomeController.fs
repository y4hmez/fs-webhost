namespace Webhost

open System
open System.Web.Http

type HomeController() =
    inherit ApiController()
    member this.Get() =
        ()