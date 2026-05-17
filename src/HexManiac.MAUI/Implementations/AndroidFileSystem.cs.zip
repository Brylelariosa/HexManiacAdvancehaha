using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.MAUI.Implementations {
   /// <summary>
   /// Android implementation of IFileSystem.
   /// Uses MAUI FilePicker (which calls Android's Storage Access Framework) for
   /// open/save dialogs, and Android Clipboard for copy/paste.
   /// </summary>
   public class AndroidFileSystem : IFileSystem, IWorkDispatcher {
      // ── Clipboard ──────────────────────────────────────────────────────────

      public string CopyText {
         get => Clipboard.Default.GetTextAsync().GetAwaiter().GetResult() ?? string.Empty;
         set => MainThread.InvokeOnMainThreadAsync(() => Clipboard.Default.SetTextAsync(value)).GetAwaiter().GetResult();
      }

      // Image clipboard is not supported on Android via MAUI; no-op.
      public (short[] image, int width) CopyImage {
         get => (Array.Empty<short>(), 0);
         set { /* Android doesn't support image clipboard via MAUI */ }
      }

      // ── File picking ────────────────────────────────────────────────────────

      public string RequestNewName(string currentName, string extensionDescription = null, params string[] extensionOptions) {
         // On Android we use a save dialog via SAF. Return a path in the app's external files dir.
         // Full SAF "create document" requires a native intent; here we fall back to app-local storage.
         var ext = extensionOptions.FirstOrDefault() ?? "bin";
         var fileName = Path.GetFileNameWithoutExtension(currentName);
         if (string.IsNullOrEmpty(fileName)) fileName = "output";
         return Path.Combine(GetAppSaveDir(), $"{fileName}.{ext}");
      }

      public LoadedFile OpenFile(string extensionDescription = null, params string[] extensionOptions) {
         return OpenFileAsync(extensionDescription, extensionOptions).GetAwaiter().GetResult();
      }

      private async Task<LoadedFile> OpenFileAsync(string description, string[] extensions) {
         try {
            var types = BuildFileTypes(description, extensions);
            var result = await FilePicker.Default.PickAsync(new PickOptions {
               PickerTitle = description ?? "Open ROM",
               FileTypes = types,
            });
            if (result == null) return null;
            var data = await ReadAllBytesAsync(result.FullPath);
            return new LoadedFile(result.FullPath, data);
         } catch {
            return null;
         }
      }

      public string OpenFolder() {
         // Android doesn't have a folder picker in standard MAUI; return app external dir.
         return GetAppSaveDir();
      }

      public bool Exists(string file) => File.Exists(file);

      public void LaunchProcess(string file, string arguments = null) {
         // Not applicable on Android — no shell process launching.
      }

      public LoadedFile LoadFile(string fileName) {
         if (!File.Exists(fileName)) return null;
         try {
            var data = File.ReadAllBytes(fileName);
            return new LoadedFile(fileName, data);
         } catch {
            return null;
         }
      }

      // ── File watching ───────────────────────────────────────────────────────

      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new();

      public void AddListenerToFile(string fileName, Action<IFileSystem> listener) {
         if (!listeners.ContainsKey(fileName)) listeners[fileName] = new List<Action<IFileSystem>>();
         listeners[fileName].Add(listener);
      }

      public void RemoveListenerForFile(string fileName, Action<IFileSystem> listener) {
         if (listeners.TryGetValue(fileName, out var list)) list.Remove(listener);
      }

      // ── Save ────────────────────────────────────────────────────────────────

      public bool Save(LoadedFile file) {
         try {
            EnsureDirectory(Path.GetDirectoryName(file.Name));
            File.WriteAllBytes(file.Name, file.Contents.ToArray());
            return true;
         } catch {
            return false;
         }
      }

      public bool SaveMetadata(string originalFileName, string[] metadata) {
         try {
            var metaPath = originalFileName + ".toml";
            File.WriteAllLines(metaPath, metadata);
            return true;
         } catch {
            return false;
         }
      }

      public bool? TrySavePrompt(LoadedFile file) {
         // On Android, show a dialog via the dispatcher then save.
         bool? result = null;
         MainThread.InvokeOnMainThreadAsync(async () => {
            var answer = await Shell.Current.DisplayAlert(
               "Save?",
               $"Do you want to save changes to {Path.GetFileName(file.Name)}?",
               "Save", "Don't Save");
            if (answer) {
               result = Save(file);
            } else {
               result = false;
            }
         }).GetAwaiter().GetResult();
         return result;
      }

      // ── Metadata ────────────────────────────────────────────────────────────

      public string[] MetadataFor(string fileName) {
         var metaPath = fileName + ".toml";
         if (!File.Exists(metaPath)) return null;
         try { return File.ReadAllLines(metaPath); } catch { return null; }
      }

      // ── Image I/O ───────────────────────────────────────────────────────────

      public (short[] image, int width) LoadImage(string fileName = null) {
         // Load a PNG via MAUI and convert to 5r5g5b shorts
         try {
            if (fileName == null) {
               var result = FilePicker.Default.PickAsync(new PickOptions {
                  PickerTitle = "Open Image",
                  FileTypes = FilePickerFileType.Images,
               }).GetAwaiter().GetResult();
               if (result == null) return (null, 0);
               fileName = result.FullPath;
            }
            return PngToGbaImage(fileName);
         } catch {
            return (null, 0);
         }
      }

      public bool TryLoadIndexedImage(ref string fileName, out int[,] image, out IReadOnlyList<short> palette) {
         image = null; palette = null;
         // Indexed PNG loading requires a PNG decoder. Return false (not supported in initial port).
         return false;
      }

      public void SaveImage(short[] image, int width, string fileName = null) {
         // Convert 5r5g5b shorts to PNG and save.
         if (fileName == null) fileName = Path.Combine(GetAppSaveDir(), "export.png");
         try { GbaImageToPng(image, width, fileName); } catch { /* best-effort */ }
      }

      public void SaveImage(int[,] image, IReadOnlyList<short> palette, string fileName = null) {
         // Indexed image export — best effort.
      }

      // ── Dialogs ─────────────────────────────────────────────────────────────

      public int ShowOptions(string title, string prompt, IReadOnlyList<IReadOnlyList<object>> additionalDetails, params VisualOption[] options) {
         int chosen = 0;
         MainThread.InvokeOnMainThreadAsync(async () => {
            var names = options.Select(o => o.Option).ToArray();
            var pick = await Shell.Current.DisplayActionSheet(title + "\n" + prompt, "Cancel", null, names);
            chosen = Array.IndexOf(names, pick);
            if (chosen < 0) chosen = 0;
         }).GetAwaiter().GetResult();
         return chosen;
      }

      public string RequestText(string title, string prompt) {
         string result = null;
         MainThread.InvokeOnMainThreadAsync(async () => {
            result = await Shell.Current.DisplayPromptAsync(title, prompt);
         }).GetAwaiter().GetResult();
         return result;
      }

      public bool? ShowCustomMessageBox(string message, bool showYesNoCancel = true, params ProcessModel[] links) {
         bool? result = null;
         MainThread.InvokeOnMainThreadAsync(async () => {
            if (showYesNoCancel) {
               var answer = await Shell.Current.DisplayAlert("HexManiacAdvance", message, "Yes", "No");
               result = answer;
            } else {
               await Shell.Current.DisplayAlert("HexManiacAdvance", message, "OK");
               result = true;
            }
         }).GetAwaiter().GetResult();
         return result;
      }

      // ── IWorkDispatcher (dual-interface, same object passed to EditorViewModel) ──

      public async Task WaitForRenderingAsync() => await Task.Delay(16);

      public void BlockOnUIWork(Action action) {
         if (MainThread.IsMainThread) action();
         else MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
      }

      public Task DispatchWork(Action action) => MainThread.InvokeOnMainThreadAsync(action);

      public Task RunBackgroundWork(Action action) => Task.Run(action);

      public IDelayWorkTimer CreateDelayTimer() => new MauiDelayTimer();

      // ── Helpers ─────────────────────────────────────────────────────────────

      private static string GetAppSaveDir() {
         var dir = Path.Combine(
            Android.OS.Environment.GetExternalStoragePublicDirectory(
               Android.OS.Environment.DirectoryDocuments)?.AbsolutePath
               ?? FileSystem.AppDataDirectory,
            "HexManiacAdvance");
         EnsureDirectory(dir);
         return dir;
      }

      private static void EnsureDirectory(string dir) {
         if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
      }

      private static async Task<byte[]> ReadAllBytesAsync(string path) {
         using var stream = File.OpenRead(path);
         var bytes = new byte[stream.Length];
         await stream.ReadAsync(bytes, 0, bytes.Length);
         return bytes;
      }

      private static FilePickerFileType BuildFileTypes(string description, string[] extensions) {
         if (extensions == null || extensions.Length == 0) return null;
         return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
            { DevicePlatform.Android, extensions.Select(e => "." + e) },
         });
      }

      /// <summary>Convert a PNG file to an array of 5r5g5b shorts (GBA format).</summary>
      private static (short[] image, int width) PngToGbaImage(string path) {
         using var fs = File.OpenRead(path);
         var bmp = SkiaSharp.SKBitmap.Decode(fs);
         if (bmp == null) return (null, 0);
         var pixels = new short[bmp.Width * bmp.Height];
         for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
               var c = bmp.GetPixel(x, y);
               int r = c.Red >> 3, g = c.Green >> 3, b = c.Blue >> 3;
               pixels[y * bmp.Width + x] = (short)(r | (g << 5) | (b << 10));
            }
         }
         return (pixels, bmp.Width);
      }

      /// <summary>Convert a 5r5g5b shorts array back to a PNG file.</summary>
      private static void GbaImageToPng(short[] image, int width, string path) {
         int height = image.Length / width;
         using var bmp = new SkiaSharp.SKBitmap(width, height);
         for (int i = 0; i < image.Length; i++) {
            int s = image[i];
            byte r = (byte)((s & 0x1F) << 3);
            byte g = (byte)(((s >> 5) & 0x1F) << 3);
            byte b = (byte)(((s >> 10) & 0x1F) << 3);
            bmp.SetPixel(i % width, i / width, new SkiaSharp.SKColor(r, g, b));
         }
         using var output = File.Create(path);
         bmp.Encode(output, SkiaSharp.SKEncodedImageFormat.Png, 100);
      }
   }
}
