using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using TKOfficialGUI.Utilities;


namespace TKOfficialGUI.Views;
public partial class SkCanvas : UserControl
{
    public int CanvasWidth = 1000;
    public int CanvasHeight = 1000;
    public Stack<Selection> Selections = new();
    
    private static byte[]? board;
    private static byte[]? changes;
    private static bool boardCached;
    private static SKImage? boardCache;

    public byte[]? Board
    {
        get => board;
        set
        {
            board = value;
            boardCached = false;
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
    }

    private byte[]? Changes
    {
        get => changes;
        set
        {
            changes = value;
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
    }
    
    public SkCanvas()
    {
        InitializeComponent();
        ClipToBounds = true;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private class CustomDrawOp : ICustomDrawOperation
    {
        private SkCanvas ParentSk { get; }
        public Rect Bounds { get; }
        
        private readonly SKPaint rplaceGrey = new() { Color = new SKColor(51, 51, 51, 100) };
        private readonly SKPaint rplaceOrange = new() { Color = new SKColor(255, 87, 0, 200) };
        private readonly SKPaint rplaceBlack = new() { Color = SKColors.Black };

        public CustomDrawOp(Rect bounds, SkCanvas parentSk)
        {
            Bounds = bounds;
            ParentSk = parentSk;
        }

        public void Dispose() { }
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        
        public void Render(IDrawingContextImpl context)
        {
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas == null) throw new Exception("[Fatal] Render Error: SkCanvas was null, perhaps not using skia as render backend?");
            canvas.Save();
            
            //Equivalent of renderAll
            if (!boardCached && board is not null)
            {
                using var img = new SKBitmap(ParentSk.CanvasWidth, ParentSk.CanvasHeight, true);
                
                for (var i = 0; i < board.Length; i++)
                {
                    img.SetPixel
                    (
                        i % ParentSk.CanvasWidth,
                        i / ParentSk.CanvasWidth,
                        StandardPalette.Colours[board[i]]
                    );
                }
                
                boardCache = SKImage.FromBitmap(img);
                boardCached = true;
            }
            
            if (boardCached && boardCache is not null)
            {
                canvas.DrawImage(boardCache, 0, 0);
            }
            else
            {
                //Draw rplacetk logo background instead
                canvas.DrawRect(0, 0, 500, 500, rplaceGrey); //background
                canvas.DrawRect(74, 74, 280, 70, rplaceOrange); //top
                canvas.DrawRect(74, 144, 70, 280, rplaceOrange); //left
                canvas.DrawRect(354, 144, 70, 280, rplaceOrange); //right
                canvas.DrawRect(214, 354, 140, 70, rplaceOrange); //bottom
                canvas.DrawRect(214, 214, 72, 72, rplaceBlack); //centre
            }
            
            //Draw live pixels
            if (changes is not null)
            {
                for (var c = 0; c < changes.Length; c++)
                {
                    canvas.DrawRect
                    (
                        c % ParentSk.CanvasWidth,
                        c / ParentSk.CanvasWidth,
                        1,
                        1,
                        new SKPaint { Color = StandardPalette.SkiaColours[changes[c]] }
                    );
                }
            }

            //Draw selections
            foreach (var sel in ParentSk.Selections)
            {
                var sKBrush = new SKPaint();
                sKBrush.Color = new SKColor(100, 167, 255, 140);
                canvas.DrawRect
                (
                    (float) Math.Floor(sel.Tl.X),
                    (float) Math.Floor(sel.Tl.Y),
                    (float) Math.Floor(sel.Br.X),
                    (float) Math.Floor(sel.Br.Y),
                    sKBrush
                );
            }
            
            canvas.Flush();
            canvas.Restore();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), this));
    }
    
    public void StartSelection(Point topLeft, Point bottomRight)
    {
        var sel = new Selection
        {
            Tl = topLeft,
            Br = bottomRight
        };
        Selections.Push(sel);
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    public void UpdateSelection(Point? topLeft = null, Point? bottomRight = null)
    {
        var cur = Selections.Pop();
        cur.Tl = topLeft ?? cur.Tl;
        cur.Br = bottomRight ?? cur.Br;
        Selections.Push(cur);
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    public void ClearSelections()
    {
        Selections = new Stack<Selection>();
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    public void AddPixel(int x, int y, int colour)
    {
        Changes ??= new byte[CanvasWidth * CanvasHeight];
        Changes[x % CanvasWidth + y % CanvasHeight * CanvasWidth] = (byte) colour;
    }
}

public struct Selection
{
    public Point Tl;
    public Point Br;
}