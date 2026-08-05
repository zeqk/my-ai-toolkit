# Disposal Rules — SkiaSharp Migration

## Core constraint — enforced at write time, not as a post-check

Every time you write a line that produces a SkiaSharp disposable, write `using var` on *that same line* before moving on. The three categories that produce disposables are:

1. **Constructors**: `new SKBitmap(...)`, `new SKPaint(...)`, `new SKPath()`, `new SKFont(...)`, `new SKPictureRecorder()`, `new SKRegion()`, …
2. **Static factory methods** on any `SK*` class: `.From*(…)`, `.Decode(…)`, `.Create*(…)`, `.Load*(…)` — e.g. `SKImage.FromEncodedData(...)`, `SKImage.FromBitmap(...)`, `SKBitmap.Decode(...)`, `SKTypeface.FromFamilyName(...)`, `SKData.CreateCopy(...)`, `SKSurface.Create(...)`, `SKShader.CreateLinearGradient(...)`, `SKColorFilter.CreateColorMatrix(...)`, `SKMaskFilter.CreateBlur(...)`, `SKImageFilter.CreateBlur(...)`.
3. **Instance methods that return a new disposable object**: `skImage.Encode(...)` → `SKData`, `skData.AsStream()` → `Stream`, `recorder.EndRecording()` → `SKPicture`, `surface.Snapshot()` → `SKImage`, `skBitmap.PeekPixels()` → `SKPixmap`, `skImage.PeekPixels()` → `SKPixmap`.

**Never** write `var x = SKImage.FromEncodedData(...)` — always `using var x = SKImage.FromEncodedData(...)`.

---

## Separated declaration and assignment

When a variable is declared on one line and assigned on another (`SKImage img; ... img = SKImage.From*(...)`) the `using var` modifier cannot be added at the assignment site. This is a **high-risk pattern** that almost always results in a leak. Apply one of these fixes in order of preference:

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

---

## Fallback / blank-image pattern

When a "default" image is created as a fallback and a ternary determines which image to use, a common mistake is leaving the unused branch undisposed. Use `using var` for the blank/default image, then separately decode the real image (or `null`) with `using var`, and use `??` to select a non-owning alias:

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

---

## Never dispose a received parameter

A method must not call `.Dispose()` (or `?.Dispose()`) on an `SK*` object it received as a parameter. Ownership belongs to whoever created the object; only the creator (or a scope above it) is responsible for disposal.

```csharp
// Wrong — the method disposes blankImg which it did NOT create
SKImage pickImage(string photo, SKImage blankImg)
{
    using var photoDecoded = photo != null ? SKImage.FromEncodedData(...) : null;
    if (photoDecoded != null)
    {
        blankImg?.Dispose();  // ← NEVER do this; caller owns blankImg
        return photoDecoded.ToRasterImage();
    }
    return blankImg;
}

// Correct — caller owns both objects and disposes them; callee only selects
SKImage pickImage(string photo, SKImage blankImg)
{
    using var photoDecoded = photo != null ? SKImage.FromEncodedData(...) : null;
    return photoDecoded ?? blankImg;  // non-owning alias; caller disposes both
}

// Caller — owns both, disposes both
using var blank   = SKImage.FromEncodedData(defaultData);
using var decoded = photo != null ? SKImage.FromEncodedData(photoData) : null;
var result = pickImage(photo, blank);  // result is a non-owning alias — do NOT dispose it separately
```

---

## Never chain SkiaSharp factory/constructor calls

Avoid passing the result of one SkiaSharp factory method directly as an argument to another. The intermediate object is never assigned to a variable and therefore can never be disposed — it leaks native memory.

```csharp
// Wrong — the SKData produced by CreateCopy is never disposed (native memory leak)
using var image = SKImage.FromEncodedData(SKData.CreateCopy(bytes));

// Correct — each intermediate object has its own using var
using var skData = SKData.CreateCopy(bytes);
using var image  = SKImage.FromEncodedData(skData);
```

This rule applies to any depth of nesting. If a call chain has N SkiaSharp factory/constructor calls, introduce N local `using var` declarations — one per call.

---

## Helper / factory methods that return a SkiaSharp disposable

When a private or local method creates and **returns** an `SK*` object, the method itself must **not** apply `using var` to the returned value — doing so would dispose the object before the caller can use it. **Ownership transfers to the caller**, which must capture the result with `using var`:

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

Common mistakes:
```csharp
// Wrong — returned SKImage is never disposed (native memory leak)
report.Logo = getImage(picture);

// Wrong — var without using; GC may never finalize the native resource
var logo = getImage(picture);
report.Logo = logo;
```

**Rule**: Every call to a method whose return type is a SkiaSharp disposable (`SKImage`, `SKBitmap`, `SKData`, `SKSurface`, etc.) must be treated exactly like a static factory call — capture the result with `using var` in the calling scope. When the returned object must be assigned to a property before a render call:

```csharp
using var logo = getImage(picture);
report.Logo = logo;
var result = await report.Render();
// logo disposed here — after Render() has finished
```

---

## Property-assignment leaks

Assigning an `SK*` factory result **directly to a property** (`owner.Prop = SKImage.From*(...)`) leaks native memory because there is no `using var` to tie the lifetime to a scope. Two valid patterns:

**Option A — owning class implements `IDisposable`** (preferred when the SK* object outlives the method):

```csharp
// Owner class disposes the SK* field in its own Dispose()
public class OwnerClass : IDisposable
{
    public SKImage ImageProperty { get; set; }
    public void Dispose() => ImageProperty?.Dispose();
}

// Caller — wrap owner in using var; SK* lives as long as owner
using var owner = new OwnerClass();
if (owner.ImageProperty == null)
{
    using var stream = GetStream("resource://asset.png");
    owner.ImageProperty = SKImage.FromEncodedData(stream); // owned by owner
}
await owner.DoWork();
// owner.Dispose() → ImageProperty.Dispose() called here
```

**Option B — short-lived local** (only when the owner is fully used inside the same scope):

```csharp
// ONLY valid when every use of owner happens before the using scope exits
using var skObject = SKImage.FromEncodedData(stream);
owner.ImageProperty = skObject;
owner.DoWork();                // must happen BEFORE the using scope exits
owner.ImageProperty = null;    // defensive: clear before skObject is disposed
```

> **⚠ Use-after-dispose trap**: if the `SK*` factory call is inside a nested block (`if`, `foreach`, `using`) but the owner is used **after** that block, the SK* object is already disposed when the owner tries to use it — wrong even though it compiles:
> ```csharp
> // WRONG — skObject is disposed at the end of the if block
> if (owner.ImageProperty == null)
> {
>     using var skObject = SKImage.FromEncodedData(stream); // disposed on }
>     owner.ImageProperty = skObject;
> }
> await owner.DoWork(); // use-after-dispose!
> ```
> In this pattern Option B is **not applicable** — use Option A instead.

Prefer Option A whenever the owner is used outside the scope where the SK* object was created, or when the owner class can be made `IDisposable`.
