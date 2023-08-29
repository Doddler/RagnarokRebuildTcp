cd RoRebuildServer\RebuildSharedData
dotnet build -c Release
cd ../..
xcopy "RoRebuildServer\RebuildSharedData\bin\Release\netstandard2.0\RebuildSharedData.dll" "RebuildClient\Assets\Data\RebuildSharedData.dll" /s /y
cd RoRebuildServer\DataToClientUtility
dotnet build -c Release
cd "bin\Release\net7.0\"
DataToClientUtility.exe
pause