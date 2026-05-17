using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Implementations;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace HavenSoft.HexManiac.MAUI.Pages {
   /// <summary>
   /// Code-behind for the main editor page.
   /// Bridges EditorViewModel (unchanged from the WPF version) to MAUI controls.
   /// </summary>
   public partial class MainEditorPage : ContentPage {
      private readonly EditorViewModel _editor;
      private readonly AndroidFileSystem _fileSystem;

      public MainEditorPage(EditorViewModel editor) {
         InitializeComponent();
         _editor     = editor;
         _fileSystem = (AndroidFileSystem)editor.FileSystem;
         BindingContext = editor;

         // Apply theme to hex renderer
         SkiaHexRenderer.UpdateTheme(editor.Theme);
         editor.Theme.PropertyChanged += (_, _) => SkiaHexRenderer.UpdateTheme(editor.Theme);

         // Wire up editor events
         editor.PropertyChanged          += OnEditorPropertyChanged;
         editor.CollectionChanged        += OnTabsChanged;

         // Initial tab render
         RebuildTabStrip();
         SwitchToTab(editor.SelectedIndex);

         // Wire status bar
         UpdateStatusBar();
      }

      // ── Tab management ────────────────────────────────────────────────────────

      private void RebuildTabStrip() {
         TabStrip.Children.Clear();
         for (int i = 0; i < _editor.Count; i++) {
            int captured = i;
            var tab = _editor[i];

            var btn = new Button {
               Text    = tab.Name,
               Style   = (Style)Resources["TabBtn"],
            };
            btn.Clicked += (_, _) => {
               _editor.SelectedIndex = captured;
               SwitchToTab(captured);
            };

            // Highlight the active tab
            if (i == _editor.SelectedIndex)
               btn.BackgroundColor = Color.FromArgb("#313244");

            // Close button (small ×)
            var closeBtn = new Button {
               Text            = "×",
               FontSize        = 10,
               Padding         = new Thickness(4, 0),
               BackgroundColor = Colors.Transparent,
               TextColor       = Color.FromArgb("#585B70"),
               WidthRequest    = 20,
            };
            closeBtn.Clicked += (_, _) => tab.Close.Execute(_fileSystem);

            var cell = new HorizontalStackLayout { Spacing = 0 };
            cell.Children.Add(btn);
            cell.Children.Add(closeBtn);
            TabStrip.Children.Add(cell);
         }
      }

      private void SwitchToTab(int index) {
         if (index < 0 || index >= _editor.Count) {
            HexView.ViewPort = null;
            return;
         }
         var tab = _editor[index];
         if (tab is IViewPort vp) {
            HexView.ViewPort = vp;
         }

         // Listen for errors / messages from this tab
         foreach (var t in _editor) {
            t.OnError   -= OnTabError;
            t.OnMessage -= OnTabMessage;
            t.ClearMessage -= OnTabClearMessage;
         }
         tab.OnError      += OnTabError;
         tab.OnMessage    += OnTabMessage;
         tab.ClearMessage += OnTabClearMessage;
         UpdateStatusBar();
      }

      private void OnTabsChanged(object sender, NotifyCollectionChangedEventArgs e) {
         MainThread.BeginInvokeOnMainThread(() => {
            RebuildTabStrip();
            SwitchToTab(_editor.SelectedIndex);
         });
      }

      private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(EditorViewModel.SelectedIndex)) {
            MainThread.BeginInvokeOnMainThread(() => {
               RebuildTabStrip();
               SwitchToTab(_editor.SelectedIndex);
            });
         }
         UpdateStatusBar();
      }

      // ── Status bar ────────────────────────────────────────────────────────────

      private void UpdateStatusBar() {
         MainThread.BeginInvokeOnMainThread(() => {
            if (HexView.ViewPort is IViewPort vp) {
               var sel = vp.SelectionStart;
               int addr = (vp.ScrollValue + sel.Y) * vp.Width + sel.X;
               AddressLabel.Text = $"0x{addr:X6}";
            }
            StatusLabel.Text  = _editor.InformationMessage ?? string.Empty;
            MessageLabel.Text = _editor.IsNewVersionAvailable ? "Update available" : string.Empty;
         });
      }

      private void OnTabError(object sender, string error) {
         MainThread.BeginInvokeOnMainThread(() => {
            ErrorLabel.Text  = error;
            ErrorBar.IsVisible = !string.IsNullOrEmpty(error);
         });
      }

      private void OnTabMessage(object sender, string msg) {
         MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text = msg;
         });
      }

      private void OnTabClearMessage(object sender, EventArgs e) {
         MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text   = string.Empty;
            ErrorBar.IsVisible = false;
         });
      }

      // ── Toolbar handlers ──────────────────────────────────────────────────────

      private void OnOpenClicked(object sender, EventArgs e) {
         _editor.Open.Execute(_fileSystem);
      }

      private void OnSaveClicked(object sender, EventArgs e) {
         _editor.Save.Execute(_fileSystem);
      }

      private void OnSaveAsClicked(object sender, EventArgs e) {
         _editor.SaveAs.Execute(_fileSystem);
      }

      private void OnUndoClicked(object sender, EventArgs e) => _editor.Undo.Execute(null);

      private void OnRedoClicked(object sender, EventArgs e) => _editor.Redo.Execute(null);

      private void OnNewTabClicked(object sender, EventArgs e) {
         // Open file picker immediately
         _editor.Open.Execute(_fileSystem);
      }

      // ── Goto overlay ──────────────────────────────────────────────────────────

      private void OnGotoClicked(object sender, EventArgs e) {
         GotoOverlay.IsVisible = true;
         GotoEntry.Focus();
      }

      private void OnGotoConfirm(object sender, EventArgs e) {
         var text = GotoEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) {
            _editor.GotoViewModel.GotoAddress = text;
            _editor.GotoViewModel.MoveToAddress.Execute(text);
         }
         GotoOverlay.IsVisible = false;
         GotoEntry.Text = string.Empty;
         UpdateStatusBar();
      }

      private void OnGotoCancel(object sender, EventArgs e) {
         GotoOverlay.IsVisible = false;
         GotoEntry.Text = string.Empty;
      }

      // ── Find overlay ──────────────────────────────────────────────────────────

      private void OnFindClicked(object sender, EventArgs e) {
         FindOverlay.IsVisible = true;
         FindEntry.Focus();
      }

      private void OnFindNext(object sender, EventArgs e) {
         var text = FindEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) _editor.FindNext.Execute(text);
      }

      private void OnFindPrev(object sender, EventArgs e) {
         var text = FindEntry.Text?.Trim();
         if (!string.IsNullOrEmpty(text)) _editor.FindPrevious.Execute(text);
      }

      private void OnFindCancel(object sender, EventArgs e) {
         FindOverlay.IsVisible = false;
         FindEntry.Text = string.Empty;
      }
   }
}
