using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Memoria;
using Memoria.Prime;
using Memoria.Assets;
using Object = System.Object;

public static class AssetManager
{
	/*
	New system for Memoria:
	 1) Assets can be read from mod folders (defined in Memoria.Configuration.Mod)
	 - They can be either read from bundle archives or as plain non-archived files
	 - Assets in non-bundle archives (typically "resources.assets", but also all the "levelX" and "sharedassetsX.assets") can currently only be read as plain files when they are in a mod's folder
	 - They are read in priority from the first mod's folder up to the last mod's folder, then from the default folder
	 - Bundle archives should be placed directly in the mod's folder
	 - Plain files should be placed in a subfolder of the mod's folder respecting the asset's access path (eg. "EmbeddedAsset/Manifest/Text/Localization.txt")
	 - Only assets of certain types can be read as plain files currently: binary (TextAsset), text (TextAsset), textures (Texture / Texture2D / Sprite / UIAtlas) and animations (AnimationClip)
	 - Texture files that are not in archives should be placed as PNG or JPG files, not DXT-compressed files or using Unity's format
	 - Assets that are not present in the mod folders must be archived as normally in the default folder
	 2) Any asset that is not archived can use a "Memoria information" file which is a text file placed in the same subfolder as the asset, with the same name and the extension "AssetManager.MemoriaInfoExtension" (".memnfo"); the purpose of these files may vary from type to type
	 - For textures, they can define different properties of the texture that are usually in the Unity-formatted assets, such as the anisotropic level (see "AssetManager.ApplyTextureGenericMemoriaInfo")
	 - For sounds and musics, they can define or change different properties, such as the LoopStart/LoopEnd frame points (see "SoundLoaderResources.Load")
	 - For battle scene binary files (".raw16"), they can give battle properties as textual informations, which can be useful for adding custom properties to Memoria or go beyond binary max limits (see "BTL_SCENE.ReadBattleScene")
	 - Etc... [To be completed]
	 - The idea behind these files is that they should be optional (non-modded assets don't need them) and allow to use asset-specific special features without forcing other mods to take these features into account (for explicitely disabling them for instance)
	*/

