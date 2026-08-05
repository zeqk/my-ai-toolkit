# Drawing Operations — System.Drawing → SkiaSharp

## Canvas creation
```csharp
// Before
using var bmp = new Bitmap(width, height);
using var g   = Graphics.FromImage(bmp);

// After
using var bmp    = new SKBitmap(width, height);
using var canvas = new SKCanvas(bmp);
```

## Colors
```csharp
// Before
Color c = Color.FromArgb(255, 30, 144, 255); // A,R,G,B

// After
SKColor c = new SKColor(30, 144, 255, 255);  // R,G,B,A  ← note argument order
// Named colors: SKColors.Red, SKColors.White, etc.
```

## Pen / Brush → SKPaint
```csharp
// Before
using var pen   = new Pen(Color.Red, 2f);
using var brush = new SolidBrush(Color.Blue);

// After
using var strokePaint = new SKPaint { Color = SKColors.Red,  StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
using var fillPaint   = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill,   IsAntialias = true };
```

## Drawing primitives
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

## Text rendering
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

## Measure text
```csharp
// Before
SizeF size = g.MeasureString("Hello", font);

// After
float width = paint.MeasureText("Hello");          // width only
SKRect bounds = default;
paint.MeasureText("Hello", ref bounds);            // full bounding box
```

## Image I/O
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

## GraphicsPath → SKPath
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

## Transforms
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

## Images assigned to report/component properties before a method call

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
