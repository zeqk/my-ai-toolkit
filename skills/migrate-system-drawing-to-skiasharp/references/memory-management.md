# Memory Management — SkiaSharp Disposables

## Disposable types reference

Use a **`using` declaration (`using var x = ...`)** for every SkiaSharp object. Explicit `.Dispose()` calls are only acceptable inside `IDisposable.Dispose()` implementations or `finally` blocks when ownership transfer makes `using var` impractical.

| Type | Holds |
|---|---|
| `SKBitmap` | pixel buffer |
| `SKImage` | encoded/decoded image data |
| `SKData` | raw byte buffer |
| `SKSurface` | render surface + backing store |
| `SKCanvas` | drawing context (when created via `SKSurface.Canvas`, **do not** dispose separately) |
| `SKPaint` | paint state |
| `SKPath` | path data |
| `SKFont` | font metrics |
| `SKTypeface` | font data |
| `SKPicture` | recorded drawing commands |
| `SKPictureRecorder` | picture recording context |
| `SKRegion` | pixel region |
| `SKShader` | gradient / pattern shader |
| `SKColorFilter` | color transformation filter |
| `SKMaskFilter` | mask/blur filter |
| `SKImageFilter` | image effect filter |
| `SKMatrix44` | 4×4 matrix |
| `SKDocument` | PDF/XPS document |

> **Rule**: `SKCanvas` obtained from `SKSurface.Canvas` is owned by the surface — **do not** call `.Dispose()` on it directly. All other types in the table above **must** be disposed.

- Do **not** share a single `SKPaint` across threads; create one per render scope.

> **Important — native memory leaks**: SkiaSharp objects hold unmanaged (native Skia) memory that is **not** reclaimed by the GC. Failing to dispose them causes native memory leaks that are invisible to .NET memory profilers. Always use a `using` declaration:
> ```csharp
> // Correct — use a using declaration; native memory is released deterministically
> using var paint = new SKPaint { Color = SKColors.Red };
> using var path  = new SKPath();
>
> // Wrong — no using; GC may never finalize these; native memory leaks
> var paint = new SKPaint { Color = SKColors.Red };
> var path  = new SKPath();
>
> // Wrong — explicit Dispose without using; error-prone, avoid unless necessary
> var paint = new SKPaint { Color = SKColors.Red };
> paint.Dispose();
> ```

---

## Disposal audit — do this before finalizing any migration

All three categories below produce disposable objects. **Always use `using var`** for each result:

**Category 1 — constructors**
```csharp
using var bmp       = new SKBitmap(width, height);
using var paint     = new SKPaint { ... };
using var path      = new SKPath();
using var font      = new SKFont(typeface, size);
using var recorder  = new SKPictureRecorder();
using var region    = new SKRegion();
```

**Category 2 — static factory methods**
```csharp
using var image     = SKImage.FromBitmap(bmp);
using var image2    = SKImage.FromEncodedData(bytes);   // ← common miss
using var image3    = SKImage.FromPixels(info, pixels);
using var bmp2      = SKBitmap.Decode(stream);
using var typeface  = SKTypeface.FromFamilyName("Arial");
using var typeface2 = SKTypeface.FromFile("font.ttf");  // ← common miss
using var data      = SKData.CreateCopy(bytes);
using var surface   = SKSurface.Create(info);
using var shader    = SKShader.CreateLinearGradient(...);
using var cf        = SKColorFilter.CreateColorMatrix(matrix);
using var mf        = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
using var imgf      = SKImageFilter.CreateBlur(sigmaX, sigmaY);
```

**Category 3 — instance methods that return a new disposable**
```csharp
// These are the most commonly missed — the variable looks like a value but is IDisposable
using var encoded  = image.Encode(SKEncodedImageFormat.Png, 100);  // returns SKData
using var encoded2 = image.Encode();                                // returns SKData
using var stream   = skData.AsStream();                             // returns Stream
using var picture  = recorder.EndRecording();                       // returns SKPicture
using var snapshot = surface.Snapshot();                            // returns SKImage
using var pixmap   = skBitmap.PeekPixels();                         // returns SKPixmap
using var pixmap2  = skImage.PeekPixels();                          // returns SKPixmap
```

For each match, verify `using var x = ...;` is present. If ownership transfer is required (e.g. storing in a field), ensure disposal happens in the class's own `Dispose()` method — but `using var` at the local scope is always preferred.

**Also scan for separated declaration and assignment**: search for lines of the form `SKxxx varName;` (type-only declaration with no initializer). Each one is a potential leak — find where the variable is assigned and apply Option A, B, or C from the [disposal rules](./disposal-rules.md).
