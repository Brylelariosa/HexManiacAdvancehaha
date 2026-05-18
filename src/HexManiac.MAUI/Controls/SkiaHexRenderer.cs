using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using SkiaSharp;
using System;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.MAUI.Controls {
   /// <summary>
   /// Renders hex data cells onto a SkiaSharp canvas.
   /// Implements IDataFormatVisitor — each Visit() call draws one cell.
   /// </summary>
   public class SkiaHexRenderer : IDataFormatVisitor {
      // Default dark-theme colours; overwritten by UpdateTheme()
      private static SKColor ColorText      = new(0xCD, 0xD6, 0xF4);
      private static SKColor ColorPointer   = new(0x89, 0xB4, 0xFA);
      private static SKColor ColorString    = new(0xA6, 0xE3, 0xA1);
      private static SKColor ColorEnum      = new(0xFA, 0xB3, 0x87);
      private static SKColor ColorError     = new(0xF3, 0x8B, 0xA8);
      private static SKColor ColorPalette   = new(0xCA, 0x9E, 0xE6);
      private static SKColor ColorUndefined = new(0x58, 0x5B, 0x70);

      public static void UpdateTheme(Theme t) {
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

      private readonly SKCanvas canvas;
      private readonly float cellWidth, cellHeight;
      private readonly SKPaint paint = new() { IsAntialias = true };
      private readonly SKFont font;
      private readonly SKFont fontSmall;

      public ModelPoint Position { get; set; }

      public SkiaHexRenderer(SKCanvas canvas, float cellWidth, float cellHeight, float fontSize) {
         this.canvas     = canvas;
         this.cellWidth  = cellWidth;
         this.cellHeight = cellHeight;
         font      = new SKFont(SKTypeface.FromFamilyName("monospace"), fontSize);
         fontSmall = new SKFont(SKTypeface.FromFamilyName("monospace"), fontSize * 0.75f);
      }

      // ── IDataFormatVisitor ───────────────────────────────────────────────────

      public void Visit(None dataFormat, byte data)
         => DrawHex(data, dataFormat.IsSearchResult ? ColorEnum : ColorText);

      public void Visit(Undefined dataFormat, byte data)
         => DrawHex(data, ColorUndefined, italic: true);

      public void Visit(UnderEdit dataFormat, byte data) {
         DrawBackground(new SKColor(0x31, 0x32, 0x44));
         DrawText(dataFormat.CurrentText, ColorText);
      }

      public void Visit(Pointer pointer, byte data)               => DrawHex(data, ColorPointer);
      public void Visit(Anchor anchor, byte data)                  => anchor.OriginalFormat.Visit(this, data);
      public void Visit(SpriteDecorator decorator, byte data)     => decorator.OriginalFormat.Visit(this, data);
      public void Visit(StreamEndDecorator decorator, byte data)  => decorator.OriginalFormat.Visit(this, data);
      public void Visit(PCS pcs, byte data)                       => DrawText(pcs.ThisCharacter.ToString(), ColorString);
      public void Visit(EscapedPCS pcs, byte data)                => DrawHex(data, ColorString);
      public void Visit(ErrorPCS pcs, byte data)                  => DrawHex(data, ColorError);
      public void Visit(Ascii ascii, byte data)                   => DrawText(((char)data).ToString(), ColorString);
      public void Visit(Braille braille, byte data)               => DrawText(braille.ThisCharacter.ToString(), ColorString);
      public void Visit(Integer integer, byte data)               => DrawHex(data, ColorText);
      public void Visit(IntegerEnum intEnum, byte data) {
         var text = intEnum.DisplayValue ?? $"{data:X2}";
         if (text.Length > 4) text = text[..4];
         DrawText(text, ColorEnum, small: true);
      }
      public void Visit(IntegerHex intHex, byte data)             => DrawHex(data, ColorEnum);
      public void Visit(EggSection section, byte data) {
         var name = section.SectionName;
         DrawText(name.Length > 4 ? name[..4] : name, ColorString, small: true);
      }
      public void Visit(EggItem item, byte data) {
         var name = item.ItemName;
         DrawText(name.Length > 4 ? name[..4] : name, ColorString, small: true);
      }
      public void Visit(PlmItem item, byte data)                  => DrawHex(data, ColorString);
      public void Visit(BitArray array, byte data)                => DrawHex(data, ColorEnum);
      public void Visit(MatchedWord word, byte data)              => DrawHex(data, ColorPointer);
      public void Visit(EndStream stream, byte data)              => DrawHex(data, ColorUndefined);
      public void Visit(LzMagicIdentifier lz, byte data)         => DrawText("LZ", ColorEnum);
      public void Visit(LzGroupHeader lz, byte data)             => DrawHex(data, ColorEnum);
      public void Visit(LzCompressed lz, byte data)              => DrawHex(data, ColorPointer);
      public void Visit(LzUncompressed lz, byte data)            => DrawHex(data, ColorText);
      public void Visit(UncompressedPaletteColor color, byte data) {
         byte r = (byte)(color.R << 3), g = (byte)(color.G << 3), b = (byte)(color.B << 3);
         paint.Color = new SKColor(r, g, b);
         var rect = CellRect();
         canvas.DrawRect(rect.Left + 2, rect.Top + 2, rect.Width - 4, rect.Height - 4, paint);
      }
      public void Visit(Tuple tuple, byte data)                   => DrawHex(data, ColorEnum);

      // ── Drawing helpers ───────────────────────────────────────────────────────

      private SKRect CellRect() {
         float x = Position.X * cellWidth;
         float y = Position.Y * cellHeight;
         return new SKRect(x, y, x + cellWidth, y + cellHeight);
      }

      private void DrawBackground(SKColor color) {
         paint.Color = color;
         canvas.DrawRect(CellRect(), paint);
      }

      private void DrawHex(byte data, SKColor color, bool italic = false)
         => DrawText($"{data:X2}", color, italic: italic);

      private void DrawText(string text, SKColor color, bool small = false, bool italic = false) {
         var f = small ? fontSmall : font;
         f.SkewX = italic ? -0.25f : 0f;
         paint.Color = color;
         float textWidth = f.MeasureText(text);
         float x = Position.X * cellWidth + (cellWidth - textWidth) / 2f;
         float y = Position.Y * cellHeight + (cellHeight + f.Size) / 2f - 2f;
         canvas.DrawText(text, x, y, SKTextAlign.Left, f, paint);
      }
   }
}
