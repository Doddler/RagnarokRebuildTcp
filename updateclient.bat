cd RoRebuildServer\GameConfig
dotnet build -c Release --property WarningLevel=0
cd ../..
cd RoRebuildServer\RebuildSharedData
dotnet build -c Release --property WarningLevel=0
cd ../..
if not exist "RebuildClient\Assets\Data\" mkdir "RebuildClient\Assets\Data\"
copy /b/v/y "RoRebuildServer\GameConfig\bin\Release\netstandard2.0\GameConfig.dll" "RebuildClient\Assets\Data\GameConfig.dll"
copy /b/v/y "RoRebuildServer\RebuildSharedData\bin\Release\netstandard2.1\RebuildSharedData.dll" "RebuildClient\Assets\Data\RebuildSharedData.dll"
cd RoRebuildServer\DataToClientUtility
dotnet build -c Release --property WarningLevel=0
cd "bin\Release\net8.0\"
DataToClientUtility.exe
pause