---
name: migrate-system-drawing-to-skiasharp
description: 'Migrate C# code from System.Drawing (GDI+) to SkiaSharp. Use when porting Bitmap, Graphics, Pen, Brush, Font, Color, GraphicsPath, or any System.Drawing type to SkiaSharp equivalents. Covers NuGet setup, type mapping, drawing operations, color conversion, text rendering, image I/O, transforms, and memory management. Triggers: System.Drawing migration, SkiaSharp port, GDI+ to Skia, cross-platform graphics, PlatformNotSupportedException System.Drawing.'
argument-hint: 'Path or description of the C# file(s) to migrate'
---

# Migrate System.Drawing → SkiaSharp

## When to Use
- Porting a .NET app to be cross-platform (Linux/macOS/containers) where `System.Drawing.Common` throws `PlatformNotSupportedException` on .NET 6+.
- Modernizing image-processing, chart-generation, or thumbnail code.
- The user has files with `using System.Drawing`, `System.Drawing.Imaging`, `System.Drawing.Drawing2D`, or `System.Drawing.Text`.

## Procedure

### Step 1 — Audit the Code
Scan the target file(s) for all `System.Drawing` usages:
- Collect every type, method, and property referenced.
- Flag non-trivial areas: `IDeviceContext`, GDI handles, `Graphics.FromHwnd`, `Metafile`, `PrintDocument`.

> Load `./references/disposal-rules.md` now and keep it active throughout — apply its rules at every step of the migration.

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
> Load `./references/type-mappings.md` for the complete mapping table.

Key gotcha: **`SKColor` uses R,G,B,A argument order** — the opposite of GDI+'s A,R,G,B.

### Step 5 — Migrate Drawing Operations
> Load `./references/drawing-operations.md` **before writing any migrated code** — it contains before/after examples for: canvas creation, colors, Pen/Brush → SKPaint, drawing primitives, text rendering, measure text, image I/O, GraphicsPath → SKPath, transforms, and the property-assign pattern for images used in render calls.

### Step 6 — Memory Management
> Load `./references/memory-management.md` — all SK* objects hold unmanaged native memory not reclaimed by the GC. See the full disposable-types table and 3-category audit checklist (constructors, static factories, instance methods returning disposables).

### Step 7 — Validate
1. Build the project — resolve any remaining `CS0246` (missing types).
2. Run existing tests, or generate new ones comparing pixel output / file sizes.
3. Check `IsAntialias = true` on paints where the original used `Graphics.SmoothingMode`.
4. Run the disposal audit from `./references/memory-management.md` — verify every `new SK*` and every static factory/instance-method result has `using var`.
5. Run the disposal rule checks from `./references/disposal-rules.md` — verify: no chained SK* calls, no disposal of received parameters, `using var` on every caller-side factory-return capture, and no property-assignment leaks (Options A/B).

## Known Limitations / Non-trivial Cases
- **`Graphics.FromHwnd` / GDI handles**: No direct equivalent — use platform-specific rendering instead.
- **`Metafile` (EMF/WMF)**: SkiaSharp has no built-in support; consider `PdfSharp` or `SVGSkia` as alternatives.
- **`PrintDocument` / `PrintGraphics`**: Out of scope; use platform print APIs.
- **`TextRenderer` (GDI text)**: Replaced by SkiaSharp's text rendering; output may differ slightly at small sizes.
- **`ImageAttributes` / color matrices**: Use `SKColorFilter` (`SKColorFilter.CreateColorMatrix`).
- **`Region`**: Use `SKRegion` for pixel regions or `SKPath` clipping (`canvas.ClipPath(path)`).
