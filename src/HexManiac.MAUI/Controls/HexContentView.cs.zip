using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.MAUI.Controls {
   /// <summary>
   /// The central hex grid view. Replaces the WPF HexContent custom FrameworkElement.
   /// Renders using SkiaSharp for high-performance drawing on Android.
   ///
   /// Touch handling:
   ///   Tap              → move cursor
   ///   Long-press       → context menu
   ///   Swipe vertical   → scroll
   ///   Drag             → extend selection
   /// </summary>
   public class HexContentView : SKCanvasView {
      // ── Layout constants ──────────────────────────────────────────────────────
      public const float CellWidth  = 30f;
      public const float CellHeight = 20f;
      public const float FontSize   = 14f;
      public const float HeaderWidth = 50f; // left gutter showing row addresses

      // ── Bound view-port ───────────────────────────────────────────────────────
      private IViewPort _viewPort;
      public IViewPort ViewPort {
         get => _viewPort;
         set {
            if (_viewPort != null) {
               _viewPort.CollectionChanged -= OnDataChanged;
               _viewPort.PropertyChanged   -= OnPropertyChanged;
            }
            _viewPort = value;
            if (_viewPort != null) {
               _viewPort.CollectionChanged += OnDataChanged;
               _viewPort.PropertyChanged   += OnPropertyChanged;
               SizeViewPort();
            }
            InvalidateSurface();
         }
      }

      public HexContentView() {
         EnableTouchEvents = true;
         Touch += OnTouch;
         SizeChanged += (_, _) => SizeViewPort();
      }

      // ── Resize the viewmodel when the canvas resizes ──────────────────────────
      private void SizeViewPort() {
         if (_viewPort == null) return;
         int cols = Math.Max(1, (int)(Width  / CellWidth));
         int rows = Math.Max(1, (int)(Height / CellHeight));
         _viewPort.Width  = cols;
         _viewPort.Height = rows;
      }

      // ── Invalidate on data / property changes ─────────────────────────────────
      private void OnDataChanged(object sender, NotifyCollectionChangedEventArgs e) => InvalidateSurface();
      private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
         var triggers = new[] {
            nameof(IViewPort.ScrollValue),
            nameof(ViewPort.SelectionStart),
            nameof(ViewPort.SelectionEnd),
            nameof(ViewPort.UpdateInProgress),
         };
         if (triggers.Contains(e.PropertyName)) InvalidateSurface();
      }

      // ── SkiaSharp paint ───────────────────────────────────────────────────────
      protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
         base.OnPaintSurface(e);
         var canvas = e.Surface.Canvas;
         canvas.Clear(ParseThemeColor("#1E1E2E")); // Background

         if (_viewPort == null) return;

         float scale = (float)DeviceDisplay.MainDisplayInfo.Density;
         canvas.Scale(scale);  // HiDPI

         DrawHeaders(canvas);
         DrawCells(canvas);
         DrawSelection(canvas);
         DrawScrollbar(canvas);
      }

      private void DrawHeaders(SKCanvas canvas) {
         using var paint = new SKPaint { Color = new SKColor(0x45, 0x47, 0x5A), IsAntialias = true };
         using var font  = new SKFont(SKTypeface.FromFamilyName("monospace"), FontSize * 0.85f);

         // Column headers: 00 01 02 … (width - 1)
         for (int col = 0; col < _viewPort.Width; col++) {
            float x = HeaderWidth + col * CellWidth + CellWidth / 2f - 8f;
            canvas.DrawText($"{col:X2}", x, FontSize, font, paint);
         }

         // Row headers: absolute address
         for (int row = 0; row < _viewPort.Height; row++) {
            int addr = (_viewPort.ScrollValue + row) * _viewPort.Width;
            float y = CellHeight + row * CellHeight + CellHeight * 0.75f;
            canvas.DrawText($"{addr:X6}", 2f, y, font, paint);
         }
      }

      private void DrawCells(SKCanvas canvas) {
         if (_viewPort == null) return;
         var renderer = new SkiaHexRenderer(
            canvas, _viewPort,
            _viewPort.Width, _viewPort.Height,
            CellWidth, CellHeight, FontSize);

         float offsetX = HeaderWidth;
         float offsetY = CellHeight; // leave room for column headers
         canvas.Translate(offsetX, offsetY);

         for (int row = 0; row < _viewPort.Height; row++) {
            for (int col = 0; col < _viewPort.Width; col++) {
               if (row >= _viewPort.Count || col >= _viewPort[row].Count) continue;
               var cell = _viewPort[row][col];
               if (cell.Format == null) continue;

               renderer.Position = new ModelPoint(col, row);
               cell.Format.Visit(renderer, cell.Value);
            }
         }

         canvas.Translate(-offsetX, -offsetY);
      }

      private void DrawSelection(SKCanvas canvas) {
         if (_viewPort == null) return;
         var start = _viewPort.SelectionStart;
         var end   = _viewPort.SelectionEnd;
         if (start == end && start == new ModelPoint(-1, -1)) return;

         using var selPaint = new SKPaint {
            Color = new SKColor(0x89, 0xB4, 0xFA, 0x55),
            IsAntialias = false,
         };

         // Normalise start/end
         int startIndex = start.Y * _viewPort.Width + start.X;
         int endIndex   = end.Y   * _viewPort.Width + end.X;
         if (startIndex > endIndex) (startIndex, endIndex) = (endIndex, startIndex);

         for (int i = startIndex; i <= endIndex; i++) {
            int row = i / _viewPort.Width;
            int col = i % _viewPort.Width;
            float x = HeaderWidth + col * CellWidth;
            float y = CellHeight + row * CellHeight;
            canvas.DrawRect(x, y, CellWidth, CellHeight, selPaint);
         }
      }

      private void DrawScrollbar(SKCanvas canvas) {
         if (_viewPort == null) return;
         int total = _viewPort.MaximumScroll + _viewPort.Height;
         if (total <= _viewPort.Height) return;

         float canvasH = (float)Height;
         float thumbH  = canvasH * _viewPort.Height / total;
         float thumbY  = canvasH * _viewPort.ScrollValue / total;

         float scrollbarX = (float)Width - 6f;

         using var trackPaint = new SKPaint { Color = new SKColor(0x31, 0x32, 0x44) };
         using var thumbPaint = new SKPaint { Color = new SKColor(0x89, 0xB4, 0xFA, 0xAA), IsAntialias = true };

         canvas.DrawRect(scrollbarX, 0, 6, canvasH, trackPaint);
         canvas.DrawRoundRect(scrollbarX, thumbY, 6, thumbH, 3, 3, thumbPaint);
      }

      // ── Touch handling ────────────────────────────────────────────────────────
      private float _scrollStartY;
      private int _scrollStartValue;
      private bool _isDragging;

      private void OnTouch(object sender, SKTouchEventArgs e) {
         if (_viewPort == null) return;

         // Convert Skia coords → cell coords
         float density = (float)DeviceDisplay.MainDisplayInfo.Density;
         float logX = e.Location.X / density;
         float logY = e.Location.Y / density;

         int col = (int)((logX - HeaderWidth) / CellWidth);
         int row = (int)((logY - CellHeight)  / CellHeight);
         col = Math.Clamp(col, 0, _viewPort.Width  - 1);
         row = Math.Clamp(row, 0, _viewPort.Height - 1);

         switch (e.ActionType) {
            case SKTouchAction.Pressed:
               _scrollStartY     = e.Location.Y;
               _scrollStartValue = _viewPort.ScrollValue;
               _isDragging       = false;
               _viewPort.SelectionStart = new ModelPoint(col, row);
               _viewPort.SelectionEnd   = new ModelPoint(col, row);
               break;

            case SKTouchAction.Moved:
               float dy = (e.Location.Y - _scrollStartY) / density;
               if (Math.Abs(dy) > CellHeight / 2f) {
                  // Treat as a scroll gesture
                  _isDragging = true;
                  int delta = -(int)(dy / CellHeight);
                  _viewPort.ScrollValue = Math.Clamp(
                     _scrollStartValue + delta,
                     0, _viewPort.MaximumScroll);
               } else {
                  // Treat as a selection drag
                  _viewPort.SelectionEnd = new ModelPoint(col, row);
               }
               break;

            case SKTouchAction.Released:
               if (!_isDragging) {
                  // Tap: commit cursor position
                  _viewPort.SelectionStart = new ModelPoint(col, row);
                  _viewPort.SelectionEnd   = new ModelPoint(col, row);
               }
               break;
         }

         e.Handled = true;
         InvalidateSurface();
      }

      // ── Helpers ───────────────────────────────────────────────────────────────
      private static SKColor ParseThemeColor(string hex) {
         hex = hex.TrimStart('#');
         if (hex.Length == 6)
            return new SKColor(
               Convert.ToByte(hex[0..2], 16),
               Convert.ToByte(hex[2..4], 16),
               Convert.ToByte(hex[4..6], 16));
         return SKColors.Black;
      }
   }
}
