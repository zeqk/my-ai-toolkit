---
name: migrateToSkiaSharpByFile
---

Migrate the following file from System.Drawing to SkiaSharp following the [migrate-system-drawing-to-skiasharp skill](.github\skills\migrate-system-drawing-to-skiasharp\SKILL.md):

{{target_file}}

Focus lines (optional): {{target_lines}}

If `{{target_lines}}` is provided, do NOT scan the entire file line by line. Instead:
- Read only the indicated lines and their immediate context.
- Identify and migrate only those lines plus any other lines directly affected by those changes (e.g. variable declarations, dependent usings, method signatures that reference changed types).
- Leave all other lines untouched.

If `{{target_lines}}` is not provided, apply the migration to the entire file.

Important constraints:
- Modify **only** the file indicated in {{target_file}}. Do not open, read, or change any other file.
- Do not run project build or tests. Other file refactorings may be running in parallel.
- Skip Step 2 (NuGet package changes) — package references are managed separately.

Project-specific rules (apply on top of the skill):
- Replace any Hashtable variable named `images` with `Dictionary<string, SKImage>`, updating declaration and instantiation accordingly.
- When creating or replacing any variable as part of this migration, always use type inference with `var` (e.g. `var images = new Dictionary<string, SKImage>();`).
- Verify that updated types remain compatible with method signatures or interfaces that consume them (e.g. interfaces expecting `IDictionary<string, SKImage>`).
- For every call to an `IReporting` service method that has an `images` parameter, ensure the argument is typed as `Dictionary<string, SKImage>` (not `Hashtable` or another collection type).
- When calling `a1f.edu.services.imageGetter.IImageGetter.getImage` with a `fallbackImage` or `blankImage` parameter, it is safe to dispose that image after the method invocation because `getImage` only copies it and does not retain/reuse the original instance.
- For calls to `a1f.edu.services.imageGetter.IImageGetter` that return SkiaSharp objects, ensure returned disposable objects are disposed following the migration instructions.
- When the return value of an `IImageGetter` call is assigned **directly to an object property** (e.g. `rpt.Photo = _imageGetter.getImage(...);`), you cannot dispose it inline. Instead:
  1. Assign the return value to a local `var` first.
  2. Assign that local variable to the property.
  3. Track the local variable for disposal — add it to the nearest enclosing `try/finally` or dispose block, or append a `localVar?.Dispose();` call at the earliest point after the property owner is no longer needed.
  - Example — **before**: `rpt.Photo = _imageGetter.getImage(studentData.Photo, fallbackImage);`
  - Example — **after**:
    ```csharp
    var photo = _imageGetter.getImage(studentData.Photo, fallbackImage);
    rpt.Photo = photo;
    // ... use rpt ...
    photo?.Dispose();
    ```
- Keep all other logic, structure, and unrelated usings unchanged.
