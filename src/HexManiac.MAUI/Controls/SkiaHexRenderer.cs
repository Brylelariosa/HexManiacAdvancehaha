using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using SkiaSharp;
using System;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;
// Alias to disambiguate from System.Tuple
using DataTuple = HavenSoft.HexManiac.Core.ViewModels.DataFormats.Tuple;

namespace HavenSoft.HexManiac.MAUI.Controls {
   /// <summary>
   /// Renders hex data cells onto a SkiaSharp canvas.
   /// This is the MAUI equivalent of the WPF FormatDrawer class.
   ///
   /// Each byte of the ROM is drawn as a 2-hex-digit cell with colour coding
   /// that matches the WPF version:
   ///   White          = raw / None
   ///   Sky blue       = Pointer
   ///   Green          = PCS text string
   ///   Orange         = Integer enum
   ///   Red            = Error
   ///   Purple         = Palette colour
   ///   Italic grey    = Undefined / out of range
   /// </summary>
   public class SkiaHexRenderer : IDataFormatVisitor {
      // ── Colours (default dark theme, overwritten via UpdateTheme) ────────────
      private static SKColor ColorText     = new(0xCD, 0xD6, 0xF4); // text1
      private static SKColor ColorPointer  = new(0x89, 0xB4, 0xFA); // secondary / blue
      private static SKColor ColorString   = new(0xA6, 0xE3, 0xA1); // data1 / green
      private static SKColor ColorEnum     = new(0xFA, 0xB3, 0x87); // stream1 / orange
      private static SKColor ColorError    = new(0xF3, 0x8B, 0xA8); // error / red
      private static SKColor ColorPalette  = new(0xCA, 0x9E, 0xE6); // accent / purple
      private static SKColor ColorUndefined= new(0x58, 0x5B, 0x70); // dimmed grey
      private static SKColor ColorSelected = new(0x31, 0x32, 0x44, 0xCC); // selection overlay

      public static void UpdateTheme(HavenSoft.HexManiac.Core.ViewModels.Theme t) {
         ColorText      = ParseColor(t.Text1,    ColorText);
         ColorPointer   = ParseColor(t.Secondary, ColorPointer);
         ColorString    = ParseColor(t.Data1,     ColorString);
         ColorEnum      = ParseColor(t.Stream1,   ColorEnum);
         ColorError     = ParseColor(t.Error,     ColorError);
         ColorPalette   = ParseColor(t.Accent,    ColorPalette);
      }

      private static SKColor ParseColor(string hex, SKColor fallback) {
         if (string.IsNullOrEmpty(hex)) return fallback;
         try {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
               return new SKColor(
                  Convert.ToByte(hex[0..2], 16),
                  Convert.ToByte(hex[2..4], 16),
                  Convert.ToByte(hex[4..6], 16));
         } catch { }
         return fallback;
      }

      // ── Drawing state ─────────────────────────────────────────────────────────
      private readonly SKCanvas canvas;
      private readonly IViewPort viewPort;
      private readonly float cellWidth, cellHeight;
      private readonly int modelWidth, modelHeight;
      private readonly SKFont font;
      private readonly SKFont fontSmall;
      private readonly SKPaint fillPaint   = new() { IsAntialias = true };
      private readonly SKPaint selPaint    = new() { IsAntialias = false, Color = ColorSelected };

      private ModelPoint _position;
      public ModelPoint Position {
         get => _position;
         set {
            _position = value;
            // When X resets to 0 we've moved to a new row — nothing extra needed in Skia.
         }
      }

      public SkiaHexRenderer(
         SKCanvas canvas, IViewPort viewPort,
         int modelWidth, int modelHeight,
         float cellWidth, float cellHeight,
         float fontSize)
      {
         this.canvas      = canvas;
         this.viewPort    = viewPort;
         this.modelWidth  = modelWidth;
         this.modelHeight = modelHeight;
         this.cellWidth   = cellWidth;
         this.cellHeight  = cellHeight;

         // Build monospace font
         font      = new SKFont(SKTypeface.FromFamilyName("monospace"), fontSize);
         fontSmall = new SKFont(SKTypeface.FromFamilyName("monospace"), fontSize * 0.75f);
      }

      // ── IDataFormatVisitor implementation ─────────────────────────────────────

