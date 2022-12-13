# RplaceServer
A modular, moddable and scalable server software designed to host rplace.tk canvases. Can be used standalone, GUI, or embedded inside of a client in order to allow for easy, no code self hosted game. Modular server library allows for setting up servers, and modifying game mechanics easily.

### Project Aims:
 - Open, contributable and easy to use
 - Faster than legacy node server
 - Moddable and easily extendable
 - Daemonisable
 - Linux shell scripts such as used in board shrinking made cross platform and built in
 - Library makes use of events and abstract methods to allow customisation of server functions
 - Compatible with both GUI, and CLI (Nephrite)
 - Decoupled unlike legacy node server, abstracted and simplified
 - Scalable

### What's here:
 - .TK Official (Official CLI server software implementation, for spinning up single canvas/instance servers)
 - .TK GUI (Offial GUI server software implementation, with multiple server instances/canvases in mind)
 - RplaceServer (Library with base rplace server functionality that can be implemented by a server software (not for use independently))
 - WatsonWebsocket(Zekiah-A Fork) Websocket library especially customised with cross platform compatibility, and security in mind.


## Developing:
### Events:
 - Events are distributed after an action is accepted, but before the server processes it. For example, when a pixel is placed, server will send out PixelPlacementReceived event, only after it has confirmed that pixel placement has been done after the cooldown period, or the IP has passed the banned checks, etc. If a packet is rejected before the server has handled it, no event will be called.
 - As events such as PixelPlacementReceived are called before the server has handled it (such as before the server has sent out the pixel and applied it to the canvas), by inhibiting the event from continuing in an event handler, the (for example) pixel placement will never go through.
 - Events with past tense names, such as CanvasBackupCreated, PlayerConnected and PlayerDisonnected are handled beforehand, and are uninhibitable alert-only events.
