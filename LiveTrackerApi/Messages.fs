namespace LiveTracker

open System

[<CLIMutable>]
type ReservationCmd =  {
    Date : DateTime  
    Name : string
    Email : string
    Quantity : int
}

[<CLIMutable>]
type ReservationEvt =  {
    Date : DateTime  
    Name : string
    Email : string
    Quantity : int
}

[<AutoOpen>]
module Envelope = 
    [<CLIMutable>]
    type Envelope<'T> = {
        Id : Guid
        Created : DateTimeOffset
        Item : 'T 
     }

    let Wrap id created item = {
        Id = id
        Created = created
        Item = item
    }

    let WrapWithDefaults item = 
        Wrap (Guid.NewGuid()) DateTimeOffset.Now item
