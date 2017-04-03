namespace LiveTracker

open System

//this is the message outide the boundary of the system...

[<CLIMutable>]  //allow normal serializers and deserializers
type MakeReservationDto = {
    Date : string
    Name : string
    Email : string
    Quantity : int 
}

