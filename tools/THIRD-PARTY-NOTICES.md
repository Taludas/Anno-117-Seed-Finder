# Third-party notices

## Anno 117 Layout Tool (savegame parsing)

`savegame_map_dump.py` is adapted from `savegame_parser.py` of the Anno 117 Layout Tool (repository `anno117-layouttool`), which is distributed under the MIT License. The extraction pipeline (RdaConsole, zlib, FileDBReader, streaming the session XML) and the binary field decoding come from that script. The parts that read the MapTemplate section are new.

The MIT License requires that the copyright notice and this permission notice stay with the code:

> MIT License
>
> Copyright (c) 2026 Taludas
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Savegame format reference

The field layout of the savegame's map data follows `AnnoDesigner.Import/docs/Anno117_Savegames.md` in the `anno-designer` repository (branch "Savegames").

## External programs (not included)

RdaConsole and FileDBReader are separate community tools and are not part of this repository. Follow their own licences when you download them.