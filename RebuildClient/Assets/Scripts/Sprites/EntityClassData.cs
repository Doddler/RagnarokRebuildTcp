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

	class DatabasePlayerHeadData
	{
		public List<PlayerHeadData> PlayerHeadData;
	}

	class DatabasePlayerClassData
	{
		public List<PlayerClassData> PlayerClassData;
	}

	//[Serializable]
	//class DatabaseMonsterClassData
	//{
	//	public List<MonsterClassData> MonsterClassData;
	//}
}
