---
name: migrateToSkiaSharp
---
Migrate the current file from `System.Drawing` to `SkiaSharp`:

1. Add `using SkiaSharp;` to the imports if not already present.
2. Replace all `System.Drawing` types with their SkiaSharp equivalents (e.g., `Bitmap` → `SKBitmap`, `Image` → `SKImage`, `Color` → `SKColor`, `Graphics` → `SKCanvas`, `Pen` → `SKPaint`, `Brush` → `SKPaint`, etc.).
3. Replace any `Hashtable` variable whose name is `images` with `Dictionary<string, SKImage>`, updating the declaration and instantiation accordingly. When creating or replacing any variable as part of this migration, always use type inference (`var`) instead of an explicit type declaration (e.g., `var images = new Dictionary<string, SKImage>();`).
4. Remove `using System.Drawing;` (and related sub-namespaces) only if they are no longer referenced anywhere in the file after the migration.
5. Verify that the updated types are compatible with any method signatures or interfaces that consume them (e.g., service interfaces that already expect `IDictionary<string, SKImage>`).
6. For every call to a method of the `IReporting` service that has an `images` parameter, ensure the argument passed is typed as `Dictionary<string, SKImage>` (not `Hashtable` or any other collection type). Update the variable declaration and instantiation at the call site if needed.
7. Keep all other logic, structure, and unrelated usings unchanged.
