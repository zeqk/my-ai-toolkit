---
name: migrate-system-drawing-to-skiasharp
description: 'Migrate C# code from System.Drawing (GDI+) to SkiaSharp. Use when porting Bitmap, Graphics, Pen, Brush, Font, Color, GraphicsPath, or any System.Drawing type to SkiaSharp equivalents. Covers NuGet setup, type mapping, drawing operations, color conversion, text rendering, image I/O, transforms, and memory management. Triggers: System.Drawing migration, SkiaSharp port, GDI+ to Skia, cross-platform graphics, PlatformNotSupportedException System.Drawing.'
argument-hint: 'Path or description of the C# file(s) to migrate'
---

# Migrate System.Drawing → SkiaSharp

## When to Use
- Porting a .NET app to be cross-platform (Linux/macOS/containers) where `System.Drawing.Common` throws `PlatformNotSupportedException` on .NET 6+.
- Modernizing image-processing, chart-generation, or thumbnail code.
- The user has files with `using System.Drawing` or `System.Drawing.Imaging`, `System.Drawing.Drawing2D`, `System.Drawing.Text`.

## Procedure

### Step 1 — Audit the Code
Scan the target file(s) for all `System.Drawing` usages:
- Collect every type, method, and property referenced.
- Flag areas where the migration is **non-trivial** (custom `IDeviceContext`, GDI handles, `Graphics.FromHwnd`, `Metafile`, `PrintDocument`).
- **Disposal constraint — enforced at write time, not as a post-check**: every time you write a line that produces a SkiaSharp disposable, write `using var` on *that same line* before moving on. The three categories that produce disposables are:
  1. **Constructors**: `new SKBitmap(...)`, `new SKPaint(...)`, `new SKPath()`, `new SKFont(...)`, `new SKPictureRecorder()`, `new SKRegion()`, …
  2. **Static factory methods** on any `SK*` class: `.From*(…)`, `.Decode(…)`, `.Create*(…)`, `.Load*(…)` — e.g. `SKImage.FromEncodedData(...)`, `SKImage.FromBitmap(...)`, `SKBitmap.Decode(...)`, `SKTypeface.FromFamilyName(...)`, `SKData.CreateCopy(...)`, `SKSurface.Create(...)`, `SKShader.CreateLinearGradient(...)`, `SKColorFilter.CreateColorMatrix(...)`, `SKMaskFilter.CreateBlur(...)`, `SKImageFilter.CreateBlur(...)`.
  3. **Instance methods that return a new disposable object**: `skImage.Encode(...)` → `SKData`, `skData.AsStream()` → `Stream`, `recorder.EndRecording()` → `SKPicture`, `surface.Snapshot()` → `SKImage`, `skBitmap.PeekPixels()` → `SKPixmap`, `skImage.PeekPixels()` → `SKPixmap`.

  **Never** write `var x = SKImage.FromEncodedData(...)` — always `using var x = SKImage.FromEncodedData(...)`.

- **Separated declaration and assignment**: when a variable is declared on one line and assigned on another (`SKImage img; ... img = SKImage.From*(...)`) the `using var` modifier cannot be added at the assignment site. This is a **high-risk pattern** that almost always results in a leak. Apply one of these fixes in order of preference:

  **Option A — combine declaration and assignment (always preferred)**:
  ```csharp
  // Wrong — separated, no disposal
  SKImage img;
  img = SKImage.FromBitmap(bmp);

  // Correct — combined with using var
  using var img = SKImage.FromBitmap(bmp);
  ```

  **Option B — conditional assignment: use ternary to allow a single `using var`**:
  ```csharp
  // Wrong — separated conditional, no disposal
  SKImage img;
  if (condition)
      img = SKImage.FromBitmap(bmp);
  else
      img = SKImage.FromEncodedData(data);

  // Correct — ternary keeps a single using var
  using var img = condition
      ? SKImage.FromBitmap(bmp)
      : SKImage.FromEncodedData(data);
  ```

  **Option C — when restructuring is not possible: `try/finally` with null-conditional dispose**:
  ```csharp
  SKImage? img = null;
  try
  {
      img = SKImage.FromBitmap(bmp);
      // ... use img ...
  }
  finally
  {
      img?.Dispose();
  }
  ```

  Never leave a separated declaration/assignment without one of the above patterns.

