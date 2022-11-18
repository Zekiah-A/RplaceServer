using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace TkOfficialGUI.Utilities;

//TODO: This is temporary.
public static class StandardPalette
{
    public static List<SKColor> SkiaColours => new(Colours.Select(item => new SKColor(item.Red, item.Green, item.Blue)));

    public static readonly SKColor[] Colours =
    {
        new(109, 0, 26),
        new(190, 0, 57),
        new(255, 69, 0),
        new(255, 168, 0),
        new(255, 214, 53),
        new(255, 248, 184),
        new(0, 163, 104),
        new(0, 204, 120),
        new(126, 237, 86),
        new(0, 117, 111),
        new(0, 158, 170),
        new(0, 204, 192),
        new(36, 80, 164),
        new(54, 144, 234),
        new(81, 233, 244),
        new(73, 58, 193),
        new(106, 92, 255),
        new(148, 179, 255),
        new(129, 30, 159),
        new(180, 74, 192),
        new(228, 171, 255),
        new(222, 16, 127),
        new(255, 56, 129),
        new(255, 153, 170),
        new(109, 72, 47),
        new(156, 105, 38),
        new(255, 180, 112),
        new(0, 0, 0),
        new(81, 82, 82),
        new(137, 141, 144),
        new(212, 215, 217),
        new(255, 255, 255)
    };
}