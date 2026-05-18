using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Controls;
using HavenSoft.HexManiac.MAUI.Implementations;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HavenSoft.HexManiac.MAUI.Pages {
   public partial class MainEditorPage : ContentPage {
      private readonly EditorViewModel _editor;
      private readonly AndroidFileSystem _fileSystem;

      public MainEditorPage(EditorViewModel editor) {
         InitializeComponent();
         _editor     = editor;
         _fileSystem = (AndroidFileSystem)editor.FileSystem;
         BindingContext = editor;

         SkiaHexRenderer.UpdateTheme(editor.Theme);
         editor.Theme.PropertyChanged += (_, _) => SkiaHexRenderer.UpdateTheme(editor.Theme);

         editor.PropertyChanged   += OnEditorPropertyChanged;
         editor.CollectionChanged += OnTabsChanged;

         RebuildTabStrip();
         SwitchToTab(editor.SelectedIndex);
      }

      // ── Tab management ────────────────────────────────────────────────────────
      private void RebuildTabStrip() {
         TabStrip.Children.Clear();
         for (int i = 0; i < _editor.Count; i++) {
            int captured = i;
            var tab = _editor[i];
            var btn = new Button {
               Text            = tab.Name,
               BackgroundColor = i == _editor.SelectedIndex
                  ? Color.FromArgb("#313244") : Colors.Transparent,
               TextColor       = Colors.White,
               FontFamily      = "Consolas",
               FontSize        = 12,
               Padding         = new Thickness(8, 4),
               CornerRadius    = 0,
            };
            btn.Clicked += (_, _) => { _editor.SelectedIndex = captured; SwitchToTab(captured); };

            var close = new Button {
               Text            = "×",
               FontSize        = 10,
               Padding         = new Thickness(4, 0),
               BackgroundColor = Colors.Transparent,
               TextColor       = Color.FromArgb("#585B70"),
               WidthRequest    = 22,
            };
            close.Clicked += (_, _) => tab.Close.Execute(_fileSystem);

            var cell = new HorizontalStackLayout { Spacing = 0 };
            cell.Children.Add(btn);
            cell.Children.Add(close);
            TabStrip.Children.Add(cell);
         }
      }

      private void SwitchToTab(int index) {
         foreach (var t in _editor) {
            t.OnError      -= OnTabError;
            t.OnMessage    -= OnTabMessage;
            t.ClearMessage -= OnTabClearMessage;
         }
         if (index < 0 || index >= _editor.Count) { HexView.ViewPort = null; return; }
         var tab = _editor[index];
         if (tab is IEditableViewPort vp) HexView.ViewPort = vp;
         tab.OnError      += OnTabError;
         tab.OnMessage    += OnTabMessage;
         tab.ClearMessage += OnTabClearMessage;
         UpdateStatusBar();
      }

      private void OnTabsChanged(object s, NotifyCollectionChangedEventArgs e) {
         MainThread.BeginInvokeOnMainThread(() => { RebuildTabStrip(); SwitchToTab(_editor.SelectedIndex); });
      }
      private void OnEditorPropertyChanged(object s, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(EditorViewModel.SelectedIndex))
            MainThread.BeginInvokeOnMainThread(() => { RebuildTabStrip(); SwitchToTab(_editor.SelectedIndex); });
         UpdateStatusBar();
      }

      // ── Status bar ────────────────────────────────────────────────────────────
      private void UpdateStatusBar() {
         MainThread.BeginInvokeOnMainThread(() => {
            if (HexView.ViewPort is IViewPort vp)
               AddressLabel.Text = vp.SelectedAddress ?? string.Empty;
            StatusLabel.Text  = _editor.InformationMessage ?? string.Empty;
            MessageLabel.Text = _editor.IsNewVersionAvailable ? "Update available" : string.Empty;
         });
      }
      private void OnTabError(object s, string error) {
         MainThread.BeginInvokeOnMainThread(() => { ErrorLabel.Text = error; ErrorBar.IsVisible = !string.IsNullOrEmpty(error); });
      }
      private void OnTabMessage(object s, string msg) {
         MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg);
      }
      private void OnTabClearMessage(object s, EventArgs e) {
         MainThread.BeginInvokeOnMainThread(() => { StatusLabel.Text = string.Empty; ErrorBar.IsVisible = false; });
      }

      // ── Toolbar ───────────────────────────────────────────────────────────────
      private void OnOpenClicked(object s, EventArgs e)   => _editor.Open.Execute(_fileSystem);
      private void OnSaveClicked(object s, EventArgs e)   => _editor.Save.Execute(_fileSystem);
      private void OnSaveAsClicked(object s, EventArgs e) => _editor.SaveAs.Execute(_fileSystem);
      private void OnUndoClicked(object s, EventArgs e)   => _editor.Undo.Execute(null);
      private void OnRedoClicked(object s, EventArgs e)   => _editor.Redo.Execute(null);
      private void OnNewTabClicked(object s, EventArgs e) => _editor.Open.Execute(_fileSystem);

      // ── Goto ─────────────────────────────────────────────────────────────────
      private void OnGotoClicked(object s, EventArgs e)  { GotoOverlay.IsVisible = true; GotoEntry.Focus(); }
      private void OnGotoCancel(object s, EventArgs e)   { GotoOverlay.IsVisible = false; GotoEntry.Text = string.Empty; }
      private void OnGotoConfirm(object s, EventArgs e) {
         var text = GotoEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) {
            // GotoControlViewModel uses Text + Goto command
            _editor.GotoViewModel.Text = text;
            _editor.GotoViewModel.Goto.Execute(text);
         }
         GotoOverlay.IsVisible = false;
         GotoEntry.Text = string.Empty;
      }

      // ── Find ──────────────────────────────────────────────────────────────────
      private void OnFindClicked(object s, EventArgs e)  { FindOverlay.IsVisible = true; FindEntry.Focus(); }
      private void OnFindCancel(object s, EventArgs e)   { FindOverlay.IsVisible = false; FindEntry.Text = string.Empty; }
      private void OnFindNext(object s, EventArgs e) {
         var text = FindEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) _editor.FindNext.Execute(text);
      }
      private void OnFindPrev(object s, EventArgs e) {
         var text = FindEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) _editor.FindPrevious.Execute(text);
      }
   }
}
