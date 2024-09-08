﻿using FF9;
using Memoria.Assets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SFXChannel
{
    SFXDataMesh.JSON sfx;
    Int32 frame;
    Boolean isReflect = false;

    public static void PlayAnyEffect(SFXDataMesh.JSON sfx, BTL_DATA origin, BTL_DATA target, Vector3 averagePos, Int32 delay)
    {
        SFXChannel sfxModel = new SFXChannel();
        sfxModel.sfx = sfx.CopyWeak();
        sfxModel.frame = -delay;
        sfxModel.sfx.SetupPositions(origin, target, averagePos);
        sfxModel.sfx.Begin();
        SFXChannel.CurrentPlayOthers.Add(sfxModel);
    }

    public static void PlayReflectEffect(UInt16 reflecting, Int32 delay)
    {
        foreach (BTL_DATA reflBtl in btl_util.findAllBtlData(reflecting))
        {
            SFXChannel reflect = new SFXChannel();
            reflect.sfx = ModelReflect.CopyWeak();
            reflect.frame = -delay;
            reflect.isReflect = true;
            reflect.sfx.SetupPositions(reflBtl, null, default(Vector3));
            reflect.sfx.Begin();
            SFXChannel.CurrentPlayOthers.Add(reflect);
            delay += 6;
        }
    }

    public static void Play(String chanType, BTL_DATA channeler, Boolean playSound)
    {
        if (SFXChannel.CurrentPlayChannel.ContainsKey(channeler))
            SFXChannel.Stop(channeler, true);
        Int32[] sounds;
        if (playSound && SFXChannel.CHANNEL_TYPE.TryGetValue(chanType, out sounds))
            foreach (Int32 snd in sounds)
                SoundLib.PlaySoundEffect(snd);
        SFXDataMesh.JSON dataMesh;
        if (!SFXChannel.ModelList.TryGetValue(chanType, out dataMesh))
            dataMesh = LoadSingle(chanType);
        if (dataMesh != null)
        {
            SFXChannel channel = new SFXChannel();
            channel.sfx = dataMesh.CopyWeak();
            channel.frame = 0;
            channel.sfx.SetupPositions(channeler, null, default(Vector3));
            channel.sfx.Begin();
            SFXChannel.CurrentPlayChannel[channeler] = channel;
        }
    }

    public static void Stop(BTL_DATA channeler, Boolean stopImmediatly = false)
    {
        SFXChannel channel;
        if (!SFXChannel.CurrentPlayChannel.TryGetValue(channeler, out channel))
            return;
        if (stopImmediatly)
        {
            channel.sfx.End();
            SFXChannel.CurrentPlayChannel.Remove(channeler);
        }
        else
        {
            channel.sfx.emit = false;
        }
    }

    public static void Render()
    {
        HashSet<BTL_DATA> clearList = new HashSet<BTL_DATA>();
        foreach (KeyValuePair<BTL_DATA, SFXChannel> p in SFXChannel.CurrentPlayChannel)
        {
            if (p.Value.sfx.Render(p.Value.frame))
            {
                clearList.Add(p.Key);
                p.Value.sfx.End();
            }
        }
        foreach (BTL_DATA btl in clearList)
            SFXChannel.CurrentPlayChannel.Remove(btl);
        for (Int32 i = 0; i < SFXChannel.CurrentPlayOthers.Count; i++)
        {
            if (SFXChannel.CurrentPlayOthers[i].frame >= 0 && SFXChannel.CurrentPlayOthers[i].sfx.Render(SFXChannel.CurrentPlayOthers[i].frame))
            {
                SFXChannel.CurrentPlayOthers[i].sfx.End();
                SFXChannel.CurrentPlayOthers.RemoveAt(i--);
            }
        }
    }

    public static void ExecuteLoop()
    {
        foreach (SFXChannel channel in SFXChannel.CurrentPlayChannel.Values)
            channel.frame++;
        foreach (SFXChannel sfxModel in SFXChannel.CurrentPlayOthers)
        {
            if (sfxModel.isReflect && sfxModel.frame == 0)
                SoundLib.PlaySoundEffect(SFX.REFLECT_SOUND_ID, 1f, 0f, 1f);
            sfxModel.frame++;
        }
    }

    public static void EndBattle()
    {
        foreach (SFXChannel channel in SFXChannel.CurrentPlayChannel.Values)
            channel.sfx.End();
        foreach (SFXChannel reflect in SFXChannel.CurrentPlayOthers)
            reflect.sfx.End();
        SFXChannel.CurrentPlayChannel.Clear();
        SFXChannel.CurrentPlayOthers.Clear();
    }

    public static SFXDataMesh.JSON LoadSingle(String chantype)
    {
        String modelPathBase = DataResources.PureDataDirectory + "SpecialEffects/Common/Channel";
        SFXDataMesh.ModelSequence modelJSON = SFXDataMesh.ModelSequence.Load(modelPathBase + chantype + UnifiedBattleSequencer.EXTENSION_SFXMESH_MODEL);
        if (modelJSON != null)
        {
            SFXDataMesh.JSON dataMesh = new SFXDataMesh.JSON();
            dataMesh.model.Add(modelJSON);
            ModelList[chantype] = dataMesh;
            return dataMesh;
        }
        return null;
    }

    public static void LoadAll()
    {
        String modelPathBase = DataResources.PureDataDirectory + "SpecialEffects/Common/Channel";
        foreach (String chantype in CHANNEL_TYPE.Keys)
        {
            SFXDataMesh.JSON dataMesh = new SFXDataMesh.JSON();
            SFXDataMesh.ModelSequence modelJSON = SFXDataMesh.ModelSequence.Load(modelPathBase + chantype + UnifiedBattleSequencer.EXTENSION_SFXMESH_MODEL);
            if (modelJSON != null)
                dataMesh.model.Add(modelJSON);
            ModelList[chantype] = dataMesh;
        }
        ModelReflect = new SFXDataMesh.JSON();
        SFXDataMesh.ModelSequence reflectJSON = SFXDataMesh.ModelSequence.Load(DataResources.PureDataDirectory + "SpecialEffects/Common/Reflect" + UnifiedBattleSequencer.EXTENSION_SFXMESH_MODEL);
        if (reflectJSON != null)
            ModelReflect.model.Add(reflectJSON);
    }

    public static SFXDataMesh.JSON ModelReflect = null;
    public static Dictionary<String, SFXDataMesh.JSON> ModelList = new Dictionary<String, SFXDataMesh.JSON>();
    public static Dictionary<BTL_DATA, SFXChannel> CurrentPlayChannel = new Dictionary<BTL_DATA, SFXChannel>();
    public static List<SFXChannel> CurrentPlayOthers = new List<SFXChannel>();

    public static Dictionary<String, Int32[]> CHANNEL_TYPE = new Dictionary<String, Int32[]> {
        { "Spell", new Int32[]{ 1504, 1505, 1506 } },
        { "Black", new Int32[]{ 1504, 1505, 1506 } },
        { "Summon", new Int32[]{ 1509, 1502, 1503 } },
        { "Enemy", new Int32[]{ 1504, 1505, 1506 } }
    };
}