- **Fallback / blank-image pattern**: When a "default" image is created as a fallback and a ternary determines which image to use, a common mistake is leaving the unused branch undisposed. Use `using var` for the blank/default image, then separately decode the real image (or `null`) with `using var`, and use `??` to select a non-owning alias:

  ```csharp
  // Wrong — blankImg leaks when photo is not null; the decoded photo always leaks
  var blankImg = SKImage.FromEncodedData(defaultStream);
  var p = photo != null ? SKImage.FromEncodedData(photo) : blankImg;

  // Correct — separate ownership; ?? for non-owning alias selection
  using var blankImg     = SKImage.FromEncodedData(defaultStream);
  using var photoDecoded = photo != null ? SKImage.FromEncodedData(photo) : null;
  var p = photoDecoded ?? blankImg;  // p is a non-owning alias — do NOT dispose p separately
  ```

  Key rule: `p` is just an alias — do **not** call `p.Dispose()` or wrap it in a separate `using`. Disposal is already handled by `blankImg` and `photoDecoded`.

- **Never dispose a received parameter**: A method must not call `.Dispose()` (or `?.Dispose()`) on an `SK*` object it received as a parameter. Ownership belongs to whoever created the object; only the creator (or a scope above it) is responsible for disposal. A method that receives an `SKImage`, `SKBitmap`, etc. may use it freely but must never dispose it.

  ```csharp
  // Wrong — the method disposes blankImg which it did NOT create
  SKImage pickImage(string photo, SKImage blankImg)
  {
      using var photoDecoded = photo != null ? SKImage.FromEncodedData(...) : null;
      if (photoDecoded != null)
      {
          blankImg?.Dispose();  // ← NEVER do this; caller owns blankImg
          return photoDecoded.ToRasterImage(); // return a copy if needed
      }
      return blankImg;
  }

  // Correct — caller owns both objects and disposes them; callee only selects
  SKImage pickImage(string photo, SKImage blankImg)
  {
      using var photoDecoded = photo != null ? SKImage.FromEncodedData(...) : null;
      // Return a non-owning alias; caller is responsible for disposal of both
      return photoDecoded ?? blankImg;
  }

  // Caller — owns both, disposes both
  using var blank   = SKImage.FromEncodedData(defaultData);
  using var decoded = photo != null ? SKImage.FromEncodedData(photoData) : null;
  var result = pickImage(photo, blank);  // result is a non-owning alias — do NOT dispose it separately
  ```

- **Never chain SkiaSharp factory/constructor calls**: Avoid passing the result of one SkiaSharp factory method directly as an argument to another (e.g. `SKImage.FromEncodedData(SKData.CreateCopy(bytes))`). The intermediate object produced by the inner call (`SKData` in this example) is never assigned to a variable and therefore can never be disposed — it leaks native memory. Always assign each intermediate result to a local `using var` first.

  ```csharp
  // Wrong — the SKData produced by CreateCopy is never disposed (native memory leak)
  using var image = SKImage.FromEncodedData(SKData.CreateCopy(bytes));

  // Correct — each intermediate object has its own using var
  using var skData = SKData.CreateCopy(bytes);
  using var image  = SKImage.FromEncodedData(skData);
  ```

  This rule applies to any depth of nesting. If a call chain has N SkiaSharp factory/constructor calls, introduce N local `using var` declarations — one per call.

- **Helper / factory methods that return a SkiaSharp disposable**: When a private or local method creates and **returns** an `SK*` object, the method itself must **not** apply `using var` to the returned value — doing so would dispose the object before the caller can use it. **Ownership transfers to the caller**, which must capture the result with `using var`:

  ```csharp
  // Method — do NOT put using var on the object being returned; caller takes ownership
  SKImage getImage(string photo)
  {
      var stream = photo != null ? new MemoryStream(Convert.FromBase64String(...)) : null;
      return stream != null ? SKImage.FromEncodedData(stream) : null;  // caller owns this
  }

  // Caller — MUST use `using var` to capture and dispose the returned SKImage
  using var logo = getImage(picture);  // owned here; disposed when scope exits
  report.Logo = logo;
  var result = await report.Render();  // logo is still alive during the call
  // logo disposed here
  ```

  The common mistake is assigning the result directly to a property or discarding the local variable:

  ```csharp
  // Wrong — returned SKImage is never disposed (native memory leak)
  report.Logo = getImage(picture);

  // Wrong — var without using; GC may never finalize the native resource
  var logo = getImage(picture);
  report.Logo = logo;
  ```

  **Rule**: Every call to a method whose return type is a SkiaSharp disposable (`SKImage`, `SKBitmap`, `SKData`, `SKSurface`, etc.) must be treated exactly like a static factory call — capture the result with `using var` in the calling scope. This applies to private helpers, extension methods, and any other method that returns an `SK*` type.

  When the returned object must be assigned to a property before a render/processing call, use the same local-`using-var`-then-assign pattern from the previous section:

  ```csharp
  using var logo = getImage(picture);
  report.Logo = logo;
  var result = await report.Render();
  // logo disposed here — after Render() has finished
  ```

