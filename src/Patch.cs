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
            if ((vec == 0) || !ether)
                // エーテル病以外の変異時（自己変容）は何もしない
                return true;

            // コンフィグ判定
            bool enabled;
            
            // コンフィグがNullだったら何もしない
            if (Plugin.Instance is null || Plugin.Instance.EnableForPlayer is null ||
                Plugin.Instance.EnableForMember is null || Plugin.Instance.EnableForOther is null)
                return true;

            if (__instance.IsPC)
            {
                enabled = Plugin.Instance.EnableForPlayer.Value;
            }
            else if (__instance.IsPCParty)
            {
                enabled = Plugin.Instance.EnableForMember.Value;
            }
            else
            {
                enabled = Plugin.Instance.EnableForOther.Value;
            }

            if (!enabled)
                // コンフィグで無効の場合は通常の処理を行う
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

                // 治療時(vec < 0)
                if (req.vec < 0)
                {
                    // 罹患していない(Level 0)場合は除外
                    if (currentLevel <= 0)
                        continue;
                }
                // 感染時(vec > 0)
                else
                {
                    // 罹患レベルが最大値に達している場合は除外
                    if (currentLevel == row.max)
                        continue;
                }

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

            // 感染時と治療時でヘッダーテキストを変える
            var headerText = req.vec > 0 ? "Select Ether Disease" : "Cure Ether Disease";

            layer.SetList(etherDiseaseList,
                    (row) =>
                    {
                        // エーテル病取得
                        var element = req.chara?.elements.GetElement(row.id);

                        // エーテル病の進行度を取得
                        // 感染時は次のレベル、治療時は現在のレベルを表示
                        var nextValue = (element?.Value ?? 0) + (req.vec > 0 ? req.vec : 0);

                        // example : 重力発生\n大きな重力\nとてつもない重力
                        var names = row.GetName();

                        // 改行で分割して対象レベルの名前を表示
                        string[] nameArray = names.Split('\n');

                        // 進行度に対応したエーテル病名を返す
                        if (nextValue - 1 < nameArray.Length && nextValue - 1 >= 0)
                            return nameArray[nextValue - 1];

                        return names;
                    },
                    (index, _) =>
                    {
                        // リスト選択時処理
                        var selectedRow = etherDiseaseList[index];
                        Utils.ApplyEther(req.chara, selectedRow, req.vec);
                    })
                .SetSize(500) // キャラ名がはみ出さないように横幅を長くする
                .SetHeader($"{headerText} : {req.chara?.Name}"); // 誰かわかるよう対象キャラの名前をヘッダーに表示

            // レイヤーが閉じるのを待つ
            EClass.core.StartCoroutine(WaitLayerClose(layer));
        }

        // レイヤーが閉じられるタイミングを待機する処理
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
    }
}