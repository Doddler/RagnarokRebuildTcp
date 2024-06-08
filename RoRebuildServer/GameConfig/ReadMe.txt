GameConfig is a central place to put all of the clietn and server data files.
You should NOT include any c# scripts here, place those within RebuildSharedData.
This dll (without files) is given to the client so it can make use of types 
created via source generators (like CharacterSkill) so it's best to keep it clean of c#.