### Step 2 — Update NuGet Packages
```xml
<!-- Remove (or stop using) -->
<PackageReference Include="System.Drawing.Common" />

<!-- Add -->
<PackageReference Include="SkiaSharp" Version="3.*" />
<!-- Optional: native assets for Linux/macOS in self-contained apps -->
<PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="3.*" Condition="$([MSBuild]::IsOSPlatform('Linux'))" />
```

### Step 3 — Replace `using` Directives
```csharp
// Remove
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

// Add
using SkiaSharp;
```

### Step 4 — Apply Type Mappings
Consult [./references/type-mappings.md](./references/type-mappings.md) for the full table.

Key substitutions at a glance:

| System.Drawing | SkiaSharp |
|---|---|
| `Bitmap` | `SKBitmap` (mutable) / `SKImage` (immutable) |
| `Graphics` | `SKCanvas` |
| `Color` | `SKColor` |
| `Pen` | `SKPaint` with `Style = SKPaintStyle.Stroke` |
| `SolidBrush` / `Brush` | `SKPaint` with `Style = SKPaintStyle.Fill` |
| `Font` | `SKFont` + `SKTypeface` |
| `Rectangle` / `RectangleF` | `SKRectI` / `SKRect` |
| `Point` / `PointF` | `SKPointI` / `SKPoint` |
| `GraphicsPath` | `SKPath` |
| `Matrix` | `SKMatrix` |
| `ImageFormat` | `SKEncodedImageFormat` |

### Step 5 — Migrate Drawing Operations

#### Canvas creation
```csharp
// Before
using var bmp = new Bitmap(width, height);
using var g   = Graphics.FromImage(bmp);

// After
using var bmp    = new SKBitmap(width, height);
using var canvas = new SKCanvas(bmp);
```

#### Colors
```csharp
// Before
Color c = Color.FromArgb(255, 30, 144, 255); // A,R,G,B

// After
SKColor c = new SKColor(30, 144, 255, 255);  // R,G,B,A  ← note argument order
// Named colors: SKColors.Red, SKColors.White, etc.
```

#### Pen / Brush → SKPaint
```csharp
// Before
using var pen   = new Pen(Color.Red, 2f);
using var brush = new SolidBrush(Color.Blue);

// After
using var strokePaint = new SKPaint { Color = SKColors.Red,  StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
using var fillPaint   = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill,   IsAntialias = true };
```

#### Drawing primitives
```csharp
// Lines
g.DrawLine(pen, x1, y1, x2, y2);
canvas.DrawLine(x1, y1, x2, y2, strokePaint);

// Rectangles
g.DrawRectangle(pen, rect);
canvas.DrawRect(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), strokePaint);

g.FillRectangle(brush, rect);
canvas.DrawRect(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), fillPaint);

// Ellipses / circles
g.DrawEllipse(pen, rect);
canvas.DrawOval(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), strokePaint);

// Clear
g.Clear(Color.White);
canvas.Clear(SKColors.White);
```

#### Text rendering
```csharp
// Before
using var font  = new Font("Arial", 12f, FontStyle.Bold);
using var brush = new SolidBrush(Color.Black);
g.DrawString("Hello", font, brush, 10f, 20f);

// After
using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
using var skFont   = new SKFont(typeface, 12f);
using var paint    = new SKPaint { Color = SKColors.Black, IsAntialias = true };
canvas.DrawText("Hello", 10f, 20f + skFont.Size, skFont, paint);
// SkiaSharp baseline is at Y; add font.Size to match GDI+ top-left origin.
```

#### Measure text
```csharp
// Before
SizeF size = g.MeasureString("Hello", font);

// After
float width = paint.MeasureText("Hello");          // width only
SKRect bounds = default;
paint.MeasureText("Hello", ref bounds);            // full bounding box
```

#### Image I/O
```csharp
// Load
// Before:  using var bmp = new Bitmap("input.png");
using var bmp = SKBitmap.Decode("input.png");

// Draw image onto canvas
// Before:  g.DrawImage(src, destX, destY);
canvas.DrawBitmap(bmp, destX, destY);

// Save
// Before:  bmp.Save("output.png", ImageFormat.Png);
using var image  = SKImage.FromBitmap(bmp);
using var data   = image.Encode(SKEncodedImageFormat.Png, 100);
using var stream = File.OpenWrite("output.png");
data.SaveTo(stream);
```

#### GraphicsPath → SKPath
```csharp
// Before
using var path = new GraphicsPath();
path.AddLine(0, 0, 100, 100);
path.AddArc(rect, 0, 90);
path.CloseFigure();

// After
using var path = new SKPath();
path.MoveTo(0, 0);
path.LineTo(100, 100);
path.ArcTo(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), 0, 90, false);
path.Close();

canvas.DrawPath(path, strokePaint);
```

