# Third-party notices

Parts of this engine are derived from other people's work. All of it is under the MIT licence, which permits use,
modification and distribution — including inside a closed product — on one condition: the copyright notice and the
licence text travel with the code. The per-file headers in the sources are that notice and must not be removed; this
file carries the licence texts they refer to.

Listed here is code that was *taken in and edited*, not packages consumed as dependencies — those carry their own
licences with them.

**On modifications.** All of this has been changed, some of it heavily. That is what the licence allows, and it does not
transfer anything: the original notice stays with a derived file for as long as any of the original remains in it, and
the changes themselves belong to this project. Both statements are meant to be read together — the header says whose
work it started as, this file says it did not stay that way.

---

## FJCore — JPEG codec

**Where:** `Adamantium/Adamantium.Imaging/Jpeg/` (22 files: decoder, encoder, DCT, colour models, filters)

A pure C# JPEG codec, originally a Fluxcapacity Open Source project by Jeffrey Powers, later mirrored on GitHub after
Google Code shut down. Upstream is no longer maintained — the last code change was in 2016 — which changes nothing about
the grant: an MIT licence, once given, is not withdrawn by a project going quiet.

Our copy diverged from upstream long ago and keeps diverging: it was reworked to fit this engine's imaging types, and
most recently the inverse DCT was rebuilt for speed and a forced `GC.Collect()` taken out of the decode path. Those
changes are this project's; the notice below covers what they were made to.

```
Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Later maintainers of the same codebase state their copyright as
`(c) 2008-2009 Occipital Open Source, (c) 2010-2013 Brian Donahue, (c) 2012-2013 Anders Gustafsson, Cureos AB`,
under the same licence.

**What it supports**, so nobody expects more from it than it has: baseline (SOF0) and progressive (SOF2) JPEG. Not
JPEG 2000 — that is a different format built on wavelets, and there is no code for it here.

---

## SharpDX

**Where:** ten files still carry its notice, and only those ten are derived from it —

- `Adamantium.Imaging`: `ImageDescription.cs`, `PixelBuffer.cs`, `PixelBufferArray.cs`, and DDS handling
  (`DdsHelper.cs`, `DdsFlags.cs`, `FourCC.cs`, `HeaderDXT10.cs`)
- `Adamantium.Core`: `DataBuffer.cs`, `NamedObject.cs`
- `Adamantium.Mathematics`: `ViewportF.cs`

Everything else that once came from it has been rewritten and carries no notice.

**Its own ancestry.** SharpDX did not write its maths either: that part came in from SlimMath, itself a port of the
maths in SlimDX, under MIT/X11. So `ViewportF.cs` — and anything else here descended from that maths — traces back
further than the header says. The terms are the same all the way down, which is why one notice covers the chain; it is
recorded because provenance is worth knowing accurately, not because a second obligation exists.

Maths is also where this project keeps rewriting. As a file stops containing any of the original, its notice stops
applying and can go — but that is judged per file, on what is actually left in it, not on how much work has been done
around it.

```
Copyright (c) 2010-2014 SharpDX - Alexandre Mutel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## webgl-noise — simplex noise

**Where:** `Adamantium/Adamantium.UI/Effects/NoiseMath.fxh` (the 2D simplex noise function, ported to Slang)

```
Copyright (C) 2011 by Ashima Arts (Simplex noise)
Copyright (C) 2011-2016 by Stefan Gustavson (Classic noise and others)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