	static AssetManager()
	{
		Array moduleList = Enum.GetValues(typeof(AssetManagerUtil.ModuleBundle));
		String[] foldname = new String[Configuration.Mod.FolderNames.Length + 1];
		String path;
		for (Int32 i = 0; i < Configuration.Mod.FolderNames.Length; ++i)
			foldname[i] = Configuration.Mod.FolderNames[i] + "/";
		foldname[Configuration.Mod.FolderNames.Length] = "";
		Folder = new AssetFolder[foldname.Length];
		for (Int32 i = 0; i < foldname.Length; ++i)
		{
			Folder[i] = new AssetFolder(foldname[i]);
			foreach (Object module in moduleList)
			{
				AssetManagerUtil.ModuleBundle moduleBundle = (AssetManagerUtil.ModuleBundle)module;
				if (moduleBundle == AssetManagerUtil.ModuleBundle.FieldMaps)
				{
					Array fieldModuleList = Enum.GetValues(typeof(AssetManagerUtil.FieldMapBundleId));
					foreach (Object fieldModule in fieldModuleList)
					{
						AssetManagerUtil.FieldMapBundleId bundleId = (AssetManagerUtil.FieldMapBundleId)fieldModule;
						path = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateFieldMapBundleFilename(Application.platform, false, bundleId);
						if (File.Exists(path))
							Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetFieldMapBundleName(bundleId), new AssetBundleRef(path, 0));
					}
				}
				else if (moduleBundle == AssetManagerUtil.ModuleBundle.Sounds)
				{
					Array soundModuleList = Enum.GetValues(typeof(AssetManagerUtil.SoundBundleId));
					foreach (Object soundModule in soundModuleList)
					{
						AssetManagerUtil.SoundBundleId bundleId = (AssetManagerUtil.SoundBundleId)soundModule;
						path = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateSoundBundleFilename(Application.platform, false, bundleId);
						if (File.Exists(path))
							Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetSoundBundleName(bundleId), new AssetBundleRef(path, 0));
					}
				}
				else
				{
					path = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateModuleBundleFilename(Application.platform, false, moduleBundle);
					if (File.Exists(path))
						Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetModuleBundleName(moduleBundle), new AssetBundleRef(path, 0));
				}
			}
		}
		_LoadAnimationFolderMapping();
	}

	private static void _LoadAnimationFolderMapping()
	{
		AnimationInFolder = new Dictionary<String, List<String>>();
		AnimationReverseFolder = new Dictionary<String, String>();
		String filestr = LoadString("EmbeddedAsset/Manifest/Animations/AnimationFolderMapping.txt");
		if (filestr == null)
			return;
		String[] folderList = filestr.Split('\n');
		for (Int32 i = 0; i < folderList.Length; i++)
		{
			String[] geoAndAnim = folderList[i].Split(':');
			String geoFolder = geoAndAnim[0];
			String modelName = Path.GetFileNameWithoutExtension(geoFolder);
			String[] animList = geoAndAnim[1].Split(',');
			List<String> animFolderList = new List<String>();
			for (Int32 j = 0; j < animList.Length; j++)
			{
				String animName = animList[j].Trim();
				String animPath = geoFolder + "/" + animName;
				animPath = animPath.Trim();
				animFolderList.Add(animPath);
				if (FF9BattleDB.GEO.ContainsValue(modelName))
					AnimationReverseFolder[animName] = modelName;
			}
			AnimationInFolder.Add(geoFolder, animFolderList);
		}
	}

	public static void UpdateAutoAnimMapping(String modelName, String[] addedAnimList)
	{
		List<String> modelAnimList;
		String animDir = "Animations/" + modelName;
		String animPath, animModelName;
		if (!AssetManager.AnimationInFolder.TryGetValue(animDir, out modelAnimList))
			modelAnimList = new List<String>();
		for (Int32 i = 0; i < addedAnimList.Length; ++i)
		{
			if (!AssetManager.AnimationReverseFolder.TryGetValue(addedAnimList[i], out animModelName)) // Animation registered in "AnimationFolderMapping.txt": use name ID of already registered model
				animModelName = modelName; // Custom animation: use name ID of the specified model
			animPath = "Animations/" + animModelName + "/" + addedAnimList[i];
			if (!modelAnimList.Contains(animPath))
			{
				modelAnimList.Add(animPath);
				AssetManager.AnimationReverseFolder[addedAnimList[i]] = animModelName;
			}
		}
		AssetManager.AnimationInFolder[animDir] = modelAnimList;
	}

	public static void ClearCache()
	{
		Caching.CleanCache();
		foreach (AssetFolder modfold in Folder)
			foreach (KeyValuePair<String, AssetBundleRef> keyValuePair in modfold.DictAssetBundleRefs)
			{
				AssetBundleRef value = keyValuePair.Value;
				if (value.assetBundle != null)
				{
					value.assetBundle.Unload(true);
					value.assetBundle = null;
				}
			}
	}

	public static void ApplyTextureGenericMemoriaInfo<T>(ref T texture, ref String[] memoriaInfo) where T : UnityEngine.Texture
	{
		// Dummied
		// Maybe remove the successfully parsed lines from the "info" array?
		foreach (String s in memoriaInfo)
        {
			String[] textureCode = s.Split(' ');
			if (textureCode.Length >= 2 && String.Compare(textureCode[0], "AnisotropicLevel") == 0)
			{
				Int32 anisoLevel;
				if (Int32.TryParse(textureCode[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out anisoLevel) && anisoLevel >= 1 && anisoLevel <= 9)
					texture.anisoLevel = anisoLevel;
			} else if (textureCode.Length >= 2 && String.Compare(textureCode[0], "FilterMode") == 0)
			{
				foreach (FilterMode m in (FilterMode[]) Enum.GetValues(typeof(FilterMode)))
					if (String.Compare(textureCode[1], m.ToString()) == 0)
						texture.filterMode = m;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "HideFlags") == 0)
			{
				foreach (HideFlags f in (HideFlags[]) Enum.GetValues(typeof(HideFlags)))
					if (String.Compare(textureCode[1], f.ToString()) == 0)
						texture.hideFlags = f;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "MipMapBias") == 0)
			{
				Single mipMapBias;
				if (Single.TryParse(textureCode[1], out mipMapBias))
					texture.mipMapBias = mipMapBias;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "WrapMode") == 0)
			{
				foreach (TextureWrapMode m in (TextureWrapMode[]) Enum.GetValues(typeof(TextureWrapMode)))
					if (String.Compare(textureCode[1], m.ToString()) == 0)
						texture.wrapMode = m;
			}
		}
	}

	public static Boolean IsTextureFormatReadableFromBytes(TextureFormat format)
	{
		// Todo: verify which TextureFormat are compatible with LoadRawTextureData
		return format == TextureFormat.Alpha8
			|| format == TextureFormat.RGB24
			|| format == TextureFormat.RGBA32
			|| format == TextureFormat.ARGB32
			|| format == TextureFormat.RGB565
			|| format == TextureFormat.DXT1
			|| format == TextureFormat.DXT5;
	}

	public static Texture2D LoadTextureGeneric(Byte[] raw)
	{
		const UInt32 DDS_HEADER_SIZE = 0x3C;
		if (raw.Length >= DDS_HEADER_SIZE)
		{
			UInt32 w = BitConverter.ToUInt32(raw, 0);
			UInt32 h = BitConverter.ToUInt32(raw, 4);
			UInt32 imgSize = BitConverter.ToUInt32(raw, 8);
			TextureFormat fmt = (TextureFormat)BitConverter.ToUInt32(raw, 12);
			UInt32 mipCount = BitConverter.ToUInt32(raw, 16);
			// UInt32 flags = BitConverter.ToUInt32(raw, 20); // 1 = isReadable (for use of GetPixels); 0x100 is usually on
			// UInt32 imageCount = BitConverter.ToUInt32(raw, 24);
			// UInt32 dimension = BitConverter.ToUInt32(raw, 28);
			// UInt32 filterMode = BitConverter.ToUInt32(raw, 32);
			// UInt32 anisotropic = BitConverter.ToUInt32(raw, 36);
			// UInt32 mipBias = BitConverter.ToUInt32(raw, 40);
			// UInt32 wrapMode = BitConverter.ToUInt32(raw, 44);
			// UInt32 lightmapFormat = BitConverter.ToUInt32(raw, 48);
			// UInt32 colorSpace = BitConverter.ToUInt32(raw, 52);
			// UInt32 imgSize = BitConverter.ToUInt32(raw, 56);
			if (raw.Length == DDS_HEADER_SIZE + imgSize && IsTextureFormatReadableFromBytes(fmt))
			{
				Byte[] imgBytes = new Byte[imgSize];
				Buffer.BlockCopy(raw, (Int32)DDS_HEADER_SIZE, imgBytes, 0, (Int32)imgSize);
				Texture2D ddsTexture = new Texture2D((Int32)w, (Int32)h, fmt, mipCount > 1);
				ddsTexture.LoadRawTextureData(imgBytes);
				ddsTexture.Apply();
				return ddsTexture;
			}
		}
		Texture2D pngTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
		if (pngTexture.LoadImage(raw))
			return pngTexture;
		return null;
	}

	public static T LoadFromDisc<T>(String name, String archiveName)
	{
		/*
		Types used by the game by default:
		 * TextAsset (many, both text and binaries, including sounds and musics) - Use LoadString / LoadBytes instead
		 * Texture2D (LoadAsync is used in MBG) - Can be read as PNG/JPG
		 * Texture (only used by ModelFactory for "GEO_MAIN_F3_ZDN", "GEO_MAIN_F4_ZDN" and "GEO_MAIN_F5_ZDN") - Can be read as PNG/JPG as Texture2D
		 * RenderTexture (Usually split into many pieces) - Can't be read from disc currently
		 * Material - Can't be read from disc currently
		 * AnimationClip (LoadAll is used in AnimationFactory) - Can be read as .anim (serialized format, as in the p0data5.bin) or JSON but with simple readers
		 * GameObject (Usually split into many pieces ; LoadAsync is used in WMWorld) - Can't be read from disc currently
		*/
		if (typeof(T) == typeof(String))
		{
			return (T)(Object)File.ReadAllText(name);
		}
		else if (typeof(T) == typeof(Byte[]))
		{
			return (T)(Object)File.ReadAllBytes(name);
		}
		else if (typeof(T) == typeof(Texture2D) || typeof(T) == typeof(Texture))
		{
			Byte[] raw = File.ReadAllBytes(name);
			Texture2D newTexture = LoadTextureGeneric(raw);
			if (newTexture == null)
				newTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
			//ApplyTextureGenericMemoriaInfo<Texture2D>(ref newTexture, ref memoriaInfo);
			return (T)(Object)newTexture;
		}
		else if (typeof(T) == typeof(Sprite))
		{
			Byte[] raw = File.ReadAllBytes(name);
			Texture2D newTexture = LoadTextureGeneric(raw);
			if (newTexture == null)
				newTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
			//ApplyTextureGenericMemoriaInfo<Texture2D>(ref newTexture, ref memoriaInfo);
			Sprite newSprite = Sprite.Create(newTexture, new Rect(0, 0, newTexture.width, newTexture.height), new Vector2(0.5f, 0.5f));
			return (T)(Object)newSprite;
		}
		else if (typeof(T) == typeof(UIAtlas))
		{
			// Todo: Maybe avoid the call of Resources.Load<UIAtlas> if a complete .tpsheet is available and it's not necessary to get the original's sprite list
			UIAtlas newAtlas = Resources.Load<UIAtlas>(archiveName);
			if (newAtlas != null)
				newAtlas.ReadFromDisc(name);
			else
				Log.Error("[AssetManager] When loading an UIAtlas from disc, the base asset is required! This base asset cannot be found: " + archiveName);
			return (T)(Object)newAtlas;
		}
		else if (typeof(T) == typeof(AnimationClip))
		{
			return (T)(Object)AnimationClipReader.ReadAnimationClipFromDisc(name);
		}
		Log.Error("[AssetManager] Trying to load from disc the asset " + name + " of type " + typeof(T).ToString() + ", which is not currently possible");
		return default(T);
	}

	public static IEnumerable<T> LoadMultiple<T>(String name) where T : UnityEngine.Object
	{
		T result;
		if (AssetManagerForObb.IsUseOBB)
		{
			yield return AssetManagerForObb.Load<T>(name);
			yield break;
		}
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
					yield return LoadFromDisc<T>(modfold.FolderPath + name, name);
			if (File.Exists(name))
				yield return LoadFromDisc<T>(name, name);
			yield break;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
					yield return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
			result = Resources.Load<T>(name);
			if (result != null)
				yield return result;
			yield break;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<T>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
					yield return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, nameInBundle);
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != null)
				{
					result = assetBundleRef.assetBundle.LoadAsset<T>(nameInBundle);
					if (result != null)
						yield return result;
				}
			}
		}
		if (ForceUseBundles)
			yield break;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				yield return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
		result = Resources.Load<T>(name);
		if (result != null)
			yield return result;
		yield break;
	}
	
	public static IEnumerable<String> LoadStringMultiple(String name)
	{
		TextAsset txt;
		if (AssetManagerForObb.IsUseOBB)
		{
			txt = AssetManagerForObb.Load<TextAsset>(name);
			if (txt != null)
				yield return txt.text;
			yield break;
		}
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
					yield return LoadFromDisc<String>(modfold.FolderPath + name, name);
			if (File.Exists(name))
				yield return LoadFromDisc<String>(name, name);
			yield break;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
					yield return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
			txt = Resources.Load<TextAsset>(name);
			if (txt != null)
				yield return txt.text;
			yield break;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<TextAsset>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
					yield return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, nameInBundle);
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != null)
				{
					txt = assetBundleRef.assetBundle.LoadAsset<TextAsset>(nameInBundle);
					if (txt != null)
						yield return txt.text;
				}
			}
		}
		if (ForceUseBundles)
			yield break;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				yield return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
		txt = Resources.Load<TextAsset>(name);
		if (txt != null)
			yield return txt.text;
		yield break;
	}
	
	public static IEnumerable<Byte[]> LoadBytesMultiple(String name)
	{
		TextAsset txt;
		if (AssetManagerForObb.IsUseOBB)
		{
			txt = AssetManagerForObb.Load<TextAsset>(name);
			if (txt != null)
				yield return txt.bytes;
			yield break;
		}
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
					yield return LoadFromDisc<Byte[]>(modfold.FolderPath + name, name);
			if (File.Exists(name))
				yield return LoadFromDisc<Byte[]>(name, name);
			yield break;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
					yield return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
			txt = Resources.Load<TextAsset>(name);
			if (txt != null)
				yield return txt.bytes;
			yield break;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<TextAsset>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
					yield return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, nameInBundle);
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != null)
				{
					txt = assetBundleRef.assetBundle.LoadAsset<TextAsset>(nameInBundle);
					if (txt != null)
						yield return txt.bytes;
				}
			}
		}
		if (ForceUseBundles)
			yield break;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				yield return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, name);
		txt = Resources.Load<TextAsset>(name);
		if (txt != null)
			yield return txt.bytes;
		yield break;
	}

	public static T Load<T>(String name, Boolean suppressMissingError = false) where T : UnityEngine.Object
	{
		T mainFile = LoadMultiple<T>(name).FirstOrDefault();
		if (mainFile == null && !suppressMissingError)
			Log.Message("[AssetManager] Memoria asset not found: " + name);
		return mainFile;
	}

	public static String LoadString(String name, Boolean suppressMissingError = false)
	{
		String mainFile = LoadStringMultiple(name).FirstOrDefault();
		if (mainFile == null && !suppressMissingError)
			Log.Message("[AssetManager] Memoria asset not found: " + name);
		return mainFile;
	}

	public static Byte[] LoadBytes(String name, Boolean suppressMissingError = false)
	{
		Byte[] mainFile = LoadBytesMultiple(name).FirstOrDefault();
		if (mainFile == null && !suppressMissingError)
			Log.Message("[AssetManager] Memoria asset not found: " + name);
		return mainFile;
	}

	public static AssetManagerRequest LoadAsync<T>(String name) where T : UnityEngine.Object
	{
		if (AssetManagerForObb.IsUseOBB)
			return AssetManagerForObb.LoadAsync<T>(name);
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			ResourceRequest resourceRequest = Resources.LoadAsync<T>(name);
			if (resourceRequest != null)
				return new AssetManagerRequest(resourceRequest, null);
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<T>(name);
			foreach (AssetFolder modfold in Folder)
			{
				AssetBundleRef assetBundleRef;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != null)
				{
					AssetBundleRequest assetBundleRequest = assetBundleRef.assetBundle.LoadAssetAsync(nameInBundle);
					if (assetBundleRequest != null)
						return new AssetManagerRequest(null, assetBundleRequest);
				}
			}
		}
		if (ForceUseBundles)
			return null;
		ResourceRequest resourceRequest2 = Resources.LoadAsync<T>(name);
		if (resourceRequest2 != null)
			return new AssetManagerRequest(resourceRequest2, null);
		Log.Message("[AssetManager] Asset not found: " + name);
		return null;
	}

	public static Boolean CheckIfAssetExists<T>(String name, Boolean checkDisc = true, Boolean checkBundle = true, Boolean checkResources = true) where T : UnityEngine.Object
	{
		if (checkDisc && AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
					return true;
			if (File.Exists(name))
				return true;
			return false;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<T>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (checkDisc && File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
					return true;
				if (!checkBundle)
					continue;
				AssetBundleRef assetBundleRef;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != null)
					if (assetBundleRef.assetBundle.Contains(nameInBundle))
						return true;
			}
		}
		if (checkDisc)
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
					return true;
		return checkResources ? Resources.Load<T>(name) != null : false;
	}

	public static String SearchAssetOnDisc(String name, Boolean includeAssetPath, Boolean includeAssetExtension)
	{
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) + "/" : "") + name))
					return modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) + "/" : "") + name;
			return String.Empty;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = (includeAssetPath ? AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() : "") + name + (includeAssetExtension ? AssetManagerUtil.GetAssetExtension<TextAsset>(name) : "");
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + nameInBundle))
					return modfold.FolderPath + nameInBundle;
		}
		if (ForceUseBundles)
			return String.Empty;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) : "") + "/" + name))
				return modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) : "") + "/" + name;
		return String.Empty;
	}

	public static Boolean HasAssetOnDisc(String name, Boolean includeAssetPath, Boolean includeAssetExtension)
	{
		return !String.IsNullOrEmpty(SearchAssetOnDisc(name, includeAssetPath, includeAssetExtension));
	}

	public static T[] LoadAll<T>(String name) where T : UnityEngine.Object
	{
		if (AssetManagerForObb.IsUseOBB)
			return AssetManagerForObb.LoadAll<T>(name);
		if (typeof(T) != typeof(AnimationClip))
			return null;
		if (!UseBundles)
			return Resources.LoadAll<T>(AnimationFactory.GetRenameAnimationDirectory(name));
		if (AnimationInFolder.ContainsKey(name))
		{
			List<String> clipNameList = AnimationInFolder[name];
			T[] clipList = new T[clipNameList.Count];
			for (Int32 i = 0; i < clipNameList.Count; i++)
			{
				String renameAnimationPath = AnimationFactory.GetRenameAnimationPath(clipNameList[i]);
				clipList[i] = Load<T>(renameAnimationPath, false);
				AnimationClip clip = clipList[i] as AnimationClip;
				if (clip != null && String.Compare(clip.name, "CUSTOM_MUST_RENAME") == 0)
					clip.name = Path.GetFileNameWithoutExtension(clipNameList[i]);
			}
			return clipList;
		}
		return null;
	}

	// .memnfo files are not used anymore, except for sound meta-datas
	// Even for these, setting proper meta-datas in the OGG is better
	public const String MemoriaInfoExtension = ".memnfo";

	public const TextureFormat DefaultTextureFormat = TextureFormat.ARGB32;

	public static Boolean UseBundles;

	public static Boolean ForceUseBundles;

	public static AssetFolder[] Folder;

	public static Dictionary<String, List<String>> AnimationInFolder;

	public static Dictionary<String, String> AnimationReverseFolder;

	public class AssetBundleRef
	{
		public AssetBundleRef(String strUrl, Int32 intVersionIn)
		{
			this.fullUrl = strUrl;
			this.version = intVersionIn;
		}

		public AssetBundle assetBundle;

		public Int32 version;

		public String fullUrl;
	}
	
	public class AssetFolder
	{
		public AssetFolder(String path)
		{
			this.FolderPath = path;
			this.DictAssetBundleRefs = new Dictionary<String, AssetBundleRef>();
		}

		public String FolderPath;

		public Dictionary<String, AssetBundleRef> DictAssetBundleRefs;
	}
}
