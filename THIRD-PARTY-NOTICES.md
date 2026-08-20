# Third-Party Notices

Zshot includes or is derived from third-party software. This file records those obligations.

## Starshot

Zshot contains code derived from [Starshot](https://github.com/loliri/Starshot) by loliri.

Starshot is licensed under the MIT License. The original copyright notice is:

```
Copyright (c) 2026 loliri
```

See `LICENSE` for the full MIT text.

## Other dependencies

The following libraries are used by Zshot. Their licenses are those of the respective upstream projects:

- Starward / Starward.Codec / Starward.Win2D
- Windows App SDK, Win2D, CsWinRT
- CommunityToolkit
- Serilog
- Dapper / Microsoft.Data.Sqlite
- Vanara.PInvoke
- H.NotifyIcon.WinUI
- SharpCompress
- SemanticVersioning
- TaskScheduler
- ComputeSharp.D2D1.WinUI
- SkiaSharp (MIT) — pulled in by RapidOcrNet as its image backend
- Microsoft.ML.OnnxRuntime (MIT) — OCR inference runtime
- Inno Setup — Windows installer builder

## OCR (Apache License 2.0)

Zshot performs local text recognition with [RapidOcrNet](https://github.com/BobLd/RapidOcrNet)
by BobLd and RapidAI, licensed under the Apache License 2.0.

The bundled recognition models are PP-OCRv6 (detection + recognition) and the PP-OCRv5
text-line orientation classifier, from [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR)
by PaddlePaddle, also licensed under the Apache License 2.0. Model files are redistributed
in the release package under `models/`.

You may obtain a copy of the Apache License 2.0 at:

```
http://www.apache.org/licenses/LICENSE-2.0
```

Neither project's files were modified. OCR runs entirely on the local machine;
screenshots are never uploaded.

Windows.Media.Ocr (part of Windows) is used as a fallback when the model files are absent.