#### Transforms
```csharp
// Before
g.TranslateTransform(50, 50);
g.RotateTransform(45f);
g.ScaleTransform(2f, 2f);

// After — use save/restore to scope transforms
canvas.Save();
canvas.Translate(50, 50);
canvas.RotateDegrees(45f);
canvas.Scale(2f, 2f);
// ... drawing ...
canvas.Restore();
```

#### Images assigned to report/component properties before a method call
When `SKImage` objects are created and immediately assigned to properties before a render or processing call, they are never disposed. Use local `using var` declarations, assign to the property, then call the method — the images stay alive until the `using` scope exits after the call:

```csharp
// Wrong — all three SKImages leak
rpt.logo    = SKImage.FromEncodedData(logoStream);
rpt.reverso = SKImage.FromEncodedData(reversoStream);
rpt.icon    = SKImage.FromEncodedData(iconStream);
var result = await rpt.Render();

// Correct — local using vars; disposed after Render() returns
using var logo    = SKImage.FromEncodedData(logoStream);
using var reverso = SKImage.FromEncodedData(reversoStream);
using var icon    = SKImage.FromEncodedData(iconStream);
rpt.logo    = logo;
rpt.reverso = reverso;
rpt.icon    = icon;
var result = await rpt.Render();  // images remain alive during rendering
// logo, reverso, icon disposed here — after Render() has finished
```

### Step 6 — Memory Management
Both APIs are `IDisposable`. Use a **`using` declaration (`using var x = ...`)** for every SkiaSharp object — this is the preferred and required pattern. Explicit `.Dispose()` calls are only acceptable inside `IDisposable.Dispose()` implementations or `finally` blocks when ownership transfer makes `using var` impractical; in all other cases prefer `using var`. The table below lists all common disposable types:

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

#### Disposal audit — do this before finalizing any migration

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

**Also scan for separated declaration and assignment**: search for lines of the form `SKxxx varName;` (type-only declaration with no initializer). Each one is a potential leak — find where the variable is assigned and apply Option A, B, or C from the Step 1 constraint above.

### Step 7 — Validate
1. Build the project — resolve any remaining `CS0246` (missing types).
2. Run existing tests, or generate new ones comparing pixel output / file sizes.
3. Check `IsAntialias = true` on paints where the original used `Graphics.SmoothingMode`.
4. **Disposal scan**: grep the migrated file(s) for `new SK` and for factory methods (`SKImage.From`, `SKBitmap.Decode`, `SKTypeface.From`, `SKData.Create`, `SKSurface.Create`, `SKShader.Create`, `SKColorFilter.Create`, `SKMaskFilter.Create`, `SKImageFilter.Create`). Every match must have `using var` on the same line. If a match stores into a field instead, verify the owning class implements `IDisposable` and disposes the field there. Fix any that are missing.
5. **Caller-side disposal of returned SkiaSharp objects**: Search for every method whose declared return type is an `SK*` type (e.g. `SKImage`, `SKBitmap`, `SKData`). At every call site of that method, verify the result is captured with `using var`. A bare property assignment — `rpt.Logo = getImage(picture)` — or a plain `var logo = getImage(picture)` without `using` is a leak. Apply the local-`using-var`-then-assign pattern described in Step 1.
6. **No chained SkiaSharp calls**: Search for patterns like `SKImage.From*(SK*.Create*(...))`, `SKImage.From*(SKData.CreateCopy(...))`, or any other call where an `SK*` factory/constructor result is passed directly as an argument to another `SK*` call. Every intermediate result must be split into its own `using var` line.
7. **No disposal of parameters**: Search for `?.Dispose()` or `.Dispose()` calls inside methods where the disposed variable came from a parameter (not created locally). Any such call is wrong — remove it and ensure the caller handles disposal instead.

## Known Limitations / Non-trivial Cases
- **`Graphics.FromHwnd` / GDI handles**: No direct equivalent — use platform-specific rendering instead.
- **`Metafile` (EMF/WMF)**: SkiaSharp has no built-in support; consider `PdfSharp` or `SVGSkia` as alternatives.
- **`PrintDocument` / `PrintGraphics`**: Out of scope; use platform print APIs.
- **`TextRenderer` (GDI text)**: Replaced by SkiaSharp's text rendering; output may differ slightly at small sizes.
- **`ImageAttributes` / color matrices**: Use `SKColorFilter` (`SKColorFilter.CreateColorMatrix`).
- **`Region`**: Use `SKRegion` for pixel regions or `SKPath` clipping (`canvas.ClipPath(path)`).
