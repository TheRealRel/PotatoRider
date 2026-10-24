#!/bin/bash
# Paths: VALHEIM_MANAGED = Valheim/valheim_Data/Managed, BEPINEX = a profile's BepInEx folder (with Jotunn installed).
M="${VALHEIM_MANAGED:-/mnt/user-data/uploads/Managed}"; B="${BEPINEX:-/mnt/user-data/uploads/Potato Server-Tester/BepInEx}"
J="${JOTUNN:-$B/plugins/ValheimModding-Jotunn/Jotunn.dll}"
CSC="${CSC:-dotnet /usr/lib/dotnet/sdk/8.0.131/Roslyn/bincore/csc.dll}"
python3 -I check_config_names.py src/*.cs || { echo "Build stopped: fix the setting names above."; exit 1; }
mkdir -p out
$CSC -nologo -nostdlib -noconfig -target:library -optimize+ -deterministic -langversion:9 -out:out/PotatoRider.dll -r:"$M/mscorlib.dll" -r:"$M/netstandard.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/assembly_valheim.dll" -r:"$M/assembly_utils.dll" -r:"$M/assembly_guiutils.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.PhysicsModule.dll" -r:"$M/UnityEngine.UI.dll" -r:"$M/UnityEngine.AnimationModule.dll" -r:"$B/core/BepInEx.dll" -r:"$B/core/0Harmony.dll" -r:"$J" src/*.cs
