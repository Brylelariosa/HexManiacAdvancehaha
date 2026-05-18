using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Pages;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace HavenSoft.HexManiac.MAUI {
   public partial class App : Application {
      private readonly EditorViewModel editor;

      public App(EditorViewModel editor) {
         this.editor = editor;
         InitializeComponent();
         ApplyTheme(editor.Theme);
         editor.Theme.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => ApplyTheme(editor.Theme));
         // Do NOT access Windows[0] here — it's empty until CreateWindow() is called by the platform.
      }

      protected override Window CreateWindow(IActivationState activationState)
         => new Window(new AppShell(editor));

      private void ApplyTheme(Theme theme) {
         if (Resources == null) return;
         SetColor("BackgroundColor", theme.Background);
         SetColor("PrimaryColor",    theme.Primary);
         SetColor("SecondaryColor",  theme.Secondary);
         SetColor("TextColor",       theme.Text1);
         SetColor("AccentColor",     theme.Accent);
         SetColor("ErrorColor",      theme.Error);
         SetColor("Data1Color",      theme.Data1);
         SetColor("Stream1Color",    theme.Stream1);
      }

      private void SetColor(string key, string hex) {
         if (Color.TryParse(hex, out var color)) Resources[key] = color;
      }
   }
}
