using System;
using System.Collections.Generic;
using System.Linq;
using CheckmateRPG.Data;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    /// <summary>런칭 정규 소집 풀. 에셋이 아직 없는 오퍼레이터도 동일한 ID로 즉시 소집·저장할 수 있도록 런타임 메타데이터를 제공합니다.</summary>
    public static class RecruitRosterCatalog
    {
        public sealed class Definition
        {
            public readonly string Id, DisplayName;
            public readonly int Stars;
            public readonly ChessPieceType Piece;
            public readonly UnitSubclassType Subclass;
            public Definition(string id,string displayName,int stars,ChessPieceType piece,UnitSubclassType subclass)
            { Id=id; DisplayName=displayName; Stars=stars; Piece=piece; Subclass=subclass; }
        }

        private static readonly Definition[] Definitions =
        {
            new("Kiara","키아라",5,ChessPieceType.Knight,UnitSubclassType.Swordmaster), new("Elena","엘레나",5,ChessPieceType.Bishop,UnitSubclassType.Sniper),
            new("Vesta","베스타",5,ChessPieceType.Rook,UnitSubclassType.SiegeWeapon), new("Atlas","아틀라스",5,ChessPieceType.Rook,UnitSubclassType.Colossus),
            new("Lilith","릴리스",5,ChessPieceType.Queen,UnitSubclassType.Witch), new("Siegfried","지크프리트",5,ChessPieceType.Pawn,UnitSubclassType.Agent),
            new("Minerva","미네르바",5,ChessPieceType.Bishop,UnitSubclassType.Archon), new("Kali","칼리",5,ChessPieceType.Knight,UnitSubclassType.Spellblade),
            new("Gaia","가이아",5,ChessPieceType.Rook,UnitSubclassType.MainCannon), new("Seraphina","세라피나",5,ChessPieceType.Bishop,UnitSubclassType.SanctifiedCaster),
            new("Mirage","환영기사",4,ChessPieceType.Knight,UnitSubclassType.PhantomKnight), new("Terra","지형술사",4,ChessPieceType.Bishop,UnitSubclassType.Geomancer),
            new("Warden","근위대장",4,ChessPieceType.Rook,UnitSubclassType.Defender), new("Pioneer","척후대장",4,ChessPieceType.Pawn,UnitSubclassType.Vanguard),
            new("Sanctuary","전술사제",4,ChessPieceType.Bishop,UnitSubclassType.Priest), new("Flag","플래그",4,ChessPieceType.Pawn,UnitSubclassType.Flagbearer),
            new("Isaac","아이작",4,ChessPieceType.Pawn,UnitSubclassType.Engineer), new("Valkyrie","발키리",4,ChessPieceType.Knight,UnitSubclassType.Lancer),
            new("Bolt","볼트",4,ChessPieceType.Knight,UnitSubclassType.Musketeer), new("Chaser","체이서",4,ChessPieceType.Pawn,UnitSubclassType.Scout),
            new("Lyla","라일라",4,ChessPieceType.Bishop,UnitSubclassType.Warlock), new("Bartholomew","바솔로뮤",4,ChessPieceType.Rook,UnitSubclassType.Executioner),
            new("Gale","게일",4,ChessPieceType.Queen,UnitSubclassType.Princess), new("Necro","네크로",4,ChessPieceType.Bishop,UnitSubclassType.Necromancer),
            new("Leo","레오",3,ChessPieceType.Pawn,UnitSubclassType.Vanguard), new("Baron","바론",3,ChessPieceType.Pawn,UnitSubclassType.Defender),
            new("Chloe","클로에",3,ChessPieceType.Pawn,UnitSubclassType.Charger), new("Robin","로빈",3,ChessPieceType.Pawn,UnitSubclassType.Marksman),
            new("Shadow","섀도우",3,ChessPieceType.Pawn,UnitSubclassType.Spy), new("Kyle","카일",3,ChessPieceType.Knight,UnitSubclassType.AssaultKnight),
            new("Roland","롤랑",3,ChessPieceType.Knight,UnitSubclassType.GuardKnight), new("Lucas","루카스",3,ChessPieceType.Knight,UnitSubclassType.Paladin),
            new("Pegasus","페가수스",3,ChessPieceType.Knight,UnitSubclassType.MobileKnight), new("Selena","셀레나",3,ChessPieceType.Bishop,UnitSubclassType.Archbishop),
            new("Noah","노아",3,ChessPieceType.Bishop,UnitSubclassType.Priest), new("Enoch","에녹",3,ChessPieceType.Bishop,UnitSubclassType.Inquisitor),
            new("Lumi","루미",3,ChessPieceType.Bishop,UnitSubclassType.Fairy), new("Brock","브록",3,ChessPieceType.Rook,UnitSubclassType.Fortress),
            new("Ignis","이그니스",3,ChessPieceType.Rook,UnitSubclassType.MagicTower), new("Teresa","테레사",3,ChessPieceType.Rook,UnitSubclassType.Guardian)
        };

        private static readonly Dictionary<string,UnitData> Units = new(StringComparer.Ordinal);
        private static RecruitBannerData _standard;
        private static readonly Dictionary<string,RecruitBannerData> Pickups = new(StringComparer.Ordinal);
        public static IReadOnlyList<Definition> AllDefinitions => Definitions;

        public static UnitData GetUnit(string id)
        {
            if (Units.TryGetValue(id,out var cached)) return cached;
            var asset=Resources.Load<UnitData>("Units/"+id);
            var def=Definitions.FirstOrDefault(x=>x.Id==id);
            if(asset!=null)
            {
                asset.Rarity=def?.Stars??asset.Rarity; Units[id]=asset; return asset;
            }
            if(def==null)return null;
            var unit=ScriptableObject.CreateInstance<UnitData>(); unit.name=def.Id; unit.UnitName=def.DisplayName; unit.Rarity=def.Stars; unit.PieceType=def.Piece; unit.Subclass=def.Subclass; unit.SyncDefaultChessMetadata();
            unit.BaseRole=def.Subclass.ToString(); unit.MaxHealth=def.Stars==5?700:500; unit.AttackDamage=def.Stars==5?150:100; unit.DeploymentCost=2; unit.Weight=def.Piece==ChessPieceType.Rook||def.Piece==ChessPieceType.Queen?3:2;
            unit.MoveRange=3; unit.AttackRange=def.Piece==ChessPieceType.Bishop?5:1; unit.SupportPower=def.Subclass==UnitSubclassType.Priest||def.Subclass==UnitSubclassType.SanctifiedCaster?1.25f:1f;
            unit.hideFlags=HideFlags.HideAndDontSave; Units[id]=unit; return unit;
        }

        public static RecruitBannerData GetStandardBanner()
        {
            if(_standard!=null)return _standard;
            _standard=ScriptableObject.CreateInstance<RecruitBannerData>(); _standard.name="Standard_40_Roster"; _standard.BannerId="standard-40"; _standard.IsPickup=false;
            _standard.Entries=Definitions.Select(x=>new RecruitEntry{Unit=GetUnit(x.Id),Stars=x.Stars}).ToArray(); _standard.hideFlags=HideFlags.HideAndDontSave; return _standard;
        }

        public static RecruitBannerData GetPickupBanner(string id="Kiara")
        {
            if(Pickups.TryGetValue(id,out var existing))return existing;
            var pickup=GetUnit(id); if(pickup==null)return null;
            var banner=ScriptableObject.CreateInstance<RecruitBannerData>(); banner.name="Pickup_"+id; banner.BannerId="pickup-"+id.ToLowerInvariant(); banner.IsPickup=true; banner.PickupUnit=pickup;
            var four=Definitions.Where(x=>x.Stars==4).Take(2).Select(x=>new RecruitEntry{Unit=GetUnit(x.Id),Stars=4});
            var three=Definitions.Where(x=>x.Stars==3).Take(4).Select(x=>new RecruitEntry{Unit=GetUnit(x.Id),Stars=3});
            banner.Entries=Definitions.Where(x=>x.Stars==5).Select(x=>new RecruitEntry{Unit=GetUnit(x.Id),Stars=5}).Concat(four).Concat(three).ToArray(); banner.hideFlags=HideFlags.HideAndDontSave; Pickups[id]=banner; return banner;
        }

        public static IEnumerable<UnitData> GetAllUnits()
        { foreach(var def in Definitions) yield return GetUnit(def.Id); }
    }
}
