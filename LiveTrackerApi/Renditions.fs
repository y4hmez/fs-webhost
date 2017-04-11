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

[<CLIMutable>]
type NotificationDto = {
    About : string
    Type : string
    Message : string
}

[<CLIMutable>]
type NottificationListDto = {
    Notifications : NotificationDto array
}

[<CLIMutable>]
type AtomLinkToNotification = {
    Rel : string
    Href : string
}

[<CLIMutable>]
type NotificationLinkList = {
    Links : AtomLinkToNotification array 
}
