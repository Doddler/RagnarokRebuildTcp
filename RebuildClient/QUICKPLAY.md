# QuickPlay

Copy `quickplay.example.json` to `quickplay.local.json` in this directory and fill in your existing account credentials and exact character name. The local file is ignored by Git and stays outside Unity Assets.

Start the server and open `Assets/Scenes/MainScene.unity`. Select the QuickPlay GameObject and enable its QuickPlayLauncher component before pressing Play. It automatically enters your configured character without reloading the scene. The component is disabled by default; leave it disabled for manual login. It is Editor-only; build settings and normal manual login are unchanged. No snapshot code is used.

Configuration errors appear in the Console; login and character errors use the normal error UI. There are no automatic retries. Stop and restart Play to retry after correcting configuration.

Use this real-server workflow for Classic UI development. The standalone ClassicUI scene no longer seeds player data; offline fixtures and snapshot export are no longer available.
