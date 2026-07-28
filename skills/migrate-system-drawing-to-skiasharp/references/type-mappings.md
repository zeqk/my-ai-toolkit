# System.Drawing → SkiaSharp Full Type Mapping

## Core Types

| System.Drawing | SkiaSharp | Notes |
|---|---|---|
| `Bitmap` | `SKBitmap` | Mutable pixel data |
| `Image` | `SKImage` | Immutable snapshot; use `SKImage.FromBitmap()` |
| `Graphics` | `SKCanvas` | Obtain via `new SKCanvas(bitmap)` |
| `Color` | `SKColor` | Arg order: R,G,B,A (vs A,R,G,B in GDI+) |
| `KnownColor` names | `SKColors.*` | e.g. `SKColors.Red` |
| `Pen` | `SKPaint` + `SKPaintStyle.Stroke` | |
| `SolidBrush` | `SKPaint` + `SKPaintStyle.Fill` | |
| `TextureBrush` | `SKPaint` + `SKShader.CreateBitmap(...)` | |
| `LinearGradientBrush` | `SKPaint` + `SKShader.CreateLinearGradient(...)` | |
| `RadialGradientBrush` | `SKPaint` + `SKShader.CreateRadialGradient(...)` | |
| `HatchBrush` | `SKPaint` + custom `SKPathEffect` or `SKShader` | No direct equivalent |
| `Font` | `SKFont` + `SKTypeface` | |
| `FontFamily` | `SKTypeface.FromFamilyName(name)` | |
| `FontStyle` | `SKFontStyle` (Bold, Italic, BoldItalic, Normal) | |
| `StringFormat` | `SKTextAlign` + manual layout | No full equivalent |
| `Rectangle` | `SKRectI` | Integer rect |
| `RectangleF` | `SKRect` | Float rect |
| `Point` | `SKPointI` | Integer point |
| `PointF` | `SKPoint` | Float point |
| `Size` | `SKSizeI` | Integer size |
| `SizeF` | `SKSize` | Float size |
| `GraphicsPath` | `SKPath` | |
| `PathGradientBrush` | `SKPaint` + `SKShader.CreateSweepGradient(...)` | Approximate |
| `Matrix` | `SKMatrix` | |
| `Region` | `SKRegion` or `canvas.ClipPath(path)` | |
| `Icon` | Load via stream → `SKBitmap.Decode(stream)` | |
| `ImageFormat` | `SKEncodedImageFormat` | |
| `ColorMatrix` | `SKColorFilter.CreateColorMatrix(float[20])` | |
| `ImageAttributes` | `SKPaint.ColorFilter` | |

## Graphics State

| System.Drawing | SkiaSharp |
|---|---|
| `Graphics.Clear(color)` | `canvas.Clear(color)` |
| `Graphics.SmoothingMode` | `SKPaint.IsAntialias` |
| `Graphics.TextRenderingHint` | `SKPaint.IsAntialias` + `SKFont.Edging` |
| `Graphics.InterpolationMode` | `SKPaint.FilterQuality` |
| `Graphics.PixelOffsetMode` | No direct equivalent |
| `Graphics.CompositingMode` | `SKPaint.BlendMode` |
| `Graphics.Clip` | `canvas.ClipRect()` / `canvas.ClipPath()` |
| `Graphics.Save()` / `Restore()` | `canvas.Save()` / `canvas.Restore()` |

## Drawing Methods

| System.Drawing | SkiaSharp |
|---|---|
| `DrawLine(pen, x1,y1,x2,y2)` | `canvas.DrawLine(x1,y1,x2,y2, paint)` |
| `DrawRectangle(pen, rect)` | `canvas.DrawRect(skRect, paint)` |
| `FillRectangle(brush, rect)` | `canvas.DrawRect(skRect, paint)` |
| `DrawEllipse(pen, rect)` | `canvas.DrawOval(skRect, paint)` |
| `FillEllipse(brush, rect)` | `canvas.DrawOval(skRect, paint)` |
| `DrawArc(pen, rect, start, sweep)` | `canvas.DrawArc(skRect, start, sweep, false, paint)` |
| `DrawPolygon(pen, points[])` | `canvas.DrawPoints(SKPointMode.Polygon, skPoints, paint)` |
| `FillPolygon(brush, points[])` | Build `SKPath`, call `canvas.DrawPath(path, paint)` |
| `DrawPath(pen, path)` | `canvas.DrawPath(skPath, paint)` |
| `FillPath(brush, path)` | `canvas.DrawPath(skPath, paint)` (fill paint) |
| `DrawString(s, font, brush, x, y)` | `canvas.DrawText(s, x, y+font.Size, skFont, paint)` |
| `DrawImage(img, x, y)` | `canvas.DrawBitmap(bmp, x, y)` |
| `DrawImage(img, destRect)` | `canvas.DrawBitmap(bmp, skDestRect)` |
| `DrawImage(img, destRect, srcRect, unit)` | `canvas.DrawBitmap(bmp, skSrcRect, skDestRect, paint)` |
| `CopyFromScreen(...)` | No equivalent (platform-specific capture needed) |

## Transform Methods

| System.Drawing | SkiaSharp |
|---|---|
| `TranslateTransform(dx, dy)` | `canvas.Translate(dx, dy)` |
| `RotateTransform(angle)` | `canvas.RotateDegrees(angle)` |
| `ScaleTransform(sx, sy)` | `canvas.Scale(sx, sy)` |
| `MultiplyTransform(matrix)` | `canvas.Concat(ref skMatrix)` |
| `ResetTransform()` | `canvas.ResetMatrix()` |
| `Transform` (get/set) | `canvas.TotalMatrix` / `canvas.SetMatrix(m)` |

## Pixel Access

| System.Drawing | SkiaSharp |
|---|---|
| `Bitmap.GetPixel(x, y)` | `bitmap.GetPixel(x, y)` → `SKColor` |
| `Bitmap.SetPixel(x, y, color)` | `bitmap.SetPixel(x, y, color)` |
| `Bitmap.LockBits(...)` | `bitmap.GetPixels()` → `IntPtr` or `Span<byte>` via unsafe |
| `BitmapData` | `SKPixmap` (via `bitmap.PeekPixels()`) |

## Image Encoding

| System.Drawing | SkiaSharp |
|---|---|
| `bmp.Save(path, ImageFormat.Png)` | `SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream)` |
| `bmp.Save(path, ImageFormat.Jpeg)` | `SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Jpeg, 85).SaveTo(stream)` |
| `Bitmap(stream)` | `SKBitmap.Decode(stream)` |
| `Bitmap(path)` | `SKBitmap.Decode(path)` |
| `Bitmap(width, height)` | `new SKBitmap(width, height)` |
| `Bitmap(width, height, PixelFormat.Format32bppArgb)` | `new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)` |
