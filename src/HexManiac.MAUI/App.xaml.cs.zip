using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Pages;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace HavenSoft.HexManiac.MAUI {
   public partial class App : Application {
      public App(EditorViewModel editor) {
         InitializeComponent();

         // Apply theme from EditorViewModel
         ApplyTheme(editor.Theme);
         editor.Theme.PropertyChanged += (_, __) => ApplyTheme(editor.Theme);

         MainPage = new AppShell(editor);
      }

      private void ApplyTheme(HavenSoft.HexManiac.Core.ViewModels.Theme theme) {
         // Map HexManiacAdvance theme colours into MAUI resource dictionary
         if (Application.Current?.Resources == null) return;
         var res = Application.Current.Resources;
         SetColorResource(res, "BackgroundColor",  theme.Background);
         SetColorResource(res, "PrimaryColor",     theme.Primary);
         SetColorResource(res, "SecondaryColor",   theme.Secondary);
         SetColorResource(res, "TextColor",        theme.Text1);
         SetColorResource(res, "AccentColor",      theme.Accent);
         SetColorResource(res, "ErrorColor",       theme.Error);
         SetColorResource(res, "Data1Color",       theme.Data1);
         SetColorResource(res, "Data2Color",       theme.Data2);
         SetColorResource(res, "Stream1Color",     theme.Stream1);
         SetColorResource(res, "Stream2Color",     theme.Stream2);
      }

      private static void SetColorResource(ResourceDictionary res, string key, string hex) {
         if (Color.TryParse(hex, out var color)) res[key] = color;
      }
   }
}
