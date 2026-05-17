# HexManiacAdvance — Android Port

This directory (`src/HexManiac.MAUI`) is the .NET MAUI port of HexManiacAdvance for Android.

## Architecture

```
HexManiac.Core          (unchanged — all business logic + ViewModels)
    ↕ implements IFileSystem / IWorkDispatcher
HexManiac.MAUI          (new — MAUI UI replacing HexManiac.WPF)
    ├── Implementations/
    │   ├── AndroidFileSystem.cs   IFileSystem  → Android SAF file picker, clipboard
    │   └── MauiDispatcher.cs      IWorkDispatcher → MainThread / Task.Run
    ├── Controls/
    │   ├── HexContentView.cs      SkiaSharp canvas (replaces WPF HexContent)
    │   └── SkiaHexRenderer.cs     IDataFormatVisitor drawing on SKCanvas
    │                               (replaces WPF FormatDrawer + GlyphTypeface)
    ├── Pages/
    │   └── MainEditorPage          Toolbar, tabs, hex view, status bar
    ├── Platforms/Android/
    │   ├── MainActivity.cs
    │   └── AndroidManifest.xml
    └── MauiProgram.cs             DI bootstrap
```

## WPF → MAUI mapping

| WPF concept              | MAUI equivalent                            |
|--------------------------|---------------------------------------------|
| `FrameworkElement`       | `SKCanvasView` (SkiaSharp)                  |
| `DrawingContext`         | `SKCanvas`                                  |
| `GlyphTypeface`          | `SKFont` + `SKTypeface`                     |
| `Dispatcher`             | `MainThread.InvokeOnMainThreadAsync`        |
| `Microsoft.Win32.OpenFileDialog` | `FilePicker.Default.PickAsync`      |
| `Clipboard`              | `Clipboard.Default`                         |
| `Application.Resources` | `Application.Current.Resources`             |
| `DependencyProperty`     | Bindable properties / standard C# events   |
| `Window`                 | `Shell` + `ContentPage`                     |

## Building locally

```bash
# Install prerequisites once
dotnet workload install maui-android

# Debug APK (fast)
dotnet build src/HexManiac.MAUI/HexManiac.MAUI.csproj \
  -f net8.0-android -c Debug

# APK location
find . -name "*.apk" | grep -i debug
```

## CI/CD — GitHub Actions

The workflow at `.github/workflows/build-android.yml` runs automatically:

| Event                  | Job           | Output                      |
|------------------------|---------------|-----------------------------|
| push to main/master    | Debug APK     | Artifact (14-day retention) |
| tag `v*` (e.g. v1.2.3) | Signed APK   | GitHub Release + artifact   |

### Signing a release

1. Create a keystore: `keytool -genkey -v -keystore hexmaniac.jks -alias hexmaniac -keyalg RSA -keysize 2048 -validity 10000`
2. Base64-encode it: `base64 -w0 hexmaniac.jks`
3. Add these **repository secrets** in Settings → Secrets:
   - `KEYSTORE_BASE64` — the base64 string
   - `KEYSTORE_ALIAS`  — e.g. `hexmaniac`
   - `KEYSTORE_PASS`   — your password
4. Push a tag: `git tag v1.0.0 && git push --tags`

The workflow will build, sign, and create a GitHub Release automatically.

## Known limitations of this port

| Feature               | Status                                                   |
|-----------------------|----------------------------------------------------------|
| Hex grid              | ✅ Full SkiaSharp rendering with colour coding            |
| Open / Save GBA       | ✅ Android file picker (SAF)                              |
| Undo / Redo           | ✅ Core handles this                                      |
| Find / Goto           | ✅ Overlay UI wired to EditorViewModel                    |
| Pointer navigation    | ✅ Core handles, tap to move cursor                       |
| Table editor          | 🔶 Reads fine; full TableControl UI not ported yet        |
| Map editor            | 🔶 Not ported (MapTab.xaml is 150KB of WPF XAML)          |
| Image / palette editor| 🔶 Not ported                                             |
| IronPython scripts    | ⚠️ Loads but `Reflection.Emit` restricted on Android      |
| Script editor (HMA)   | 🔶 Core parses/writes; UI not ported                      |
| Keyboard shortcuts    | ➡️ Replaced with touch gestures + toolbar buttons         |

The Core library is completely unchanged, so all ROM parsing, data model
work, and script logic is identical to the desktop version.
