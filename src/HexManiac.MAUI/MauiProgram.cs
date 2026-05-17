using CommunityToolkit.Maui;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.MAUI.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace HavenSoft.HexManiac.MAUI {
   public static class MauiProgram {
      public static MauiApp CreateMauiApp() {
         var builder = MauiApp.CreateBuilder();

         builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => {
               fonts.AddFont("Consolas.ttf", "Consolas");
               fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

         // Register platform services
         builder.Services.AddSingleton<IWorkDispatcher, MauiDispatcher>();
         builder.Services.AddSingleton<IFileSystem, AndroidFileSystem>();
         builder.Services.AddSingleton<EditorViewModel>(sp => {
            var fs = sp.GetRequiredService<IFileSystem>() as AndroidFileSystem;
            var dispatcher = sp.GetRequiredService<IWorkDispatcher>();
            return new EditorViewModel(fs, dispatcher, allowLoadingMetadata: true);
         });

         // Pages
         builder.Services.AddTransient<Pages.MainEditorPage>();

         return builder.Build();
      }
   }
}
