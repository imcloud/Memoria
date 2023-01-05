﻿using System;
using Memoria.Data;
public class AA_DATA
{
	public AA_DATA()
	{
		this.Info = new BattleCommandInfo();
		this.Ref = new BTL_REF();
		Category = 0;
		AddStatusNo = BattleStatusIndex.None;
		MP = 0;
		Type = 0;
		Vfx2 = 0;
	}

	public AA_DATA(BattleCommandInfo info, BTL_REF btlRef, Byte category, BattleStatusIndex statusSetIndex, Int32 mp, Byte type, UInt16 vfx2)
	{
		Info = info;
		Ref = btlRef;
		Category = category;
		AddStatusNo = statusSetIndex;
		MP = mp;
		Type = type;
		Vfx2 = vfx2;
	}

	public BattleCommandInfo Info;
	public BTL_REF Ref;

	[Memoria.PatchableFieldAttribute]
	public Byte Category;
	[Memoria.PatchableFieldAttribute]
	public BattleStatusIndex AddStatusNo;
	[Memoria.PatchableFieldAttribute]
	public Int32 MP;
	[Memoria.PatchableFieldAttribute]
	public Byte Type;
	[Memoria.PatchableFieldAttribute]
	public UInt16 Vfx2;
	public Byte CastingTitleType;

    // Delayed initialization
    public String Name;
}
