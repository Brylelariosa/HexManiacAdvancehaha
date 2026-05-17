using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Pages;
using Microsoft.Maui.Controls;

namespace HavenSoft.HexManiac.MAUI {
   public class AppShell : Shell {
      public AppShell(EditorViewModel editor) {
         // No navigation bar - the editor is full-screen
         Shell.SetNavBarIsVisible(this, false);

         var mainPage = new MainEditorPage(editor);
         Items.Add(new ShellContent {
            Route = "main",
            Content = mainPage,
         });
      }
   }
}
