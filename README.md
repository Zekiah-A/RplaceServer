# RplaceServer
A modular, moddable and scalable server software designed to host rplace.tk canvases. Can be used standalone, GUI, or embedded inside of a client in order to allow for easy, no code self hosted game. Modular server library allows for setting up servers, and modifying game mechanics easily.

### Running from binary:
TKOfficial has been created as a terminal-based rplace server software implementation intended for user use.

If TKOfficial binaries have been provided on the [releases](https://github.com/Zekiah-A/RplaceServer/releases) page, download the appropriate one for your system.

Then, open a terminal, (use Powershell on windows), enter the rplace server directory with `cd Path/To/TKOfficial`

Then run the binary with `./EXECUTABLE NAME`

The first time you run the server, it should create a configuration, edit this in a text editor, and run the server again to apply and start the rplace game server.

### Running from source:
_To run from source, you need the latest dotnet version and git installed on your system._

Clone the repository with `git clone --recursive https://github.com/Zekiah-A/RplaceServer/` to include all submodules.

Once cloned, enter the root RplaceServer directory, with `cd RplaceServer` and ensure all submodules are updated with `git submodule update --remote`

Enter the TKOfficial directory, for example, with `cd TKOfficial`.

Run `dotnet run` to start the server. When running with SSL/on an admin restricted port, you may need to run this command with administrator privileges.

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
 - RplaceServer (Library with base rplace server functionality that can be implemented by a server software (not for use independently))
 - WatsonWebsocket(Zekiah-A Fork) Websocket library especially customised with cross platform compatibility, and security in mind.


## Developing:
### Events:
 - Events are distributed after an action is accepted, but before the server processes it. For example, when a pixel is placed, server will send out PixelPlacementReceived event, only after it has confirmed that pixel placement has been done after the cooldown period, or the IP has passed the banned checks, etc. If a packet is rejected before the server has handled it, no event will be called.
 - As events such as PixelPlacementReceived are called before the server has handled it (such as before the server has sent out the pixel and applied it to the canvas), by inhibiting the event from continuing in an event handler, the (for example) pixel placement will never go through.
 - Events with past tense names, such as CanvasBackupCreated, PlayerConnected and PlayerDisonnected are handled beforehand, and are uninhibitable alert-only events.

### Protocol:
 - Rplace uses many non-standard protocols in order to create the fastest and  most efficient server-client communication possible, allowing for a game instance to handle thousaneds of concurrent users.

Canvas:

 - The canvas is based on a 1D byte array. The length (in bytes) of the canvas transmitted during gameplay is equal to the width in pixels * height in pixels, with each byte in the byte array mapping to the index of a colour in the colour palette. This makes implementing your own canvas drawer really easy, for example, by doing 
    ```cs
    //Board is the byte array canvas.
    // We iterate through each byte in the canvas,
    // the first "canvas width" of indexes in the byte array
    // is the first row, the second is the second, etc
    for (var i = 0; i < board.Length; i++)
    {
        img.SetPixel
        (
            // X can be calculated by getting the iteration remainder
            // of the canvas width, as the first pixel on each row will
            // always have the same remainder index
            // (1 % 500 = 0, 100 % 500 = 0, 200 % 500 = 0)
            i % ParentSk.CanvasWidth,
            // Y can be calculated by dividing the iteration 
            // by the canvas width, This value may not always,
            // be a whole number so it is important to round or "floor"
            // this value to the nearest whole number.
            i / ParentSk.CanvasWidth,
            // We get the byte representing that board colour at the
            // index of our iteration, and then that is used as the index
            // of the palette colour that this byte represents to get
            // that pixel's colour. 
            Palette.Colours[board[i]] 
        );
    }
    ```
 - Place files that have been "backed up" (saved in the canvases folder) are formatted similarly, however, a 4 byte Uint16 is prepended to the end, representing the width of the canvas at that backup, as a canvas with a width of 10, and a height of 50 will have the same length as a canvas with a height of 10 and a width of 50. After the 4 byte Uint, there is a n bytes long list of Uint32s, this the palette of the canvas at that backup, the two final bytes of the board will represent a 2 byte Uint16, the value of that Uint16 describing how long the added "canvas width" and "palette data" was in bytes. All of this information prepended to the canvas is intended in order to make it much easier for a timelapse generator, canvas backup viewer, or other form of archiving tool get the necessary information to accurately reconstruct the canvas backup, without needing all canvas information at that point in time.
    ```
    [...,      XX, XX, XX, XX,    XX, XX, XX, XX, ...,  32 12]
      ^        ^  ^    ^   ^      ^    ^   ^   ^  ^     ^  ^
      canvas   The canvas width,  Palette colours,     Final length of
      bytes    4 byte Uint32.     Uint32 array.        the added metadata.
    ```

Chat:
 - There are two types of chat packets, live chat and place chat.

## Notes:
 - **[Known bug]** If you are facing issues with enabling TLS/SSL on the socket server, you can use a TlS/SSL proxy, such as NGINX to easily enable HTTPS functionality on the non-https server.
