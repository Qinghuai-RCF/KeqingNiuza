using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeqingNiuza.Wish
{
    public class WishSummary
    {

        public List<WishData> WishDataList { get; set; }

        public WishStatistics CharacterStatistics { get; set; }

        public WishStatistics WeaponStatistics { get; set; }

        public WishStatistics PermanentStatistics { get; set; }

        public List<ItemInfo> CharacterInfoList { get; set; }

        public List<ItemInfo> WeaponInfoList { get; set; }


        private static Random Random = new Random();

        public static WishSummary Create(string path)
        {
            WishSummary summary = new WishSummary();
            var list = LocalWishLogLoader.Load(path);
            summary.WishDataList = list;
            var groups = list.Distinct().GroupBy(x => x.WishType);
            foreach (var group in groups)
            {
                var sublist = group.OrderBy(x => x.Id);
                switch (group.Key)
                {
                    case WishType.Permanent:
                        summary.PermanentStatistics = GetStatistics(sublist.ToList());
                        break;
                    case WishType.CharacterEvent:
                        summary.CharacterStatistics = GetStatistics(sublist.ToList());
                        break;
                    case WishType.WeaponEvent:
                        summary.WeaponStatistics = GetStatistics(sublist.ToList());
                        break;
                }
            }
            summary.CharacterInfoList = GetCharacterInfoList(list);
            summary.WeaponInfoList = GetWeaponInfoList(list);
            return summary;
        }

        public static WishSummary Create(List<WishData> list)
        {
            WishSummary summary = new WishSummary
            {
                WishDataList = list
            };
            var groups = list.Distinct().GroupBy(x => x.WishType);
            foreach (var group in groups)
            {
                var sublist = group.OrderBy(x => x.Id);
                switch (group.Key)
                {
                    case WishType.Permanent:
                        summary.PermanentStatistics = GetStatistics(sublist.ToList());
                        break;
                    case WishType.CharacterEvent:
                        summary.CharacterStatistics = GetStatistics(sublist.ToList());
                        break;
                    case WishType.WeaponEvent:
                        summary.WeaponStatistics = GetStatistics(sublist.ToList());
                        break;
                }
            }
            summary.CharacterInfoList = GetCharacterInfoList(list);
            summary.WeaponInfoList = GetWeaponInfoList(list);
            return summary;
        }


        private static WishStatistics GetStatistics(List<WishData> list)
        {
            if (list.Count == 0)
            {
                return null;
            }

            WishStatistics ws = new WishStatistics
            {
                WishType = list[0].WishType,
                StartTime = list.Min(x => x.Time),
                EndTime = list.Max(x => x.Time),
                Count = list.Count
            };
            if (list.Last().Rank == 5)
            {
                ws.Guarantee = 0;
            }
            else
            {
                ws.Guarantee = list.Last().Guarantee;
            }
            ws.GuaranteeType = list.Last().GuaranteeType;
            ws.Count_XiaoBaoDi = list.Count(x => (x.GuaranteeType == "小保底" || x.GuaranteeType == "保底内") && x.Rank == 5);
            ws.Count_DaBaoDi = list.Count(x => x.GuaranteeType == "大保底" && x.Rank == 5);
            ws.Star5List = GetDetailList(list, 5);
            DefineColor(ws.Star5List);
            ws.Star5List.Reverse();
            ws.Star5Count = ws.Star5List.Count;
            ws.Star4List = GetDetailList(list, 4);
            DefineColor(ws.Star4List);
            ws.Star4List.Reverse();
            ws.Star4Count = ws.Star4List.Count;
            ws.Character5Count = list.Where(x => x.Rank == 5 && x.ItemType == "角色").Count();
            ws.Weapon5Count = list.Where(x => x.Rank == 5 && x.ItemType == "武器").Count();
            ws.Character4Count = list.Where(x => x.Rank == 4 && x.ItemType == "角色").Count();
            ws.Weapon4Count = list.Where(x => x.Rank == 4 && x.ItemType == "武器").Count();
            ws.Star3Count = list.Where(x => x.Rank == 3).Count();
            ws.Weapon3Count = ws.Star3Count;
            var star5list = list.Where(x => x.Rank == 5);
            if (ws.Star5List.Any())
            {
                ws.GuaranteeMax = ws.Star5List.Max(x => x.Num);
                ws.GuaranteeMaxItems = ws.Star5List.Where(x => x.Num == ws.GuaranteeMax).ToList();
                ws.GuaranteeMin = ws.Star5List.Min(x => x.Num);
                ws.GuaranteeMinItems = ws.Star5List.Where(x => x.Num == ws.GuaranteeMin).ToList();
            }
            else
            {
                ws.GuaranteeMax = ws.Count;
                ws.GuaranteeMin = ws.Count;
            }
            return ws;
        }


        private static void DefineColor(List<StarDetail> list)
        {
            var brushList = Const.BrushList.OrderBy(x => Random.Next()).ToList();
            int brushIndex = 0;
            var groups = list.GroupBy(x => x.Name);
            foreach (var group in groups)
            {
                if (brushIndex >= brushList.Count)
                {
                    brushIndex = 0;
                    brushList = Const.BrushList.OrderBy(x => Random.Next()).ToList();
                }
                group.ToList().ForEach(x => x.Brush = brushList[brushIndex]);
                brushIndex++;
            }
        }



        private static List<ItemInfo> GetCharacterInfoList(List<WishData> list)
        {
            var groups = Const.CharacterInfoList.GroupJoin(list, x => x.Name, y => y.Name, (x, y) =>
            {
                x.WishDataList = y.Where(y1 => x.Name == y1.Name).OrderBy(y1 => y1.Id).ToList();
                x.Count = x.WishDataList.Count;
                //todo 可能有问题
                x.LastGetTime = x.WishDataList.LastOrDefault()?.Time ?? new DateTime();
                return x;
            });
            return groups.Where(x => x.Count > 0).Select(x => x as ItemInfo).ToList();
        }

        private static List<ItemInfo> GetWeaponInfoList(List<WishData> list)
        {
            var groups = Const.WeaponInfoList.GroupJoin(list, x => x.Name, y => y.Name, (x, y) =>
            {
                x.WishDataList = y.Where(y1 => x.Name == y1.Name).OrderBy(y1 => y1.Id).ToList();
                x.Count = x.WishDataList.Count;
                //todo 可能有问题
                x.LastGetTime = x.WishDataList.LastOrDefault()?.Time ?? new DateTime();
                return x;
            });
            return groups.Where(x => x.Count > 0).Select(x => x as ItemInfo).ToList();
        }

        private static List<StarDetail> GetDetailList(List<WishData> datas, int star)
        {
            List<StarDetail> result;
            var list = datas.Where(x => x.Rank == star).Select(x => new StarDetail(x.Name, datas.IndexOf(x), x.Time.ToString("yyyy/MM/dd hh:mm:ss"))).ToList();
            if (list.Any())
            {
                result = new List<StarDetail>(list.Count);
                for (int i = 1; i < list.Count; i++)
                {
                    result.Add(new StarDetail(list[i].Name, list[i].Num - list[i - 1].Num, list[i].Time));
                }
                result = result.Prepend(new StarDetail(list[0].Name, list[0].Num + 1, list[0].Time)).ToList();
            }
            else
            {
                result = new List<StarDetail>();
            }
            return result;
        }

    }
}
