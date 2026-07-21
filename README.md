![Blazor](https://img.shields.io/badge/Blazor-black?logo=blazor)
![WebAssembly](https://img.shields.io/badge/WebAssembly-black?logo=webassembly)
![OpenCV](https://img.shields.io/badge/OpenCV-black?logo=opencv)

# A WebAssembly- and computer vision-based open-source level solver for Meowdoku!

The app allows you to solve levels from the mobile game Meowdoku! ([Google Play](https://play.google.com/store/apps/details?id=com.oakever.meowdoku), [App Store](https://apps.apple.com/us/app/meowdoku/id6761760135))

Levels are loaded from the game by recognizing a screenshot of the level or manually.

The app is built using [Blazor WASM](https://learn.microsoft.com/ru-ru/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0#blazor-webassembly) and [OpenCV](https://opencv.org/), allowing it to run entirely client-side, without the need for a backend.