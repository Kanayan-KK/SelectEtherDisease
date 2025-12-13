using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SelectEtherDisease
{
    [HarmonyPatch]
    public static class Patch
    {
        // キュー実行用リクエストデータ格納用クラス
        public class MutationRequest
        {
            public Chara? chara;
            public int vec;
        }

        // キュー作成
        public static Queue<MutationRequest> queue = new();

        // リスト選択中フラグ
        public static bool isSelecting;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chara), nameof(Chara.MutateRandom))]
        public static bool MutateRandom_Prefix(Chara __instance, int vec, int tries, bool ether, BlessedState state,
            ref bool __result)
        {
            // vec = -1 : エーテル病治療時
            // vec = 1 : エーテル病感染時
            if (vec <= 0 || !ether)
                // エーテル病治療時 or エーテル病以外の変異時（自己変容）は何もしない
                return true;

            // キューを登録
            queue.Enqueue(new MutationRequest { chara = __instance, vec = vec });

            // 処理結果はtrue(成功)として返すが、実際の処理はキューで遅延させる
            __result = true;

            // キューを実行
            ProcessQueue();

            // 本来の処理はスキップする
            return false;
        }

        // キュー実行処理
        public static void ProcessQueue()
        {
            // リスト選択中 or 待機キュー無し
            if (isSelecting || queue.Count == 0)
                // 処理終了
                return;

            // リクエストデータ取得
            var req = queue.Peek();

            // エーテル病リスト候補を取得する
            var candidates =
                EClass.sources.elements.rows.Where(a => a.category == "ether" && !a.tag.Contains("noRandomMutation"));

            // 罹患可能エーテル病リストを作成
            List<SourceElement.Row> etherDiseaseList = [];
            foreach (var row in candidates)
            {
                // キャラクターのエーテル病罹患状態を取得
                var element = req.chara?.elements.GetElement(row.id);

                // エーテル病の進行度を取得
                var currentLevel = element?.Value ?? 0;

                // 罹患レベルが最大値に達している場合は除外
                if (currentLevel == row.max)
                    continue;

                etherDiseaseList.Add(row);
            }

            // リストが0件ならキューから削除して次へ
            if (etherDiseaseList.Count == 0)
            {
                queue.Dequeue();
                ProcessQueue();
                return;
            }

            // 選択中フラグ更新
            isSelecting = true;

            // UI表示
            var layer = EClass.ui.AddLayer<LayerList>();

            layer.SetList(etherDiseaseList,
                    (row) =>
                    {
                        // エーテル病取得
                        var element = req.chara?.elements.GetElement(row.id);

                        // エーテル病の進行度を取得
                        var nextValue = (element?.Value ?? 0) + 1;

                        // example : 重力発生\n大きな重力\nとてつもない重力
                        var names = row.GetName();

                        // 改行で分割して対象レベルの名前を表示
                        string[] nameArray = names.Split('\n');

                        // 進行度に対応したエーテル病名を返す
                        if (nextValue - 1 < nameArray.Length)
                            return nameArray[nextValue - 1];

                        return names;
                    },
                    (int index, string s) =>
                    {
                        // リスト選択時処理
                        var selectedRow = etherDiseaseList[index];
                        ApplyEther(req.chara, selectedRow, req.vec);
                    })
                .SetSize(500) // キャラ名がはみ出さないように横幅を長くする
                .SetHeader($"Select Ether Disease : {req.chara?.Name}"); // 誰かわかるよう名前をリストヘッダーに表示

            // レイヤーが閉じるのを待つ
            EClass.core.StartCoroutine(WaitLayerClose(layer));
        }

        // レイヤーが閉じられるタイミングをwhileで待機する処理
        public static IEnumerator WaitLayerClose(Layer layer)
        {
            // try-finallyで確実にフラグをリセットする
            try
            {
                // NOTE: layer != null チェックが無いとNull参照エラー
                while (layer != null && layer.gameObject && layer.gameObject.activeSelf)
                {
                    yield return null;
                }
            }
            finally
            {
                // エラーや例外でループを抜けた場合でも実行される

                // キューから現在のリクエストを削除
                if (queue.Count > 0)
                    queue.Dequeue();

                // 選択中フラグを更新
                isSelecting = false;

                // 次のキューを処理
                ProcessQueue();
            }
        }

        // 対象キャラにエーテル病を発病させる処理
        private static void ApplyEther(Chara c, SourceElement.Row row, int vec)
        {
            // 対象エーテル病の罹患状態を取得
            var element = c.elements.GetElement(row.id);

            // エーテル病進行度
            var num = 1;

            // 初感染ではない時は進行度変数を更新
            if (element != null)
            {
                num = element.Value + vec;

                if (num > element.source.max)
                    num = element.source.max - 1;
            }

            // テキストログ表示
            c.Say("mutation_gain", c);

            // フィート設定
            c.SetFeat(row.id, num);

            // エーテル病罹患履歴がnullの時（初感染）は作成
            c.c_corruptionHistory ??= [];

            // エーテル病罹患履歴に追記
            c.c_corruptionHistory.Add(row.id);

            // 更新された罹患状態を取得
            var updatedElement = c.elements.GetElement(row.id);

            // ポップアップテキストを表示
            WidgetPopText.Say("popEther".lang(updatedElement.Name, c.Name));
            
            // エーテル病初感染時の手紙イベント対応処理
            if (c.IsPC && !EClass.player.flags.gotEtherDisease)
            {
                EClass.player.flags.gotEtherDisease = true;
                var thing = ThingGen.Create("parchment");
                thing.SetStr(53, "letter_ether");
                var thing2 = ThingGen.Create("1165");
                thing2.SetBlessedState(BlessedState.Normal);
                var p = ThingGen.CreateParcel(null, thing2, thing);
                EClass.world.SendPackage(p);
            }

            // エフェクト表示
            if (EClass.core.IsGameStarted && c.pos != null)
            {
                c.PlaySound("mutation_ether");
                c.PlayEffect("mutation");
                var isNegative = row.tag.Contains("neg");
                Msg.SetColor(isNegative ? Msg.colors.MutateBad : Msg.colors.MutateGood);
                c.Say(row.GetText(isNegative ? "textDec" : "textInc", returnNull: true) ?? row.alias, c);
            }
        }
    }
}