using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using SkiaSharp;
using System.Runtime.InteropServices;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.MAUI.Controls {
   /// <summary>
   /// SkiaSharp canvas that renders the hex grid and handles touch input.
   /// Uses IEditableViewPort (not IViewPort) because SelectionStart/SelectionEnd
   /// and IsSelected() are on IEditableViewPort.
   /// </summary>
   public class HexContentView : SKCanvasView {
      public const float CellWidth   = 30f;
      public const float CellHeight  = 20f;
      public const float FontSize    = 13f;
      public const float HeaderWidth = 52f;
      public const float HeaderHeight= 18f;

      // ── Bound view-port ───────────────────────────────────────────────────────
      private IEditableViewPort _viewPort;
      public IEditableViewPort ViewPort {
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
         Touch      += OnTouch;
         SizeChanged += (_, _) => SizeViewPort();
      }

      private void SizeViewPort() {
         if (_viewPort == null) return;
         int cols = Math.Max(1, (int)((Width  - HeaderWidth)  / CellWidth));
         int rows = Math.Max(1, (int)((Height - HeaderHeight) / CellHeight));
         _viewPort.Width  = cols;
         _viewPort.Height = rows;
      }

      private void OnDataChanged(object s, NotifyCollectionChangedEventArgs e) => InvalidateSurface();
      private void OnPropertyChanged(object s, PropertyChangedEventArgs e) {
         if (e.PropertyName is nameof(IViewPort.ScrollValue)
            or nameof(IEditableViewPort.SelectionStart)
            or nameof(IEditableViewPort.SelectionEnd)
            or nameof(IViewPort.UpdateInProgress))
            InvalidateSurface();
      }

      // ── Paint ─────────────────────────────────────────────────────────────────
      protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
         base.OnPaintSurface(e);
         var canvas = e.Surface.Canvas;
         canvas.Clear(new SKColor(0x1E, 0x1E, 0x2E));
         if (_viewPort == null) return;

         float scale = (float)(e.Info.Width / Width);
         canvas.Save();
         canvas.Scale(scale);

         DrawColumnHeaders(canvas);
         DrawRowHeaders(canvas);
         DrawCells(canvas);
         DrawSelection(canvas);
         DrawScrollbar(canvas);

         canvas.Restore();
      }

      private void DrawColumnHeaders(SKCanvas canvas) {
         using var paint = new SKPaint { Color = new SKColor(0x45, 0x47, 0x5A), IsAntialias = true };
         using var font  = new SKFont(SKTypeface.FromFamilyName("monospace"), FontSize * 0.8f);
         for (int col = 0; col < _viewPort.Width; col++) {
            float x = HeaderWidth + col * CellWidth + (CellWidth - font.MeasureText($"{col % 16:X}")) / 2f;
            var _colStr = $"{col % 16:X}"; canvas.DrawText(MemoryMarshal.Cast<char, ushort>(_colStr.AsSpan()), x, FontSize, font, paint);
         }
      }

      private void DrawRowHeaders(SKCanvas canvas) {
         using var paint = new SKPaint { Color = new SKColor(0x45, 0x47, 0x5A), IsAntialias = true };
         using var font  = new SKFont(SKTypeface.FromFamilyName("monospace"), FontSize * 0.75f);
         for (int row = 0; row < _viewPort.Height; row++) {
            int addr = (_viewPort.ScrollValue + row) * _viewPort.Width;
            float y = HeaderHeight + row * CellHeight + CellHeight * 0.72f;
            var _addrStr = $"{addr:X6}"; canvas.DrawText(MemoryMarshal.Cast<char, ushort>(_addrStr.AsSpan()), 2f, y, font, paint);
         }
      }

      private void DrawCells(SKCanvas canvas) {
         canvas.Save();
         canvas.Translate(HeaderWidth, HeaderHeight);
         var renderer = new SkiaHexRenderer(canvas, CellWidth, CellHeight, FontSize);
         for (int row = 0; row < _viewPort.Height; row++) {
            for (int col = 0; col < _viewPort.Width; col++) {
               var cell = _viewPort[col, row];
               if (cell?.Format == null) continue;
               renderer.Position = new ModelPoint(col, row);
               cell.Format.Visit(renderer, cell.Value);
            }
         }
         canvas.Restore();
      }

      private void DrawSelection(SKCanvas canvas) {
         if (_viewPort == null) return;
         using var selPaint = new SKPaint { Color = new SKColor(0x89, 0xB4, 0xFA, 0x55), IsAntialias = false };
         for (int row = 0; row < _viewPort.Height; row++) {
            for (int col = 0; col < _viewPort.Width; col++) {
               if (!_viewPort.IsSelected(new ModelPoint(col, row))) continue;
               float x = HeaderWidth + col * CellWidth;
               float y = HeaderHeight + row * CellHeight;
               canvas.DrawRect(x, y, CellWidth, CellHeight, selPaint);
            }
         }
      }

      private void DrawScrollbar(SKCanvas canvas) {
         int total = _viewPort.MaximumScroll + _viewPort.Height;
         if (total <= _viewPort.Height) return;
         float canvasH = (float)Height;
         float thumbH  = canvasH * _viewPort.Height / total;
         float thumbY  = canvasH * _viewPort.ScrollValue / total;
         float scrollX = (float)Width - 6f;
         using var track = new SKPaint { Color = new SKColor(0x31, 0x32, 0x44) };
         using var thumb = new SKPaint { Color = new SKColor(0x89, 0xB4, 0xFA, 0xAA), IsAntialias = true };
         canvas.DrawRect(scrollX, 0, 6, canvasH, track);
         canvas.DrawRoundRect(scrollX, thumbY, 6, thumbH, 3, 3, thumb);
      }

      // ── Touch ─────────────────────────────────────────────────────────────────
      private float _scrollStartY;
      private int   _scrollStartVal;
      private bool  _isDragging;

      private void OnTouch(object sender, SKTouchEventArgs e) {
         if (_viewPort == null) return;
         float density = (float)Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo.Density;

         float logX = e.Location.X / density;
         float logY = e.Location.Y / density;
         int col = Math.Clamp((int)((logX - HeaderWidth)  / CellWidth),  0, _viewPort.Width  - 1);
         int row = Math.Clamp((int)((logY - HeaderHeight) / CellHeight), 0, _viewPort.Height - 1);

         switch (e.ActionType) {
            case SKTouchAction.Pressed:
               _scrollStartY   = e.Location.Y;
               _scrollStartVal = _viewPort.ScrollValue;
               _isDragging     = false;
               MoveSelectionTo(col, row);
               break;

            case SKTouchAction.Moved:
               float dy = (e.Location.Y - _scrollStartY) / density;
               if (Math.Abs(dy) > CellHeight * 0.5f) {
                  _isDragging = true;
                  int delta = -(int)(dy / CellHeight);
                  _viewPort.ScrollValue = Math.Clamp(_scrollStartVal + delta, 0, _viewPort.MaximumScroll);
               }
               break;

            case SKTouchAction.Released:
               if (!_isDragging)
                  MoveSelectionTo(col, row);
               break;
         }
         e.Handled = true;
         InvalidateSurface();
      }

      private void MoveSelectionTo(int col, int row) {
         // Convert grid position to ROM address and set via SelectedAddress (string setter on IViewPort)
         int addr = (_viewPort.ScrollValue + row) * _viewPort.Width + col;
         _viewPort.SelectedAddress = addr.ToString("X6");
      }
   }
}