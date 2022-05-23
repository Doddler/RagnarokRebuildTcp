using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.ClientTypes;

namespace Assets.Scripts.Sprites
{
	//[Serializable]
	//public class MonsterClassData
	//{
	//	public int Id;
	//	public string Name;
	//	public string SpriteName;
	//	public float Offset;
	//	public float ShadowSize;
	//}

	[Serializable]
	public class PlayerClassData
	{
		public int Id;
		public string Name;
		public string SpriteMale;
		public string SpriteFemale;
	}
	
	[Serializable]
	public class PlayerHeadData
	{
		public int Id;
		public string Name;
		public string SpriteMale;
		public string SpriteFemale;
	}


	[Serializable]
	public class ClassWeaponData
	{
		public int Id;
		public int ClassId;
		public string Name;
		public string SpriteMale;
		public string SpriteFemale;
	}

	[Serializable]
	public class HeadgearSpriteData
	{
		public int Id;
		public string SpriteName;
	}

    [Serializable]
    public class AdminWarpList
    {
        public string Map;
        public string Title;
        public int X;
        public int Y;
    }

    [Serializable]
    public class AdminWarpSectionList
    {
        public string Title;
        public List<AdminWarpList> WarpList;
    }

	class DatabasePlayerHeadData
	{
		public List<PlayerHeadData> PlayerHeadData;
	}

	class DatabasePlayerClassData
	{
		public List<PlayerClassData> PlayerClassData;
	}
	class AdminWarpListData
    {
        public List<AdminWarpList> WarpSections;
    }


	//[Serializable]
	//class DatabaseMonsterClassData
	//{
	//	public List<MonsterClassData> MonsterClassData;
	//}
}
