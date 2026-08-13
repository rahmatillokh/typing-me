# Screenshots

The root `README.md` embeds four screenshots from this folder: `menu.png`, `gameplay.png`,
`rankup.png` and `finale.png`.

**They are captured automatically** by a PlayMode tool (`Assets/Tests/PlayMode/ReadmeScreenshots.cs`)
that renders the real scenes into an off-screen 1920×1080 target — menu, mid-fight, the rank-up
interstitial and the campaign finale — and restores the save file afterwards. It is marked
`[Explicit]`, so normal test runs skip it. Re-capture after a visual change with:

```bash
/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity -batchmode -runTests -projectPath /Users/imac/Developer/typer -testPlatform PlayMode -testFilter "TypingMe.Tests.ReadmeScreenshots.CaptureAll" -testResults /tmp/shots.xml -logFile /tmp/shots.log
```

Prefer a hand-picked moment? Just overwrite any of the four PNGs with your own 16:9 capture —
nothing regenerates them unless the tool above is run explicitly.