      public void Visit(None dataFormat, byte data)
         => DrawHex(data, dataFormat.IsSearchResult ? ColorEnum : ColorText);

      public void Visit(Undefined dataFormat, byte data)
         => DrawHex(data, ColorUndefined, italic: true);

      public void Visit(UnderEdit dataFormat, byte data) {
         DrawBackground(new SKColor(0x31, 0x32, 0x44));
         DrawText(dataFormat.CurrentText, ColorText);
      }

      public void Visit(Pointer pointer, byte data) => DrawHex(data, ColorPointer);

      public void Visit(Anchor anchor, byte data)   => anchor.OriginalFormat.Visit(this, data);

      public void Visit(SpriteDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data)         => DrawText(pcs.ThisCharacter, ColorString);

      public void Visit(EscapedPCS pcs, byte data)  => DrawHex(data, ColorString);

      public void Visit(ErrorPCS pcs, byte data)    => DrawHex(data, ColorError);

      public void Visit(Ascii ascii, byte data)     => DrawText(((char)data).ToString(), ColorString);

      public void Visit(Braille braille, byte data) => DrawText(braille.ThisCharacter, ColorString);

      public void Visit(Integer integer, byte data) => DrawHex(data, ColorText);

      public void Visit(IntegerEnum intEnum, byte data) {
         var text = intEnum.DisplayValue;
         if (text != null && text.Length > 4) text = text[..4];
         DrawText(text ?? $"{data:X2}", ColorEnum, small: true);
      }

      public void Visit(IntegerHex intHex, byte data) => DrawHex(data, ColorEnum);

      public void Visit(EggSection section, byte data) => DrawText(section.SectionName[..Math.Min(4, section.SectionName.Length)], ColorString, small: true);

      public void Visit(EggItem item, byte data)    => DrawText(item.ItemName[..Math.Min(4, item.ItemName.Length)], ColorString, small: true);

      public void Visit(PlmItem item, byte data)    => DrawHex(data, ColorString);

      public void Visit(BitArray array, byte data)  => DrawHex(data, ColorEnum);

      public void Visit(MatchedWord word, byte data) => DrawHex(data, ColorPointer);

      public void Visit(EndStream stream, byte data) => DrawHex(data, ColorUndefined);

      public void Visit(LzMagicIdentifier lz, byte data) => DrawText("LZ", ColorEnum);

      public void Visit(LzGroupHeader lz, byte data)     => DrawHex(data, ColorEnum);

      public void Visit(LzCompressed lz, byte data)      => DrawHex(data, ColorPointer);

      public void Visit(LzUncompressed lz, byte data)    => DrawHex(data, ColorText);

      public void Visit(UncompressedPaletteColor color, byte data) {
         // Draw a small colour swatch
         var cellRect = CellRect();
         short raw = (short)(color.R | (color.G << 5) | (color.B << 10));
         byte r = (byte)(color.R << 3), g = (byte)(color.G << 3), b = (byte)(color.B << 3);
         fillPaint.Color = new SKColor(r, g, b);
         canvas.DrawRect(cellRect.Inflate(-2, -2), fillPaint);
      }

      public void Visit(DataTuple tuple, byte data) => DrawHex(data, ColorEnum);

      // ── Drawing helpers ───────────────────────────────────────────────────────

      private SKRect CellRect() {
         float x = _position.X * cellWidth;
         float y = _position.Y * cellHeight;
         return new SKRect(x, y, x + cellWidth, y + cellHeight);
      }

      private void DrawBackground(SKColor color) {
         fillPaint.Color = color;
         canvas.DrawRect(CellRect(), fillPaint);
      }

      private void DrawHex(byte data, SKColor color, bool italic = false) {
         DrawText($"{data:X2}", color, italic: italic);
      }

      private void DrawText(string text, SKColor color, bool small = false, bool italic = false) {
         var f = small ? fontSmall : font;
         f.Embolden = false;
         f.SkewX = italic ? -0.25f : 0f;

         fillPaint.Color = color;

         // Measure and centre
         float textWidth = f.MeasureText(text);
         float x = _position.X * cellWidth + (cellWidth - textWidth) / 2f;
         float y = _position.Y * cellHeight + (cellHeight + f.Size) / 2f - 2f;

         canvas.DrawText(text, x, y, f, fillPaint);
      }
   }
}
