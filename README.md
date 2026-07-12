# Ragnarok Rebuild (TCP/IP Websocket Ver.)

Server and client for a Ragnarok Online-like game. Some assembly required.

**Note**: This repository contains no game assets from Ragnarok Online. You will need to provide those yourself.

**Warning**: The code is horrifying, would not recommend reading.

## Requirements

* Unity 6000.3.19f1 or higher
* .NET 9
* Lack of sanity

## Setting things up

* Load up the server csproj project, and make sure the server is able to build successfully. You don't need to run it yet.
* In the root project directory, run "updateclient.bat". This copies server definitions and data over to the client.
* Open the client directory in Unity.
* From the Ragnarok menu, select "Set Ragnarok Data Directory". This should be the path where you have the files extracted from data.grf from an original client. For this import process to work correctly, the files will need to have been extracted with the right locale and have working korean file names.
* From the Ragnarok menu, select "Client Data Health Check". Be sure to set aside a lot of time for the import process to complete as it may take several hours. You can import less at a once if the time is a concern, but it's recommended you import items from the top going down. If you want to process fewer maps, you can import them one at a time via the 'Ragnarok -> Import Maps' option, or you can trim the files RoRebuildServer/ServerData/Db/Maps.csv and Instances.csv to only have a small subset of maps, and then rerun the updateclient.bat.
* Copy any of your BGM over from your ragnarok client into the Music folder.
* From the Rangarok menu, select "Update Addressables" to ensure everything you've imported can be loaded at runtime. Any time you add sprites or maps, you will need to do this again.
* From the Ragnarok menu, select "Lighting Manager". Drag all the scenes in the Scenes\\Maps folder into the Scenes section of the Lighting Manager window. Then, click 'bake all scenes' (not bake all). This can take several hours depending on your GPU. You can skip this step but maps will not display with any lighting until you do.
Note: There's a known issue where rarely the lighting bake process can create washed out lightmaps. If you have this occur simply re-run the bake for that map specifically (Set the scenes array to '0' and then only add the scene you want to bake).
* Once lighting is baked, you can select the option 'Make Minimaps' to generate minimap images.
* At this point, you should be able to run the server. Make sure you set visual studio to run the server as a standalone rather than via IIS or IIS express (drop down next to the run button).
* Once the server is running, hitting play in the editor should allow you to connect.

## Special Considerations

* A number of custom monsters exist within the project for which existing sprites do not exist, but like the other RO sprites these are excluded to avoid the project holding copywritten material. If you don't do something with these monsters they will appear in game with the default poring sprite so you may want to matching sprites for these monsters, adjust the sprite they use to other monsters, or simply remove them. These monsters are defined in ServerData/Db/Monsters.csv and have an ID starting with 6000 onwards, you can change the sprite field here or remove the monster entry entirely (the server will start with ignorable warnings). Remember when making changes to the server data to run updateclient.bat afterwards so the client remains up to date.

## Contribution Guidelines

If you want to contribute to the project, you are free to join the project discord ([link here](https://discord.gg/gyPdMRF76G)). Anyone is free to offer code or features to the project via pull requests, though I have final discretion over what gets added to the project. I am often busy so it can take a while before I review pull requests, so please be patient.

When working on the project, please be sure to take note of the following guidelines:

* All code and assets needs to be your own, and if you need to bring in libraries ask first. Submitting code or assets is implicitly giving permission to use it perpetually as part of the project going forward.
* When adding new features it would be best if you could find a way to make them optional. For example if you were to make a new party window/frame, it would be ideal to make an interface so that either the existing or new party frame could be used based on what prefab is used. Added features may be made default in the future, but it's best not to assume.
* If you're fully replacing or upgrading existing features (upgrading unity, replacing core systems like UI or shaders) please consult in the discord first to ensure it's something we want to do.
* AI Code: I have no bias for code submitted as long as it is clean, readable, and free of obvious issues. However please write your pull requests by hand.
* AI Art: I'd prefer AI art assets not be committed to the project repository, only assets you've created yourself or properly sourced royalty free assets. With issues surrounding generative AI and the fact that all downstream projects would be affected I'd simply rather not deal with it.

