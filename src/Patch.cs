using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SelectEtherDisease
{
    [HarmonyPatch]
    public static class Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chara), nameof(Chara.MutateRandom))]
        public static bool MutateRandom_Prefix(Chara __instance, int vec, int tries, bool ether, BlessedState state, ref bool __result)
        {
            // vec = -1 : エーテル病治療時
            // エーテル病治療時 or エーテル病以外の変異時は何もしない
            if (vec <= 0 || !ether)
                return true;

            // エーテル病リスト候補を取得する
            var candidates = EClass.sources.elements.rows.Where(a => a.category == "ether" && !a.tag.Contains("noRandomMutation"));

            // 候補をフィルタリングしてリストを作成
            List<SourceElement.Row> etherDiseaseList = [];
            foreach (var row in candidates)
            {
                // キャラクターのエーテル病罹患状態を取得
                var element = __instance.elements.GetElement(row.id);
                
                // エーテル病の進行度を取得
                var currentLevel = element?.Value ?? 0;

                // 罹患レベルが最大値に達している場合は除外
                if (currentLevel == row.max)
                    continue;

                etherDiseaseList.Add(row);
            }

            // リストが0件ならなにもしない
            if (etherDiseaseList.Count == 0)
                return true;

            // UI表示
            LayerList layer = EClass.ui.AddLayer<LayerList>();
            layer.SetList(etherDiseaseList,
                (row) =>
                {
                    // エーテル病取得
                    var element = __instance.elements.GetElement(row.id);
                    
                    // エーテル病の進行度を取得
                    var nextValue = (element?.Value ?? 0) + 1;
                    
                    // example : 重力発生\n大きな重力\nとてつもない重力
                    var names = row.GetName();
                    
                    // 改行で分割して対象レベルの名前を表示
                    string[] nameArray = names.Split('\n');
                    
                    return nameArray[nextValue - 1];
                    // var index = Mathf.Clamp(nextValue - 1, 0, parts.Length - 1);
                    // return parts[index];
                },
                (int index, string s) =>
                {
                    var selectedRow = etherDiseaseList[index];
                    ApplyEther(__instance, selectedRow, vec);
                })
                .SetHeader("Select Ether Disease");

            __result = true;
            return false;
        }

        private static void ApplyEther(Chara c, SourceElement.Row row, int vec)
        {
            Element element = c.elements.GetElement(row.id);
            int num = 1;
            if (element != null)
            {
                num = element.Value + vec;
                if (num > element.source.max) num = element.source.max - 1;
            }

            // Messages and Effects
            var flag = row.tag.Contains("neg");
            c.Say("mutation_gain", c);

            c.SetFeat(row.id, num);

            // History
            if (c.c_corruptionHistory == null) c.c_corruptionHistory = new List<int>();
            c.c_corruptionHistory.Add(row.id);

            if (c.IsPCFaction)
            {
                Element element2 = c.elements.GetElement(row.id);
                WidgetPopText.Say("popEther".lang(element2.Name, c.Name));
            }

            if (c.IsPC && !EClass.player.flags.gotEtherDisease)
            {
                EClass.player.flags.gotEtherDisease = true;
                Thing thing = ThingGen.Create("parchment");
                thing.SetStr(53, "letter_ether");
                Thing thing2 = ThingGen.Create("1165");
                thing2.SetBlessedState(BlessedState.Normal);
                Thing p = ThingGen.CreateParcel(null, thing2, thing);
                EClass.world.SendPackage(p);
            }

            if (EClass.core.IsGameStarted && c.pos != null)
            {
                c.PlaySound("mutation_ether");
                c.PlayEffect("mutation");
                Msg.SetColor(flag ? Msg.colors.MutateBad : Msg.colors.MutateGood);
                c.Say(row.GetText(flag ? "textDec" : "textInc", returnNull: true) ?? row.alias, c);
            }
        }
    }
}