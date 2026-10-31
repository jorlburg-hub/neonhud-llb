#!/bin/sh
set -e
cd "$(dirname "$0")"
R="-nostdlib -r:/usr/lib/mono/2.0-api/mscorlib.dll -r:/usr/lib/mono/2.0-api/System.dll -r:/usr/lib/mono/2.0-api/System.Core.dll"
mkdir -p out
mcs $R -target:library -out:out/UnityEngine.dll UnityEngine.cs UnityGuiMock.cs
mcs $R -target:library -r:out/UnityEngine.dll -out:out/BepInEx.dll BepInEx.cs
mcs $R -target:library -r:out/UnityEngine.dll -r:out/BepInEx.dll -out:out/NeonHUD.dll -recurse:'../src/*.cs' TestAccess.cs
mcs -target:exe -r:out/UnityEngine.dll -r:out/BepInEx.dll -r:out/NeonHUD.dll -out:out/Assembly-CSharp.exe Tests.cs FakeScreens.cs
cd out && mono Assembly-CSharp.exe
