using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.MAUI.Implementations {
   public class AndroidFileSystem : IFileSystem, IWorkDispatcher {
      public string CopyText {
         get => Clipboard.Default.GetTextAsync().GetAwaiter().GetResult() ?? string.Empty;
         set => MainThread.InvokeOnMainThreadAsync(() => Clipboard.Default.SetTextAsync(value)).GetAwaiter().GetResult();
      }
      public (short[] image, int width) CopyImage {
         get => (Array.Empty<short>(), 0);
         set { }
      }
      public string RequestNewName(string currentName, string extensionDescription = null, params string[] extensionOptions) {
         var ext = extensionOptions.FirstOrDefault() ?? "bin";
         var name = Path.GetFileNameWithoutExtension(currentName);
         if (string.IsNullOrEmpty(name)) name = "output";
         return Path.Combine(GetAppSaveDir(), $"{name}.{ext}");
      }
      public LoadedFile OpenFile(string extensionDescription = null, params string[] extensionOptions)
         => OpenFileAsync(extensionDescription, extensionOptions).GetAwaiter().GetResult();

      private async Task<LoadedFile> OpenFileAsync(string description, string[] extensions) {
         try {
            var result = await FilePicker.Default.PickAsync(new PickOptions {
               PickerTitle = description ?? "Open ROM",
               FileTypes = BuildFileTypes(extensions),
            });
            if (result == null) return null;
            var data = File.ReadAllBytes(result.FullPath);
            return new LoadedFile(result.FullPath, data);
         } catch { return null; }
      }
      public string OpenFolder() => GetAppSaveDir();
      public bool Exists(string file) => File.Exists(file);
      public void LaunchProcess(string file, string arguments = null) { }
      public LoadedFile LoadFile(string fileName) {
         if (!File.Exists(fileName)) return null;
         try { return new LoadedFile(fileName, File.ReadAllBytes(fileName)); } catch { return null; }
      }
      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new();
      public void AddListenerToFile(string fileName, Action<IFileSystem> listener) {
         if (!listeners.ContainsKey(fileName)) listeners[fileName] = new();
         listeners[fileName].Add(listener);
      }
      public void RemoveListenerForFile(string fileName, Action<IFileSystem> listener) {
         if (listeners.TryGetValue(fileName, out var list)) list.Remove(listener);
      }
      public bool Save(LoadedFile file) {
         try { EnsureDirectory(Path.GetDirectoryName(file.Name)); File.WriteAllBytes(file.Name, file.Contents.ToArray()); return true; } catch { return false; }
      }
      public bool SaveMetadata(string originalFileName, string[] metadata) {
         try { File.WriteAllLines(originalFileName + ".toml", metadata); return true; } catch { return false; }
      }
      public bool? TrySavePrompt(LoadedFile file) {
         bool? result = null;
         MainThread.InvokeOnMainThreadAsync(async () => {
            var answer = await Shell.Current.DisplayAlert("Save?", $"Save changes to {Path.GetFileName(file.Name)}?", "Save", "Don't Save");
            result = answer ? Save(file) : false;
         }).GetAwaiter().GetResult();
         return result;
      }
      public string[] MetadataFor(string fileName) {
         var p = fileName + ".toml";
         if (!File.Exists(p)) return null;
         try { return File.ReadAllLines(p); } catch { return null; }
      }
      public (short[] image, int width) LoadImage(string fileName = null) {
         try {
            if (fileName == null) {
               var r = FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Open Image", FileTypes = FilePickerFileType.Images }).GetAwaiter().GetResult();
               if (r == null) return (null, 0);
               fileName = r.FullPath;
            }
            return PngToGbaImage(fileName);
         } catch { return (null, 0); }
      }
      public bool TryLoadIndexedImage(ref string fileName, out int[,] image, out IReadOnlyList<short> palette) { image = null; palette = null; return false; }
      public void SaveImage(short[] image, int width, string fileName = null) {
         if (fileName == null) fileName = Path.Combine(GetAppSaveDir(), "export.png");
         try { GbaImageToPng(image, width, fileName); } catch { }
      }
      public void SaveImage(int[,] image, IReadOnlyList<short> palette, string fileName = null) { }
      public int ShowOptions(string title, string prompt, IReadOnlyList<IReadOnlyList<object>> additionalDetails, params VisualOption[] options) {
         int chosen = 0;
         MainThread.InvokeOnMainThreadAsync(async () => {
            var names = options.Select(o => o.Option).ToArray();
            var pick = await Shell.Current.DisplayActionSheet(title + "\n" + prompt, "Cancel", null, names);
            chosen = Math.Max(0, Array.IndexOf(names, pick));
         }).GetAwaiter().GetResult();
         return chosen;
      }
      public string RequestText(string title, string prompt) {
         string result = null;
         MainThread.InvokeOnMainThreadAsync(async () => { result = await Shell.Current.DisplayPromptAsync(title, prompt); }).GetAwaiter().GetResult();
         return result;
      }
      public bool? ShowCustomMessageBox(string message, bool showYesNoCancel = true, params ProcessModel[] links) {
         bool? result = null;
         MainThread.InvokeOnMainThreadAsync(async () => {
            if (showYesNoCancel) { result = await Shell.Current.DisplayAlert("HexManiacAdvance", message, "Yes", "No"); }
            else { await Shell.Current.DisplayAlert("HexManiacAdvance", message, "OK"); result = true; }
         }).GetAwaiter().GetResult();
         return result;
      }

      // IWorkDispatcher
      public async Task WaitForRenderingAsync() => await Task.Delay(16);
      public void BlockOnUIWork(Action action) {
         if (MainThread.IsMainThread) action();
         else MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
      }
      public Task DispatchWork(Action action) => MainThread.InvokeOnMainThreadAsync(action);
      public Task RunBackgroundWork(Action action) => Task.Run(action);
      public IDelayWorkTimer CreateDelayTimer() => new MauiDelayTimer();

      // Helpers
      private static string GetAppSaveDir() {
         var dir = Path.Combine(
            Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath
               ?? Microsoft.Maui.Storage.FileSystem.AppDataDirectory,
            "HexManiacAdvance");
         EnsureDirectory(dir);
         return dir;
      }
      private static void EnsureDirectory(string dir) { if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir); }
      private static FilePickerFileType BuildFileTypes(string[] extensions) {
         if (extensions == null || extensions.Length == 0) return null;
         return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
            { DevicePlatform.Android, extensions.Select(e => e.StartsWith(".") ? e : "." + e) },
         });
      }
      private static (short[] image, int width) PngToGbaImage(string path) {
         using var fs = File.OpenRead(path);
         var bmp = SkiaSharp.SKBitmap.Decode(fs);
         if (bmp == null) return (null, 0);
         var pixels = new short[bmp.Width * bmp.Height];
         for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++) {
               var c = bmp.GetPixel(x, y);
               pixels[y * bmp.Width + x] = (short)((c.Red >> 3) | ((c.Green >> 3) << 5) | ((c.Blue >> 3) << 10));
            }
         return (pixels, bmp.Width);
      }
      private static void GbaImageToPng(short[] image, int width, string path) {
         int height = image.Length / width;
         using var bmp = new SkiaSharp.SKBitmap(width, height);
         for (int i = 0; i < image.Length; i++) {
            int s = image[i];
            bmp.SetPixel(i % width, i / width, new SkiaSharp.SKColor((byte)((s & 0x1F) << 3), (byte)(((s >> 5) & 0x1F) << 3), (byte)(((s >> 10) & 0x1F) << 3)));
         }
         using var output = File.Create(path);
         bmp.Encode(output, SkiaSharp.SKEncodedImageFormat.Png, 100);
      }
   }
}
