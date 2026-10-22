# SkeletonCommander (dawniej ValheimSkeletonTracker)

## Komendy
- Budowanie i deploy: `dotnet build`
- Czyszczenie: `dotnet clean`
- Dekompilacja klas gry: `ilspycmd -t <NazwaKlasy> ./lib/assembly_valheim.dll`

## Środowisko i Ścieżki
- Środowisko: Ubuntu (Linux), .NET SDK 10 (Target Framework: `netstandard2.1`)
- Referencje gier (DLL): `./lib/`
- Ścieżka BepInEx Plugins: `/home/michal/snap/steam/common/.steam/steam/steamapps/common/Valheim/BepInEx/plugins/`

## Zasady i Konwencje
- Wykorzystuj BepInEx 5 oraz Harmony.
- Plik `.csproj` zawiera MSBuild Target (`AfterTargets="Build"`), który po `dotnet build` kopiuje plik `.dll` do BepInEx/plugins
- W przypadku braku pewności co do metod lub zmiennych z Valheim, najpierw wykonaj `ilspycmd` na odpowiedniej klasie z `./lib/assembly_valheim.dll`.
